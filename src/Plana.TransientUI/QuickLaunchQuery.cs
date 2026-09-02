namespace Plana.TransientUI;

internal static class QuickLaunchQuery
{
    public static string ComposePreview(
        string committedAtEvent,
        string committedAtDispatch,
        int caretAtEvent,
        string composition)
    {
        if (!string.Equals(committedAtEvent, committedAtDispatch, StringComparison.Ordinal))
            return committedAtDispatch;

        var insertion = Math.Clamp(caretAtEvent, 0, committedAtDispatch.Length);
        return committedAtDispatch.Insert(insertion, composition);
    }
}
