namespace Plana.Core.Plugins;

public static class PluginContributionPolicy
{
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
}
