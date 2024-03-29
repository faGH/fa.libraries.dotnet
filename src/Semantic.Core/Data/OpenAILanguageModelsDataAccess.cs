﻿using System.Net;
using FrostAura.Libraries.Core.Extensions.Validation;
using FrostAura.Libraries.Semantic.Core.Abstractions.Thoughts;
using FrostAura.Libraries.Semantic.Core.Enums.Models;
using FrostAura.Libraries.Semantic.Core.Interfaces.Data;
using FrostAura.Libraries.Semantic.Core.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.TextToImage;
using Polly;
using PluginsMemory = Microsoft.SemanticKernel.Plugins.Memory;

namespace FrostAura.Libraries.Semantic.Core.Data;

/// <summary>
/// An OpenAI language models communication component.
///
/// Support for Azure by simply providing an endpoint in the config object.
/// </summary>
public class OpenAILanguageModelsDataAccess : ISemanticKernelLanguageModelsDataAccess
{
    /// <summary>
    /// The dependency service provider.
    /// </summary>
    protected readonly IServiceProvider _serviceProvider;
    /// <summary>
    /// Configuration for OpenAI models.
    /// </summary>
    private readonly OpenAIConfig _openAIConfig;
    /// <summary>
    /// Instance logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Constructor to allow for injecting dependencies.
    /// </summary>
    /// <param name="serviceProvider">The dependency service provider.</param>
    /// <param name="openAIConfigOptions">Configuration for OpenAI models.</param>
    /// <param name="logger">Instance logger.</param>
    public OpenAILanguageModelsDataAccess(IServiceProvider serviceProvider, IOptions<OpenAIConfig> openAIConfigOptions, ILogger<OpenAILanguageModelsDataAccess> logger)
	{
        _serviceProvider = serviceProvider.ThrowIfNull(nameof(serviceProvider));
        _openAIConfig = openAIConfigOptions
            .ThrowIfNull(nameof(openAIConfigOptions))
            .Value
            .ThrowIfNull(nameof(openAIConfigOptions));
        _logger = logger.ThrowIfNull(nameof(logger));
    }

    /// <summary>
    /// Create a text generation service instance.
    /// </summary>
    /// <param name="modelType">The model type to communicate with.</param>
    /// <param name="token">Token to allow for cancelling downstream operations.</param>
    /// <returns>Chat model instance.</returns>
    public async Task<IChatCompletionService> GetChatCompletionModelAsync(ModelType modelType, CancellationToken token)
    {
        using (_logger.BeginScope("{MethodName}", nameof(GetChatCompletionModelAsync)))
        {
            _logger.LogInformation("Getting a chat completion model of type {ModelType}.", modelType);

            var kernel = await GetKernelAsync(token);
            var model = kernel.GetRequiredService<IChatCompletionService>(serviceKey: modelType.ToString());

            return model;
        }
    }

    /// <summary>
    /// Create a default text embedding generation service instance.
    /// </summary>
    /// <param name="token">Token to allow for cancelling downstream operations.</param>
    /// <returns>Text embedding generation service instance.</returns>
    public async Task<ITextEmbeddingGenerationService> GetEmbeddingModelAsync(CancellationToken token)
    {
        using (_logger.BeginScope("{MethodName}", nameof(GetEmbeddingModelAsync)))
        {
            var kernel = await GetKernelAsync(token);
            var model = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

            return model;
        }
    }

    /// <summary>
    /// Generate an image with a generative AI model and return the URL where the image is hosted.
    /// </summary>
    /// <param name="prompt">The promt for the generation.</param>
    /// <param name="token">Token to allow for cancelling downstream operations.</param>
    /// <returns>Semantic memory instance.</returns>
    public async Task<string> GenerateImageAndGetUrlAsync(string prompt, CancellationToken token)
    {
        using (_logger.BeginScope("{MethodName}", nameof(GenerateImageAndGetUrlAsync)))
        {
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 256,
                Temperature = 1
            };
            var kernel = await GetKernelAsync(token);
            var dallE = kernel.GetRequiredService<ITextToImageService>();

            // Use DALL-E 3 to generate an image. OpenAI in this case returns a URL (though you can ask to return a base64 image)
            _logger.LogInformation("Generating image using Dall-E 3: {Prompt}", prompt);
            var imageUrl = await dallE.GenerateImageAsync(prompt, 1024, 1024);
            _logger.LogInformation("Image generated successfully: {ImageUrl}", imageUrl);

            return imageUrl;
        }
    }

    /// <summary>
    /// Get a default Kernel instance.
    /// </summary>
    /// <param name="token">Token to allow for cancelling downstream operations.</param>
    /// <returns>a Default Kernel instance.</returns>
    public Task<Kernel> GetKernelAsync(CancellationToken token)
    {
        var builder = Kernel
            .CreateBuilder();

        builder
            .Services
            .AddLogging(c => c
                .AddDebug()
                .SetMinimumLevel(LogLevel.Trace))
            .ConfigureHttpClientDefaults(c =>
            {
                c.AddStandardResilienceHandler()
                    .Configure(o =>
                    {
                        o.TotalRequestTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromMinutes(_openAIConfig.DefaultTimeoutInMin) };
                        o.AttemptTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromMinutes(_openAIConfig.DefaultTimeoutInMin) };
                        o.CircuitBreaker = new HttpCircuitBreakerStrategyOptions { SamplingDuration = TimeSpan.FromMinutes(_openAIConfig.DefaultTimeoutInMin * 2) };
                        o.Retry.BackoffType = DelayBackoffType.Exponential;
                        o.Retry.MaxRetryAttempts = 5;
                        o.Retry.UseJitter = true;
                        o.Retry.ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode is not HttpStatusCode.OK);
                    });
            });

        var allThoughtInstances = _serviceProvider
            .GetServices<BaseThought>()
            .ToList();

        foreach (var plugin in allThoughtInstances)
        {
            builder
                .Plugins
                .AddFromObject(plugin, plugin.GetType().Name);
        }

        // Configure the public kernel.
        if (string.IsNullOrWhiteSpace(_openAIConfig.Endpoint))
        {
            builder
                .Services
                .AddOpenAIChatCompletion(_openAIConfig.LargeModelName, _openAIConfig.ApiKey, serviceId: ModelType.LargeLLM.ToString(), orgId: _openAIConfig.OrgId)
                .AddOpenAIChatCompletion(_openAIConfig.SmallModelName, _openAIConfig.ApiKey, serviceId: ModelType.SmallLLM.ToString(), orgId: _openAIConfig.OrgId)
                .AddOpenAIChatCompletion(_openAIConfig.VisionModelName, _openAIConfig.ApiKey, serviceId: ModelType.Vision.ToString(), orgId: _openAIConfig.OrgId)
                .AddOpenAITextEmbeddingGeneration(_openAIConfig.EmbeddingModelName, _openAIConfig.ApiKey, serviceId: ModelType.Embedding.ToString(), orgId: _openAIConfig.OrgId)
                .AddOpenAITextToImage(_openAIConfig.ApiKey, serviceId: ModelType.ImageGeneration.ToString(), orgId: _openAIConfig.OrgId);
        }
        // Configure the private kernel.
        else
        {
            builder
                .Services
                .AddAzureOpenAIChatCompletion(_openAIConfig.LargeModelName, _openAIConfig.Endpoint, _openAIConfig.ApiKey, serviceId: ModelType.LargeLLM.ToString())
                .AddAzureOpenAIChatCompletion(_openAIConfig.SmallModelName, _openAIConfig.Endpoint, _openAIConfig.ApiKey, serviceId: ModelType.SmallLLM.ToString())
                .AddAzureOpenAIChatCompletion(_openAIConfig.VisionModelName, _openAIConfig.Endpoint, _openAIConfig.ApiKey, serviceId: ModelType.Vision.ToString())
                .AddAzureOpenAITextEmbeddingGeneration(_openAIConfig.EmbeddingModelName, _openAIConfig.Endpoint, _openAIConfig.ApiKey, serviceId: ModelType.Embedding.ToString())
                .AddAzureOpenAITextToImage(_openAIConfig.TextToImageModelName, _openAIConfig.Endpoint, _openAIConfig.ApiKey, serviceId: ModelType.ImageGeneration.ToString());
        }

        return Task.FromResult(builder.Build());
    }

    /// <summary>
    /// Get a semantic memory store.
    /// </summary>
    /// <param name="token">Token to allow for cancelling downstream operations.</param>
    /// <returns>Semantic memory instance.</returns>
    public Task<ISemanticTextMemory> GetSemanticTextMemoryAsync(CancellationToken token)
    {
        var memoryBuilder = new MemoryBuilder()
            .WithMemoryStore(new PluginsMemory.VolatileMemoryStore());

        // Configure the public kernel.
        if (string.IsNullOrWhiteSpace(_openAIConfig.Endpoint))
        {
            memoryBuilder
                .WithOpenAITextEmbeddingGeneration(_openAIConfig.EmbeddingModelName, _openAIConfig.ApiKey, orgId: _openAIConfig.OrgId);
        }
        // Configure the private kernel.
        else
        {
            memoryBuilder
                .WithAzureOpenAITextEmbeddingGeneration(_openAIConfig.EmbeddingModelName, _openAIConfig.Endpoint, _openAIConfig.ApiKey);
        }

        return Task.FromResult(memoryBuilder.Build());
    }
}
