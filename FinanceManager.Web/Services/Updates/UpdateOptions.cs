#pragma warning disable CS1591
namespace FinanceManager.Web.Services.Updates;

public sealed class UpdateOptions
{
    public const string SectionName = "Updates";

    public bool Enabled { get; set; }
    public int CheckIntervalMinutes { get; set; } = 360;
    public string RepositoryOwner { get; set; } = "martin-stromberg";
    public string RepositoryName { get; set; } = "FinanceManager";
    public string ManifestAssetName { get; set; } = "update.json";
    public string WorkingDirectory { get; set; } = "updates";
    public int HealthTimeoutSeconds { get; set; } = 120;
    public long MaxAssetBytes { get; set; } = 512L * 1024L * 1024L;
    public bool HostedServicesEnabled { get; set; } = true;
    public string? WindowsServiceName { get; set; }
    public string? LinuxServiceName { get; set; }
    public string? ExecutablePath { get; set; }
}
#pragma warning restore CS1591
