using System.Xml.Linq;

namespace LangChain.Providers.GigaChat.Tests;

public class PackagingSmokeTests
{
    [Fact]
    public void PackageProjectIncludesNuGetMetadataAndReadme()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(
            root,
            "src",
            "LangChain.Providers.GigaChat",
            "LangChain.Providers.GigaChat.csproj");
        var readmePath = Path.Combine(
            root,
            "docs",
            "nuget",
            "LangChain.Providers.GigaChat.md");
        var project = XDocument.Load(projectPath);

        Assert.Equal("net10.0", Value(project, "TargetFramework"));
        Assert.Equal("LangChain.Providers.GigaChat", Value(project, "PackageId"));
        Assert.Equal("README.md", Value(project, "PackageReadmeFile"));
        Assert.True(File.Exists(readmePath));
        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "PackageReference"),
            element => element.Attribute("Include")?.Value == "LangChain.Core"
                && element.Attribute("Version")?.Value == "0.17.1");
        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "PackageReference"),
            element => element.Attribute("Include")?.Value == "LangChain.Providers.Abstractions"
                && element.Attribute("Version")?.Value == "0.17.0");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GigaChat.Net.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string Value(XDocument document, string name)
    {
        return document
            .Descendants()
            .First(element => element.Name.LocalName == name)
            .Value;
    }
}
