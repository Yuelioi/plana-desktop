using System.IO;
using System.Windows;
using System.Windows.Controls;
using Plana.Core.Actions;
using Plana.Desktop.Localization;
using Plana.Core.Settings;

namespace Plana.Desktop;

public partial class ActionEditorWindow : Window
{
    private static readonly ActionKindOption[] Kinds =
    [
        new(ActionKinds.OpenUrl, "ActionTypeOpenWebsite"),
        new(ActionKinds.OpenFile, "ActionTypeOpenFile"),
        new(ActionKinds.OpenFolder, "ActionTypeOpenFolder"),
        new(ActionKinds.LaunchProcess, "ActionTypeLaunchApplication"),
        new(ActionKinds.RunCommand, "ActionTypeRunCommand"),
        new(ActionKinds.RunScript, "ActionTypeRunScript"),
    ];

    internal UserActionSettings? Result { get; private set; }

    internal ActionEditorWindow()
    {
        InitializeComponent();
        ActionKindComboBox.ItemsSource = Kinds.Select(kind => new ActionKindOption(kind.Kind, LocalizationCatalog.Text(kind.Label))).ToArray();
        ActionKindComboBox.SelectedIndex = 0;
    }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActionKindComboBox.SelectedValue is not string kind) return;
        var isScript = kind == ActionKinds.RunScript;
        SecondaryTargetPanel.Visibility = isScript ? Visibility.Visible : Visibility.Collapsed;
        PrimaryTargetLabel.Text = LocalizationCatalog.Text(kind switch
        {
            ActionKinds.OpenUrl => "WebsiteUrlLabel",
            ActionKinds.OpenFile => "FilePathLabel",
            ActionKinds.OpenFolder => "FolderPathLabel",
            ActionKinds.RunScript => "InterpreterLabel",
            _ => "ExecutableLabel",
        });
        TargetHelpText.Text = LocalizationCatalog.Text(kind switch
        {
            ActionKinds.OpenUrl => "WebsiteUrlHelp",
            ActionKinds.OpenFile => "FilePathHelp",
            ActionKinds.OpenFolder => "FolderPathHelp",
            ActionKinds.RunScript => "InterpreterHelp",
            _ => "ExecutableHelp",
        });
        var highRisk = kind is ActionKinds.RunCommand or ActionKinds.RunScript;
        RequiresConfirmationCheckBox.IsChecked = highRisk || RequiresConfirmationCheckBox.IsChecked == true;
        RequiresConfirmationCheckBox.IsEnabled = !highRisk;
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var name = ActionNameTextBox.Text.Trim();
        var primary = PrimaryTargetTextBox.Text.Trim();
        var kind = ActionKindComboBox.SelectedValue as string;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(primary) || string.IsNullOrWhiteSpace(kind))
        {
            SetError(LocalizationCatalog.Text("ActionRequiredError"));
            return;
        }

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        switch (kind)
        {
            case ActionKinds.OpenUrl:
                if (!Uri.TryCreate(primary, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    SetError(LocalizationCatalog.Text("ActionUrlInvalidError"));
                    return;
                }
                parameters["url"] = primary;
                break;
            case ActionKinds.OpenFile:
                if (!Path.IsPathFullyQualified(primary) || !File.Exists(primary))
                {
                    SetError(LocalizationCatalog.Text("ActionFileMissingError", primary));
                    return;
                }
                if (!ActionFilePolicy.CanOpenWithFileCapability(primary))
                {
                    SetError(LocalizationCatalog.Text("ActionFileExecutableError"));
                    return;
                }
                parameters["path"] = Path.GetFullPath(primary);
                break;
            case ActionKinds.OpenFolder:
                if (!Path.IsPathFullyQualified(primary) || !Directory.Exists(primary))
                {
                    SetError(LocalizationCatalog.Text("ActionFolderMissingError", primary));
                    return;
                }
                parameters["path"] = Path.GetFullPath(primary);
                break;
            case ActionKinds.RunScript:
                var script = SecondaryTargetTextBox.Text.Trim();
                if (!Path.IsPathFullyQualified(script) || !File.Exists(script))
                {
                    SetError(LocalizationCatalog.Text("ActionScriptMissingError", script));
                    return;
                }
                parameters["interpreter"] = primary;
                parameters["script"] = Path.GetFullPath(script);
                break;
            default:
                parameters["executable"] = primary;
                break;
        }

        var arguments = ActionArgumentsTextBox.Text
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < arguments.Length; index++) parameters[$"arg.{index}"] = arguments[index];

        Result = new UserActionSettings
        {
            Name = name,
            Kind = kind,
            Parameters = parameters,
            RequiresConfirmation = RequiresConfirmationCheckBox.IsChecked == true,
        };
        DialogResult = true;
    }

    private void SetError(string message) => EditorErrorText.Text = message;

    private sealed record ActionKindOption(string Kind, string Label);
}
