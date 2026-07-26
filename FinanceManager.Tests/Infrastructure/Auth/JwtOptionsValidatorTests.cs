using FinanceManager.Infrastructure.Auth;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FinanceManager.Tests.Infrastructure.Auth;

public sealed class JwtOptionsValidatorTests
{
    [Fact]
    public void Validate_ShouldFailInProduction_WhenJwtKeyMissing()
    {
        var result = CreateProductionValidator().Validate(null, CreateValidOptions(key: string.Empty));

        Assert.False(result.Succeeded);
        Assert.Contains("Jwt:Key must be configured", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldFailInProduction_WhenJwtKeyIsPlaceholder()
    {
        var result = CreateProductionValidator().Validate(null, CreateValidOptions(key: "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64"));

        Assert.False(result.Succeeded);
        Assert.Contains("placeholder", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldFailInProduction_WhenJwtKeyIsShorterThan256Bits()
    {
        var result = CreateProductionValidator().Validate(null, CreateValidOptions(key: "short-key"));

        Assert.False(result.Succeeded);
        Assert.Contains("at least 32 UTF-8 bytes", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldFail_WhenIssuerMissing()
    {
        var result = CreateDevelopmentValidator().Validate(null, CreateValidOptions(issuer: string.Empty));

        Assert.False(result.Succeeded);
        Assert.Contains("Jwt:Issuer must be configured", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAudienceMissing()
    {
        var result = CreateDevelopmentValidator().Validate(null, CreateValidOptions(audience: string.Empty));

        Assert.False(result.Succeeded);
        Assert.Contains("Jwt:Audience must be configured", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldFailInProduction_WhenLifetimeExceedsMaximum()
    {
        var result = CreateProductionValidator().Validate(null, CreateValidOptions(lifetimeMinutes: 1441));

        Assert.False(result.Succeeded);
        Assert.Contains("must not exceed 1440", result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldAllowPlaceholderKeyInDevelopment()
    {
        var result = CreateDevelopmentValidator().Validate(null, CreateValidOptions(key: "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64"));

        Assert.True(result.Succeeded);
    }

    private static JwtOptionsValidator CreateProductionValidator()
    {
        return new JwtOptionsValidator(new TestHostEnvironment("Production"));
    }

    private static JwtOptionsValidator CreateDevelopmentValidator()
    {
        return new JwtOptionsValidator(new TestHostEnvironment(Environments.Development));
    }

    private static JwtOptions CreateValidOptions(
        string key = "test-signing-key-with-sufficient-length-1234567890",
        string issuer = "financemanager",
        string audience = "financemanager",
        int lifetimeMinutes = 30)
    {
        return new JwtOptions
        {
            Key = key,
            Issuer = issuer,
            Audience = audience,
            LifetimeMinutes = lifetimeMinutes
        };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "FinanceManager.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
