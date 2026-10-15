using System.Xml.Linq;

namespace MMV.Api.Tests;

/// <summary>
/// Vérifie le sens unique des références entre projets à partir des .csproj réels.
/// </summary>
public sealed class ProjectReferenceRulesTests
{
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["MMV.Domain"] = [],
        ["MMV.Application"] = ["MMV.Domain"],
        ["MMV.Infrastructure"] = ["MMV.Application"],
        ["MMV.Api"] = ["MMV.Application", "MMV.Infrastructure"],
    };

    [Theory]
    [InlineData("MMV.Domain")]
    [InlineData("MMV.Application")]
    [InlineData("MMV.Infrastructure")]
    [InlineData("MMV.Api")]
    public void ProjectReferencesFollowAllowedDirection(string project)
    {
        var actual = ReadProjectReferences(project);

        Assert.Equal(AllowedReferences[project].Order(), actual.Order());
    }

    [Fact]
    public void NoProjectReferencesPersistenceProviderOtherThanNpgsql()
    {
        var forbidden = new[] { "Sqlite", "InMemory", "SqlServer", "MySql", "Oracle", "Cosmos" };

        foreach (var project in AllowedReferences.Keys)
        {
            var packages = ReadCsproj(project)
                .Descendants("PackageReference")
                .Select(p => (string?)p.Attribute("Include") ?? string.Empty);

            foreach (var package in packages)
            {
                Assert.DoesNotContain(forbidden, f => package.Contains(f, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static string[] ReadProjectReferences(string project) =>
        ReadCsproj(project)
            .Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension((string)r.Attribute("Include")!))
            .ToArray();

    private static XDocument ReadCsproj(string project) =>
        XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", project, $"{project}.csproj"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MMV.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Racine du dépôt (MMV.slnx) introuvable.");
    }
}
