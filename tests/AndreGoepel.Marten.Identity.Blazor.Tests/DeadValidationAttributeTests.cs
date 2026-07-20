using System.Text.RegularExpressions;

namespace AndreGoepel.Marten.Identity.Blazor.Tests;

/// <summary>
/// Stops dead validation attributes being reintroduced on Radzen-validated forms (#114).
/// </summary>
public partial class DeadValidationAttributeTests
{
    [Fact]
    public void PagesValidatedByRadzenCarryNoDataAnnotationMessages()
    {
        // A page validates either via <DataAnnotationsValidator /> (EditForm + form post) or via
        // Radzen validator components (RadzenTemplateForm / CardForm) — never both. On the
        // Radzen ones, DataAnnotations are never evaluated, so an ErrorMessage there is text a
        // translator would dutifully translate and a user would never see. Worse, someone
        // tightening a rule would edit the attribute and see no effect.
        var offenders = new List<string>();

        foreach (
            var file in Directory.EnumerateFiles(
                PagesRoot(),
                "*.razor",
                SearchOption.AllDirectories
            )
        )
        {
            var text = File.ReadAllText(file);

            var validatedByDataAnnotations = text.Contains(
                "<DataAnnotationsValidator",
                StringComparison.Ordinal
            );
            if (validatedByDataAnnotations)
            {
                continue;
            }

            foreach (Match match in ErrorMessageAttribute().Matches(text))
            {
                var line = text[..match.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "DataAnnotation ErrorMessage on a form that does not run DataAnnotations "
                + "(the rule belongs in a Radzen validator instead): "
                + string.Join(", ", offenders)
        );
    }

    private static string PagesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(
            dir!.FullName,
            "src/AndreGoepel.Marten.Identity.Blazor/Components/Account/Pages"
        );
    }

    [GeneratedRegex(@"ErrorMessage\s*=")]
    private static partial Regex ErrorMessageAttribute();
}
