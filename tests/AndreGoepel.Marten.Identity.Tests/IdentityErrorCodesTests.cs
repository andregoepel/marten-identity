using System.Reflection;
using System.Text.RegularExpressions;

namespace AndreGoepel.Marten.Identity.Tests;

/// <summary>
/// Guards the error-code contract the UI translates against (#114).
/// </summary>
public partial class IdentityErrorCodesTests
{
    [Fact]
    public void EveryStoreErrorCarriesACode()
    {
        // A code-less IdentityError leaves the UI nothing to key a translation off, so it can
        // only ever show the English Description. Grepping the sources is crude, but it catches
        // the case a unit test cannot: a new error added later without a code.
        var offenders = new List<string>();

        foreach (var file in StoreSources())
        {
            var text = File.ReadAllText(file);

            foreach (Match match in IdentityErrorConstruction().Matches(text))
            {
                if (!match.Value.Contains("Code =", StringComparison.Ordinal))
                {
                    var line = text[..match.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(file)}:{line}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "IdentityError constructed without a Code at: " + string.Join(", ", offenders)
        );
    }

    [Fact]
    public void CodesAreUniqueAndNonEmpty()
    {
        var codes = Codes();

        Assert.NotEmpty(codes);
        Assert.All(codes, c => Assert.False(string.IsNullOrWhiteSpace(c)));
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CodesDoNotCollideWithTheFrameworksOwnDescriberCodes()
    {
        // ASP.NET Core's IdentityErrorDescriber emits codes like "DuplicateEmail" and
        // "ConcurrencyFailure" too. Where we reuse a name, the meaning must match, because a UI
        // maps code -> message without knowing which layer raised it. These two are deliberate
        // overlaps; anything new colliding by accident would silently mistranslate.
        var deliberateOverlaps = new[]
        {
            IdentityErrorCodes.DuplicateEmail,
            IdentityErrorCodes.ConcurrencyFailure,
        };

        var frameworkCodes = typeof(Microsoft.AspNetCore.Identity.IdentityErrorDescriber)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        var accidental = Codes()
            .Where(c => frameworkCodes.Contains(c) && !deliberateOverlaps.Contains(c))
            .ToArray();

        Assert.True(
            accidental.Length == 0,
            "Codes collide with IdentityErrorDescriber: " + string.Join(", ", accidental)
        );
    }

    private static string[] Codes() =>
        typeof(IdentityErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false })
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

    private static IEnumerable<string> StoreSources()
    {
        var root = RepositoryRoot();
        yield return Path.Combine(root, "src/AndreGoepel.Marten.Identity/Users/UserStore.cs");
        yield return Path.Combine(root, "src/AndreGoepel.Marten.Identity/Roles/RoleStore.cs");
        yield return Path.Combine(
            root,
            "src/AndreGoepel.Marten.Identity/Users/UserInvitationService.cs"
        );
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    // Matches `new IdentityError { ... }` / `new IdentityError() { ... }` up to the closing brace.
    [GeneratedRegex(@"new IdentityError\s*(\(\))?\s*\{[^}]*\}", RegexOptions.Singleline)]
    private static partial Regex IdentityErrorConstruction();
}
