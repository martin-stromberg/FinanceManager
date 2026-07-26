using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Auth;

/// <summary>
/// Validates JWT configuration before the application starts.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const int MinimumProductionKeyBytes = 32;
    private const int MaximumProductionLifetimeMinutes = 1440;

    private static readonly string[] PlaceholderKeys =
    [
        "PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64",
        "CHANGE_ME",
        "REPLACE_ME",
        "TODO"
    ];

    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of <see cref="JwtOptionsValidator"/>.
    /// </summary>
    /// <param name="environment">Host environment used to distinguish development from production-like environments.</param>
    public JwtOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var isProductionLike = !_environment.IsDevelopment();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience must be configured.");
        }

        if (options.LifetimeMinutes <= 0)
        {
            failures.Add("Jwt:LifetimeMinutes must be greater than 0.");
        }

        if (isProductionLike && options.LifetimeMinutes > MaximumProductionLifetimeMinutes)
        {
            failures.Add($"Jwt:LifetimeMinutes must not exceed {MaximumProductionLifetimeMinutes} in production-like environments.");
        }

        if (isProductionLike)
        {
            if (string.IsNullOrWhiteSpace(options.Key))
            {
                failures.Add("Jwt:Key must be configured in production-like environments.");
            }
            else
            {
                if (PlaceholderKeys.Contains(options.Key, StringComparer.OrdinalIgnoreCase))
                {
                    failures.Add("Jwt:Key must not use a known placeholder value in production-like environments.");
                }

                if (Encoding.UTF8.GetByteCount(options.Key) < MinimumProductionKeyBytes)
                {
                    failures.Add($"Jwt:Key must contain at least {MinimumProductionKeyBytes} UTF-8 bytes in production-like environments.");
                }
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
