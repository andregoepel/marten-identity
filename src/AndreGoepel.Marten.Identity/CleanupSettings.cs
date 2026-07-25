using AndreGoepel.Marten.Configuration;

namespace AndreGoepel.Marten.Identity;

public sealed class CleanupSettings : SettingsDocument, ISettingsDocument<CleanupSettings>
{
    public static string DocumentId => "cleanup-settings";

    public int RetentionDays { get; set; } = 30;
    public string CronSchedule { get; set; } = "0 0 0 * * ?";
}
