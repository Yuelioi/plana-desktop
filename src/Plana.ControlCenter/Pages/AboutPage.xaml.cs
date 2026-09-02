using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Plana.Core.Actions;
using Plana.Core.Plugins;
using Plana_ControlCenter.Services;
using Windows.Storage.Pickers;

namespace Plana_ControlCenter.Pages;

public sealed partial class AboutPage : Page
{
    private readonly string _packsDirectory = Path.Combine(App.DataDirectory, "packs");
    private readonly string _pluginsDirectory = Path.Combine(App.DataDirectory, "plugins");
    private bool _loading = true;
    private readonly ExtensionImportService _importService = new(App.DataDirectory);

    public ObservableCollection<ExtensionItem> Packs { get; } = [];
    public ObservableCollection<ExtensionItem> Plugins { get; } = [];

    public AboutPage()
    {
        InitializeComponent();
        Loaded += AboutPage_Loaded;
    }

    private async void AboutPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _loading = true;
        Directory.CreateDirectory(_packsDirectory);
        Directory.CreateDirectory(_pluginsDirectory);
        Packs.Clear();
        Plugins.Clear();

        var packResult = await new ActionPackLoader().LoadDirectoryAsync(_packsDirectory);
        foreach (var discovery in packResult.Discoveries)
        {
            var id = discovery.Pack?.Id ?? discovery.ManifestPath;
            Packs.Add(new ExtensionItem(
                id,
                discovery.DisplayName,
                discovery.Error ?? $"{discovery.Pack!.Publisher} · {discovery.Pack.Version}",
                !App.Settings.DisabledActionPacks.Contains(id),
                discovery.IsValid));
        }

        var diagnostics = await new PluginManifestLoader().LoadDirectoryAsync(_pluginsDirectory);
        foreach (var diagnostic in diagnostics)
        {
            var id = diagnostic.Manifest?.Id ?? diagnostic.ManifestPath;
            Plugins.Add(new ExtensionItem(
                id,
                diagnostic.Manifest?.Id ?? Path.GetFileName(Path.GetDirectoryName(diagnostic.ManifestPath)) ?? "Plugin",
                diagnostic.Error ?? $"{diagnostic.Manifest!.Publisher} · {diagnostic.Manifest.Version}",
                !App.Settings.DisabledPlugins.Contains(id),
                diagnostic.IsValid));
        }
        PacksEmpty.Visibility = Packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PluginsEmpty.Visibility = Plugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;
    }

    private async void ReloadButton_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
    private void OpenPacksButton_Click(object sender, RoutedEventArgs e) => OpenFolder(_packsDirectory);
    private void OpenPluginsButton_Click(object sender, RoutedEventArgs e) => OpenFolder(_pluginsDirectory);

    private async void ImportPackButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is null) return;
        await ShowImportResultAsync(await _importService.ImportPackAsync(path));
    }

    private async void ImportPluginButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is null) return;
        await ShowImportResultAsync(await _importService.ImportPluginAsync(path));
    }

    private static async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    private async Task ShowImportResultAsync(ExtensionImportResult result)
    {
        ImportStatus.Title = result.Succeeded ? (App.IsChinese ? "导入完成" : "Import complete") : (App.IsChinese ? "无法导入" : "Could not import");
        ImportStatus.Message = result.Message;
        ImportStatus.Severity = result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ImportStatus.IsOpen = true;
        if (result.Succeeded) await ReloadAsync();
    }

    private async void PackToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not ToggleSwitch { Tag: ExtensionItem item } toggle) return;
        SetDisabled(App.Settings.DisabledActionPacks, item, toggle.IsOn);
        await App.SettingsStore.SaveAsync(App.Settings);
    }

    private async void PluginToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not ToggleSwitch { Tag: ExtensionItem item } toggle) return;
        SetDisabled(App.Settings.DisabledPlugins, item, toggle.IsOn);
        await App.SettingsStore.SaveAsync(App.Settings);
    }

    private static void SetDisabled(HashSet<string> disabled, ExtensionItem item, bool isOn)
    {
        item.Enabled = isOn && item.Valid;
        if (item.Enabled) disabled.Remove(item.Id); else disabled.Add(item.Id);
    }

    private static void OpenFolder(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private void ApplyLanguage()
    {
        if (!App.IsChinese) return;
        PageTitle.Text = "扩展";
        ReloadLabel.Text = "重新加载";
        ImportPackLabel.Text = "导入 Action Pack";
        ImportPluginLabel.Text = "导入插件";
        OpenPacksLabel.Text = "打开 Pack 文件夹";
        OpenPluginsLabel.Text = "打开插件文件夹";
        PacksHeading.Text = "Action Pack";
        PacksEmpty.Text = "尚未安装 Action Pack。";
        PluginsHeading.Text = "插件";
        PluginsEmpty.Text = "尚未安装插件。";
    }
}

public sealed class ExtensionItem(string id, string name, string detail, bool enabled, bool valid)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string Detail { get; set; } = detail;
    public bool Enabled { get; set; } = enabled && valid;
    public bool Valid { get; set; } = valid;
}
