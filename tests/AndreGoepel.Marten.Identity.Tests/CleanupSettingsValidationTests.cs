using AndreGoepel.Marten.Identity;

namespace AndreGoepel.Marten.Identity.Tests;

public class CleanupSettingsValidationTests
{
    [Fact]
    public void Validate_DefaultSettings_DoesNotThrow()
    {
        var settings = new CleanupSettings();

        var ex = Record.Exception(() => CleanupSettingsService.Validate(settings));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(CleanupSettingsService.MinRetentionDays)]
    [InlineData(30)]
    [InlineData(CleanupSettingsService.MaxRetentionDays)]
    public void Validate_RetentionWithinRange_DoesNotThrow(int retentionDays)
    {
        var settings = new CleanupSettings { RetentionDays = retentionDays };

        var ex = Record.Exception(() => CleanupSettingsService.Validate(settings));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999999)] // the exploit value: a future cutoff that purges everything
    [InlineData(CleanupSettingsService.MaxRetentionDays + 1)]
    public void Validate_RetentionOutOfRange_Throws(int retentionDays)
    {
        var settings = new CleanupSettings { RetentionDays = retentionDays };

        Assert.Throws<ArgumentOutOfRangeException>(() => CleanupSettingsService.Validate(settings));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a cron")]
    [InlineData("0 0 0 * *")] // too few fields for Quartz
    public void Validate_InvalidCron_Throws(string cron)
    {
        var settings = new CleanupSettings { CronSchedule = cron };

        Assert.Throws<ArgumentException>(() => CleanupSettingsService.Validate(settings));
    }

    [Theory]
    [InlineData("0 0 0 * * ?")]
    [InlineData("0 0 3 * * ?")]
    public void Validate_ValidCron_DoesNotThrow(string cron)
    {
        var settings = new CleanupSettings { CronSchedule = cron };

        var ex = Record.Exception(() => CleanupSettingsService.Validate(settings));

        Assert.Null(ex);
    }
}
