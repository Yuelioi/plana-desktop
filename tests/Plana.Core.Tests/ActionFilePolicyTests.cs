using Plana.Core.Actions;

namespace Plana.Core.Tests;

public sealed class ActionFilePolicyTests
{
    [Theory]
    [InlineData("report.pdf")]
    [InlineData("notes.txt")]
    [InlineData("image.png")]
    public void AllowsNonExecutableFiles(string path) =>
        Assert.True(ActionFilePolicy.CanOpenWithFileCapability(path));

    [Theory]
    [InlineData("program.exe")]
    [InlineData("helper.CMD")]
    [InlineData("script.ps1")]
    [InlineData("installer.msi")]
    [InlineData("shortcut.lnk")]
    public void RejectsExecutableAndScriptFiles(string path) =>
        Assert.False(ActionFilePolicy.CanOpenWithFileCapability(path));
}
