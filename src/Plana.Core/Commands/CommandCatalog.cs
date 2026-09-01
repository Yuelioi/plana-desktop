namespace Plana.Core.Commands;

public static class CommandTargetKinds
{
    public const string Action = "action";
    public const string Navigate = "navigate";
    public const string ToolGroup = "tool-group";
    public const string Chat = "chat";
}

public sealed record CommandTarget(string Kind, string Value);

public sealed record CommandDescriptor(
    string Id,
    string Title,
    string Subtitle,
    string IconGlyph,
    string Source,
    IReadOnlyList<string> Aliases,
    CommandTarget Target);

public sealed class CommandCatalog(IEnumerable<CommandDescriptor> commands)
{
    private readonly IReadOnlyList<CommandDescriptor> _commands = commands
        .GroupBy(command => command.Id, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.Last())
        .ToArray();

    public IReadOnlyList<CommandDescriptor> Search(string? query, int limit = 50)
    {
        if (limit <= 0) return [];
        var needle = query?.Trim() ?? string.Empty;
        if (needle.Length == 0)
            return _commands.OrderBy(command => command.Title, StringComparer.CurrentCultureIgnoreCase).Take(limit).ToArray();

        var tokens = needle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _commands
            .Select(command => (command, score: Score(command, needle, tokens)))
            .Where(result => result.score >= 0)
            .OrderByDescending(result => result.score)
            .ThenBy(result => result.command.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .Select(result => result.command)
            .ToArray();
    }

    private static int Score(CommandDescriptor command, string needle, IReadOnlyList<string> tokens)
    {
        var searchable = new[] { command.Title, command.Subtitle, command.Source, command.Id }
            .Concat(command.Aliases)
            .ToArray();
        if (tokens.Any(token => !searchable.Any(value => value.Contains(token, StringComparison.CurrentCultureIgnoreCase)))) return -1;
        if (command.Title.Equals(needle, StringComparison.CurrentCultureIgnoreCase)) return 100;
        if (command.Title.StartsWith(needle, StringComparison.CurrentCultureIgnoreCase)) return 85;
        if (command.Aliases.Any(alias => alias.Equals(needle, StringComparison.CurrentCultureIgnoreCase))) return 80;
        if (command.Aliases.Any(alias => alias.StartsWith(needle, StringComparison.CurrentCultureIgnoreCase))) return 70;
        if (command.Title.Contains(needle, StringComparison.CurrentCultureIgnoreCase)) return 60;
        if (command.Subtitle.Contains(needle, StringComparison.CurrentCultureIgnoreCase)) return 45;
        if (command.Source.Contains(needle, StringComparison.CurrentCultureIgnoreCase)) return 35;
        return 25;
    }
}
