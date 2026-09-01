namespace Plana.TransientUI;

public sealed record TransientActionItem(string Id, string Name, string Description, string Type, string Source);
public sealed record TransientActionGroup(string Id, string Name, IReadOnlyList<string> ActionIds);
