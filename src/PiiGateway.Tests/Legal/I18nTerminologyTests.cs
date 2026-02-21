using FluentAssertions;

namespace PiiGateway.Tests.Legal;

[Trait("Category", "Legal")]
public class I18nTerminologyTests
{
    private static readonly string I18nBasePath = Path.Combine(
        FindRepoRoot(), "src", "frontend", "src", "i18n");

    private static readonly string[] ForbiddenTerms =
    {
        "anonymization",
        "anonymisation",
        "anonymize",
        "anonymise",
        "Anonymisierung",
        "anonymisierung",
        "anonymisieren",
    };

    private static readonly string[] RequiredTerms =
    {
        "pseudonymization",
        "Pseudonymisierung",
    };

    [Fact]
    public void I18nFiles_ShouldNotContainAnonymizationTerminology()
    {
        var jsonFiles = Directory.GetFiles(I18nBasePath, "*.json", SearchOption.AllDirectories);
        jsonFiles.Should().NotBeEmpty("i18n directory should contain JSON files");

        var violations = new List<string>();

        foreach (var file in jsonFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(I18nBasePath, file);

            foreach (var term in ForbiddenTerms)
            {
                if (content.Contains(term, StringComparison.Ordinal))
                {
                    violations.Add($"{relativePath} contains forbidden term '{term}'");
                }
            }
        }

        violations.Should().BeEmpty(
            "i18n files should use 'pseudonymization'/'Pseudonymisierung' instead of 'anonymization'/'Anonymisierung'. " +
            $"Violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void I18nFiles_ShouldContainPseudonymizationTerminology()
    {
        var jsonFiles = Directory.GetFiles(I18nBasePath, "*.json", SearchOption.AllDirectories);
        var allContent = string.Join("\n", jsonFiles.Select(File.ReadAllText));

        foreach (var term in RequiredTerms)
        {
            allContent.Should().Contain(term,
                $"i18n files should contain the term '{term}' somewhere in translations");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "src", "frontend")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: try common paths relative to test output
        var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (Directory.Exists(Path.Combine(fallback, "src", "frontend")))
            return fallback;

        throw new DirectoryNotFoundException(
            "Could not find repository root with src/frontend directory. " +
            $"Searched from: {AppContext.BaseDirectory}");
    }
}
