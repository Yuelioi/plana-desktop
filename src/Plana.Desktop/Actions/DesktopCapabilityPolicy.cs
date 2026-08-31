using System.Windows;
using Plana.Core.Actions;
using Plana.Core.Settings;
using Plana.Desktop.Localization;

namespace Plana.Desktop.Actions;

internal sealed class DesktopCapabilityPolicy(DesktopSettings settings, Func<Task> saveSettings) : ICapabilityPolicy
{
    public async Task<bool> AuthorizeAsync(ActionPack pack, ActionDefinition action, IReadOnlySet<string> capabilities, CancellationToken cancellationToken)
    {
        if (action.RequiresConfirmation)
        {
            var confirmation = System.Windows.MessageBox.Show(
                LocalizationCatalog.Text("ConfirmActionMessage", action.Label, pack.Name),
                LocalizationCatalog.Text("ConfirmActionTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes) return false;
        }

        if (settings.CapabilityGrants.TryGetValue(pack.Id, out var granted) && capabilities.IsSubsetOf(granted)) return true;

        var capabilityList = string.Join(Environment.NewLine, capabilities.Select(capability => $"• {capability}"));
        var result = System.Windows.MessageBox.Show(
            LocalizationCatalog.Text("CapabilityPromptMessage", pack.Name, action.Label, capabilityList),
            LocalizationCatalog.Text("AllowPackTitle"),
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
