﻿using FrostAura.Libraries.Semantic.Core.Extensions.Configuration;
using FrostAura.Libraries.Semantic.Core.Interfaces.Data;
using FrostAura.Libraries.Semantic.Core.Models.Configuration;
using FrostAura.Libraries.Semantic.Core.Thoughts.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Semantic.Core.Tests.Thoughts.IO;

public class WebDriverThoughtsTests
{
    [Fact]
    public void Constructor_WithInvalidServiceProvider_ShouldThrow()
    {
        IServiceProvider serviceProvider = null;
        ISemanticKernelLanguageModelsDataAccess semanticKernelLanguageModels = Substitute.For<ISemanticKernelLanguageModelsDataAccess>();
        IOptions<AppConfig> appConfigOptions = Substitute.For<IOptions<AppConfig>>();
        ILogger<WebDriverThoughts> logger = Substitute.For<ILogger<WebDriverThoughts>>();

        var actual = Assert.Throws<ArgumentNullException>(() => new WebDriverThoughts(serviceProvider, semanticKernelLanguageModels, appConfigOptions, logger));

        Assert.Equal(nameof(serviceProvider), actual.ParamName);
    }

    [Fact]
    public void Constructor_WithInvalidISemanticKernelLanguageModelsDataAccess_ShouldThrow()
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        ISemanticKernelLanguageModelsDataAccess semanticKernelLanguageModels = null;
        IOptions<AppConfig> appConfigOptions = Substitute.For<IOptions<AppConfig>>();
        ILogger<WebDriverThoughts> logger = Substitute.For<ILogger<WebDriverThoughts>>();

        var actual = Assert.Throws<ArgumentNullException>(() => new WebDriverThoughts(serviceProvider, semanticKernelLanguageModels, appConfigOptions, logger));

        Assert.Equal(nameof(semanticKernelLanguageModels), actual.ParamName);
    }

    [Fact]
    public void Constructor_WithInvalidAppConfigOptions_ShouldThrow()
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        ISemanticKernelLanguageModelsDataAccess semanticKernelLanguageModels = Substitute.For<ISemanticKernelLanguageModelsDataAccess>();
        IOptions<AppConfig> appConfigOptions = null;
        ILogger<WebDriverThoughts> logger = Substitute.For<ILogger<WebDriverThoughts>>();

        var actual = Assert.Throws<ArgumentNullException>(() => new WebDriverThoughts(serviceProvider, semanticKernelLanguageModels, appConfigOptions, logger));

        Assert.Equal(nameof(appConfigOptions), actual.ParamName);
    }

    [Fact]
    public void Constructor_WithInvalidLogger_ShouldThrow()
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        ISemanticKernelLanguageModelsDataAccess semanticKernelLanguageModels = Substitute.For<ISemanticKernelLanguageModelsDataAccess>();
        IOptions<AppConfig> appConfigOptions = Substitute.For<IOptions<AppConfig>>();
        ILogger<WebDriverThoughts> logger = null;

        var actual = Assert.Throws<ArgumentNullException>(() => new WebDriverThoughts(serviceProvider, semanticKernelLanguageModels, appConfigOptions, logger));

        Assert.Equal(nameof(logger), actual.ParamName);
    }

    [Fact]
    public void Constructor_WithValidParams_ShouldConstruct()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        var semanticKernelLanguageModels = Substitute.For<ISemanticKernelLanguageModelsDataAccess>();
        var appConfigOptions = Substitute.For<IOptions<AppConfig>>();
        appConfigOptions.Value.Returns(new AppConfig());
        var logger = Substitute.For<ILogger<WebDriverThoughts>>();

        var actual = new WebDriverThoughts(serviceProvider, semanticKernelLanguageModels, appConfigOptions, logger);

        Assert.NotNull(actual);
    }

    [Fact]
    public async Task LoadTextAsync_WithInvalidInput_ShouldThrow()
    {
        var serviceCollection = new ServiceCollection()
            .AddSemanticCore(out var configuration);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var semanticKernelLanguageModels = Substitute.For<ISemanticKernelLanguageModelsDataAccess>();
        var appConfigOptions = serviceProvider.GetRequiredService<IOptions<AppConfig>>();
        var logger = Substitute.For<ILogger<WebDriverThoughts>>();
        var instance = new WebDriverThoughts(serviceProvider, semanticKernelLanguageModels, appConfigOptions, logger);
        string uri = default;

        var actual = await Assert.ThrowsAsync<ArgumentNullException>(async () => await instance.LoadWebsiteTextAsync(uri));

        Assert.Equal(nameof(uri), actual.ParamName);
    }

    [Fact]
    public async Task LoadTextAsync_WithValidInput_ShouldRespond()
    {
        var serviceCollection = new ServiceCollection()
            .AddSemanticCore(out var configuration);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var semanticKernelLanguageModels = Substitute.For<ISemanticKernelLanguageModelsDataAccess>();
        var appConfigOptions = serviceProvider.GetRequiredService<IOptions<AppConfig>>();
        var logger = Substitute.For<ILogger<WebDriverThoughts>>();
        var instance = new WebDriverThoughts(serviceProvider, semanticKernelLanguageModels, appConfigOptions, logger);
        var uri = "https://techcrunch.com";

        var actual = await instance.LoadWebsiteTextAsync(uri);

        Assert.NotEmpty(actual);
    }
}
