using System.Windows;
using Plana.Core.Actions;
using Plana.Desktop.Settings;

namespace Plana.Desktop.Actions;

internal sealed class DesktopCapabilityPolicy(DesktopSettings settings, Func<Task> saveSettings) : ICapabilityPolicy
{
    public async Task<bool> AuthorizeAsync(ActionPack pack, ActionDefinition action, IReadOnlySet<string> capabilities, CancellationToken cancellationToken)
    {
        if (action.RequiresConfirmation)
        {
            var confirmation = System.Windows.MessageBox.Show(
                $"Run '{action.Label}' from Action Pack '{pack.Name}'?",
                "Confirm Action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes) return false;
        }

        if (settings.CapabilityGrants.TryGetValue(pack.Id, out var granted) && capabilities.IsSubsetOf(granted)) return true;

        var capabilityList = string.Join(Environment.NewLine, capabilities.Select(capability => $"• {capability}"));
        var result = System.Windows.MessageBox.Show(
            $"Action Pack '{pack.Name}' wants to run '{action.Label}' using:\n\n{capabilityList}\n\nAllow these capabilities for this pack?",
            "Allow Action Pack",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return false;

        granted ??= [];
        granted.UnionWith(capabilities);
        settings.CapabilityGrants[pack.Id] = granted;
        await saveSettings();
        return true;
    }
}
