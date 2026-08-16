using AndreGoepel.Marten.Identity.Users;

namespace AndreGoepel.Marten.Identity.Tests.Users;

public class UserExtensionTests
{
    private static readonly DateTimeOffset LockoutEnd = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static User BaseUser() =>
        new()
        {
            Email = "alice@example.com",
            UserName = "alice@example.com",
            PasswordHash = "hash",
            EmailConfirmed = true,
            PhoneNumber = "1234567890",
            AuthenticatorKey = "authkey",
            RecoveryCodes = "code1;code2",
            TwoFactorEnabled = false,
            Deletable = true,
            LockoutEnabled = true,
            LockoutEnd = LockoutEnd,
            AccessFailedCount = 0,
            // IdentityUser assigns a random SecurityStamp in its constructor;
            // pin it so two "identical" fixtures genuinely match.
            SecurityStamp = "stamp",
        };

    // Reflects over every UserChangeSet flag (excluding the derived Any property) so each
    // per-field test proves isolation — that changing one User field flags that field alone,
    // not just that the overall diff is non-empty (#138).
    private static void AssertOnlyFlagged(UserChangeSet changes, string expectedField)
    {
        var flagged = typeof(UserChangeSet)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(bool) && p.Name != nameof(UserChangeSet.Any))
            .Where(p => (bool)p.GetValue(changes)!)
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal([expectedField], flagged);
    }

    [Fact]
    public void DiffAgainst_Identical_ReportsNoChange()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        Assert.False(changes.Any);
    }

    [Fact]
    public void DiffAgainst_SameInstance_ReportsNoChange()
    {
        // Arrange
        var user = BaseUser();

        // Act
        var changes = user.DiffAgainst(user);

        // Assert
        Assert.False(changes.Any);
    }

    [Fact]
    public void DiffAgainst_EmailDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.Email = "bob@example.com";

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.Email));
    }

    [Fact]
    public void DiffAgainst_UserNameDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.UserName = "bob";

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.UserName));
    }

    [Fact]
    public void DiffAgainst_PasswordHashDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.PasswordHash = "differentHash";

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.PasswordHash));
    }

    [Fact]
    public void DiffAgainst_EmailConfirmedDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.EmailConfirmed = false;

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.EmailConfirmed));
    }

    [Fact]
    public void DiffAgainst_PhoneNumberDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.PhoneNumber = "9999999999";

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.PhoneNumber));
    }

    [Fact]
    public void DiffAgainst_AuthenticatorKeyDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.AuthenticatorKey = "differentKey";

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.AuthenticatorKey));
    }

    [Fact]
    public void DiffAgainst_RecoveryCodesDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.RecoveryCodes = "newcode";

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.RecoveryCodes));
    }

    [Fact]
    public void DiffAgainst_TwoFactorEnabledDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.TwoFactorEnabled = true;

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.TwoFactorEnabled));
    }

    [Fact]
    public void DiffAgainst_DeletableDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.Deletable = false;

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.Deletable));
    }

    [Fact]
    public void DiffAgainst_LockoutEnabledDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.LockoutEnabled = false;

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.LockoutEnabled));
    }

    [Fact]
    public void DiffAgainst_LockoutEndDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.LockoutEnd = LockoutEnd.AddHours(1);

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.LockoutEnd));
    }

    [Fact]
    public void DiffAgainst_AccessFailedCountDiffers_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        b.AccessFailedCount = 3;

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.AccessFailedCount));
    }

    [Fact]
    public void DiffAgainst_SecurityStampDiffers_FlagsOnlyThatField()
    {
        // A changed security stamp must be treated as a real change so the
        // update is persisted and existing sessions are invalidated.
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        a.SecurityStamp = "stamp-1";
        b.SecurityStamp = "stamp-2";

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.SecurityStamp));
    }

    [Fact]
    public void DiffAgainst_NullEmailBothSides_ReportsNoChange()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        a.Email = null;
        b.Email = null;

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        Assert.False(changes.Any);
    }

    [Fact]
    public void DiffAgainst_NullVsNonNullEmail_FlagsOnlyThatField()
    {
        // Arrange
        var a = BaseUser();
        var b = BaseUser();
        a.Email = null;

        // Act
        var changes = a.DiffAgainst(b);

        // Assert
        AssertOnlyFlagged(changes, nameof(UserChangeSet.Email));
    }
}
