namespace Plana.Core.Plugins;

public static class PluginContributionPolicy
{
    public static PluginContributionsPayload Validate(PluginManifest manifest, PluginContributionsPayload payload)
    {
        var actions = Validate(manifest, payload.Actions ?? []);
        var actionIds = actions.Select(action => action.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidateLinks(payload.Tools ?? [], item => item.Id, item => item.Label, item => item.ActionId, "Tool", actionIds);
        ValidateLinks(payload.ContextCommands ?? [], item => item.Id, item => item.Label, item => item.ActionId, "Context command", actionIds);
        ValidateLinks(payload.ContentProviders ?? [], item => item.Id, item => item.Label, item => item.ActionId, "Content provider", actionIds);
        return payload with { Actions = actions.ToArray(), Tools = payload.Tools ?? [], ContextCommands = payload.ContextCommands ?? [], ContentProviders = payload.ContentProviders ?? [] };
    }

    public static IReadOnlyList<PluginActionContribution> Validate(
        PluginManifest manifest,
        IEnumerable<PluginActionContribution> contributions)
    {
        var accepted = new List<PluginActionContribution>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in contributions)
        {
            if (string.IsNullOrWhiteSpace(action.Id) || action.Id.Any(character =>
                    !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
            {
                throw new PluginProtocolException("A contributed Action has an invalid ID.");
            }
            if (string.IsNullOrWhiteSpace(action.Label))
            {
                throw new PluginProtocolException($"Contributed Action '{action.Id}' has no label.");
            }
            if (!ids.Add(action.Id))
            {
                throw new PluginProtocolException($"Contributed Action ID '{action.Id}' is duplicated.");
            }
            var capabilities = action.Capabilities ?? [];
            var undeclared = capabilities.Where(capability => !manifest.Capabilities.Contains(capability)).ToArray();
            if (undeclared.Length > 0)
            {
                throw new PluginProtocolException(
                    $"Contributed Action '{action.Id}' requests capabilities absent from the Plugin manifest: {string.Join(", ", undeclared)}.");
            }
            accepted.Add(action with { Capabilities = capabilities.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() });
        }
        return accepted;
    }

    private static void ValidateLinks<T>(IEnumerable<T> items, Func<T, string> id, Func<T, string> label, Func<T, string> actionId, string kind, IReadOnlySet<string> actionIds)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(id(item)) || string.IsNullOrWhiteSpace(label(item)) || !ids.Add(id(item)))
                throw new PluginProtocolException($"A contributed {kind} has an invalid or duplicate ID/label.");
            if (!actionIds.Contains(actionId(item)))
                throw new PluginProtocolException($"Contributed {kind} '{id(item)}' references unknown Action '{actionId(item)}'.");
        }
    }
}
