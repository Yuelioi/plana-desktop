using Plana.Core.Commands;

namespace Plana.Core.Tests;

public sealed class CommandCatalogTests
{
    private static readonly CommandDescriptor OpenProject = new(
        "user.launcher.project", "Open Plana project", "Launch in Windows Terminal", "folder", "Your Actions",
        ["plana", "terminal"], new CommandTarget(CommandTargetKinds.Action, "user.launcher.project"));
    private static readonly CommandDescriptor Settings = new(
        "builtin.navigate.settings", "Settings", "Configure the Companion", "settings", "Plana Desktop",
        ["preferences", "配置"], new CommandTarget(CommandTargetKinds.Navigate, "settings"));

    [Fact]
    public void ExactTitleOutranksAliasAndSubstringMatches()
    {
        var catalog = new CommandCatalog([OpenProject, Settings]);

        var result = catalog.Search("Settings");

        Assert.Equal(Settings.Id, result[0].Id);
    }

    [Fact]
    public void FindsAliasesAndMultiTokenMatchesAcrossFields()
    {
        var catalog = new CommandCatalog([OpenProject, Settings]);

        Assert.Equal(Settings.Id, Assert.Single(catalog.Search("配置")).Id);
        Assert.Equal(OpenProject.Id, Assert.Single(catalog.Search("Plana terminal")).Id);
    }

    [Fact]
    public void DeduplicatesByStableIdentityUsingLatestContribution()
    {
        var replacement = OpenProject with { Title = "Open current project" };
        var catalog = new CommandCatalog([OpenProject, replacement]);

        Assert.Equal("Open current project", Assert.Single(catalog.Search("current")).Title);
    }
}
