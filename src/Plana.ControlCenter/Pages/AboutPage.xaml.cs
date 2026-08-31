using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Plana.Core.Actions;
using Plana.Core.Plugins;

namespace Plana_ControlCenter.Pages;

public sealed partial class AboutPage : Page
{
    private readonly string _packsDirectory = Path.Combine(App.DataDirectory, "packs");
    private readonly string _pluginsDirectory = Path.Combine(App.DataDirectory, "plugins");
    private bool _loading = true;

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

    private async void PackToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading || (sender as FrameworkElement)?.Tag is not ExtensionItem item) return;
        SetDisabled(App.Settings.DisabledActionPacks, item);
        await App.SettingsStore.SaveAsync(App.Settings);
    }

    private async void PluginToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading || (sender as FrameworkElement)?.Tag is not ExtensionItem item) return;
        SetDisabled(App.Settings.DisabledPlugins, item);
        await App.SettingsStore.SaveAsync(App.Settings);
    }

    private static void SetDisabled(HashSet<string> disabled, ExtensionItem item)
    {
        if (item.Enabled && item.Valid) disabled.Remove(item.Id); else disabled.Add(item.Id);
    }

    private static void OpenFolder(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private void ApplyLanguage()
    {
        if (!App.IsChinese) return;
        PageTitle.Text = "扩展";
        PageDescription.Text = "管理声明式 Action Pack 与进程外插件。";
        ReloadLabel.Text = "重新加载";
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
