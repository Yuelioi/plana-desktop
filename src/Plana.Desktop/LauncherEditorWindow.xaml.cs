using System.IO;
using System.Windows;
using Plana.Desktop.Localization;
using Plana.Core.Settings;

namespace Plana.Desktop;

public partial class LauncherEditorWindow : Window
{
    internal ProjectLauncherSettings? Result { get; private set; }

    internal LauncherEditorWindow() => InitializeComponent();

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var name = LauncherNameTextBox.Text.Trim();
        var folder = LauncherFolderTextBox.Text.Trim();
        var executable = LauncherExecutableTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(executable))
        {
            EditorErrorText.Text = LocalizationCatalog.Text("LauncherRequiredError");
            return;
        }
        if (!Directory.Exists(folder))
        {
            EditorErrorText.Text = LocalizationCatalog.Text("LauncherFolderMissingError", folder);
            return;
        }

        Result = new ProjectLauncherSettings
        {
            Name = name,
            Folder = Path.GetFullPath(folder),
            Executable = executable,
            Arguments = LauncherArgumentsTextBox.Text
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
        };
        DialogResult = true;
    }
}
