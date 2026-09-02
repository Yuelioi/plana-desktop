using Plana.TransientUI;

namespace Plana.TransientUI.Tests;

public sealed class QuickLaunchQueryTests
{
    [Fact]
    public void ComposePreview_DoesNotDuplicateCompositionCommittedBeforeDispatch()
    {
        var query = QuickLaunchQuery.ComposePreview(
            committedAtEvent: string.Empty,
            committedAtDispatch: "p",
            caretAtEvent: 0,
            composition: "p");

        Assert.Equal("p", query);
    }

    [Fact]
    public void ComposePreview_IncludesUncommittedComposition()
    {
        var query = QuickLaunchQuery.ComposePreview(
            committedAtEvent: "a",
            committedAtDispatch: "a",
            caretAtEvent: 1,
            composition: "b");

        Assert.Equal("ab", query);
    }
}
