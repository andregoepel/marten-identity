namespace AndreGoepel.Marten.Identity.Tests;

public class CleanupSettingsValidationTests
{
    [Fact]
    public void Validate_DefaultSettings_DoesNotThrow()
    {
        // Arrange
        var settings = new CleanupSettings();

        // Act
        var ex = Record.Exception(() => CleanupSettingsService.Validate(settings));

        // Assert
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(CleanupSettingsService.MinRetentionDays)]
    [InlineData(30)]
    [InlineData(CleanupSettingsService.MaxRetentionDays)]
    public void Validate_RetentionWithinRange_DoesNotThrow(int retentionDays)
    {
        // Arrange
        var settings = new CleanupSettings { RetentionDays = retentionDays };

        // Act
        var ex = Record.Exception(() => CleanupSettingsService.Validate(settings));

        // Assert
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999999)] // the exploit value: a future cutoff that purges everything
    [InlineData(CleanupSettingsService.MaxRetentionDays + 1)]
    public void Validate_RetentionOutOfRange_Throws(int retentionDays)
    {
        // Arrange
        var settings = new CleanupSettings { RetentionDays = retentionDays };

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => CleanupSettingsService.Validate(settings));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a cron")]
    [InlineData("0 0 0 * *")] // too few fields for Quartz
    public void Validate_InvalidCron_Throws(string cron)
    {
        // Arrange
        var settings = new CleanupSettings { CronSchedule = cron };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => CleanupSettingsService.Validate(settings));
    }

    [Theory]
    [InlineData("0 0 0 * * ?")]
    [InlineData("0 0 3 * * ?")]
    public void Validate_ValidCron_DoesNotThrow(string cron)
    {
        // Arrange
        var settings = new CleanupSettings { CronSchedule = cron };

        // Act
        var ex = Record.Exception(() => CleanupSettingsService.Validate(settings));

        // Assert
        Assert.Null(ex);
    }
}
