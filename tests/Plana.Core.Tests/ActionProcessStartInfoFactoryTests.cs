using Plana.Core.Actions;

namespace Plana.Core.Tests;

public sealed class ActionProcessStartInfoFactoryTests
{
    [Fact]
    public void UsesExecutableDirectoryForAFullPathWhenNoWorkingDirectoryIsConfigured()
    {
        var executable = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "projects", "apps", "yotta", "bin", "Yotta.exe");
        var action = Action(executable);

        var result = ActionProcessStartInfoFactory.CreateForLaunch(executable, action);

        Assert.Equal(Path.GetDirectoryName(executable), result.WorkingDirectory);
    }

    [Fact]
    public void PreservesExplicitWorkingDirectoryAndNumericArgumentOrder()
    {
        var parameters = new Dictionary<string, string>
        {
            ["executable"] = "tool.exe",
            ["arg.10"] = "ten",
            ["arg.2"] = "two",
        };
        var action = new ActionDefinition("test", "Test", ActionKinds.LaunchProcess, parameters, new HashSet<string>());

        var result = ActionProcessStartInfoFactory.Create("tool.exe", action, @"C:\work");

        Assert.Equal(@"C:\work", result.WorkingDirectory);
        Assert.Equal(["two", "ten"], result.ArgumentList);
    }

    private static ActionDefinition Action(string executable) => new(
        "test", "Test", ActionKinds.LaunchProcess,
        new Dictionary<string, string> { ["executable"] = executable }, new HashSet<string>());
}
