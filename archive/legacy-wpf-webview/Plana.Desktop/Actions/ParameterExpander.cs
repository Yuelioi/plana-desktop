using Plana.Core.Actions;

namespace Plana.Desktop.Actions;

internal static class ParameterExpander
{
    public static string Expand(string value, ActionContext context)
    {
        var result = value;
        foreach (var variable in context.VariablesOrEmpty)
        {
            result = result.Replace($"{{{variable.Key}}}", variable.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    public static IReadOnlyList<string> ReadArguments(ActionDefinition action, ActionContext context)
    {
        return action.Parameters
            .Where(parameter => parameter.Key.StartsWith("arg.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(parameter => int.TryParse(parameter.Key.AsSpan(4), out var index) ? index : int.MaxValue)
            .Select(parameter => Expand(parameter.Value, context))
            .ToArray();
    }
}
