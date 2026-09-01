// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Win32;
using Microsoft.UI.Xaml.Controls;
using Plana.Core.Actions;
using Plana.Core.Characters;
using Plana_ControlCenter.Services;
using Windows.Storage.Pickers;
using System.Text.Json;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Plana_ControlCenter.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;
    private CharacterOption[] _characterOptions = [];
    private readonly string _charactersDirectory = Path.Combine(App.DataDirectory, "characters");
    private readonly ExtensionImportService _importService = new(App.DataDirectory);

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TopmostToggle.IsOn = App.Settings.AlwaysOnTop;
        StartupToggle.IsOn = App.Settings.StartWithWindows;
        ScaleSlider.Value = App.Settings.Scale;
        ScaleSlider.Header = $"{App.Settings.Scale:P0}";
        LanguagePicker.SelectedIndex = App.IsChinese ? 1 : 0;
        AiProviderPicker.SelectedIndex = App.Settings.AiProvider.Equals("api", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        AiModelInput.Text = App.Settings.AiModel;
        AiApiBaseUrlInput.Text = App.Settings.AiApiBaseUrl;
        AiApiKeyVariableInput.Text = App.Settings.AiApiKeyEnvironmentVariable;
        await LoadCharactersAsync();
        await LoadInteractionOptionsAsync();
        ApplyLanguage();
        _loading = false;
    }

    private async Task LoadCharactersAsync()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "CharacterPacks");
        var catalog = await new CharacterPackLoader().LoadCatalogAsync(bundled, _charactersDirectory);
        _characterOptions = catalog.ValidPacks
            .Where(pack => pack.Manifest.Id is CharacterPackLoader.BundledPlanaId or "community.arona")
            .Select(pack => new CharacterOption(
                pack.Manifest.Id,
                pack.Manifest.Name,
                pack.BuiltIn
                    ? (App.IsChinese ? "内置" : "Bundled")
                    : $"{pack.Manifest.Version} · {Path.GetFileName(pack.SourceDirectory)}"))
            .OrderByDescending(option => option.Id == CharacterPackLoader.BundledPlanaId)
            .ThenBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        CharacterPicker.ItemsSource = _characterOptions;
        CharacterPicker.SelectedValue = _characterOptions.Any(option => option.Id.Equals(App.Settings.SelectedCharacterPackId, StringComparison.OrdinalIgnoreCase))
            ? App.Settings.SelectedCharacterPackId
            : null;
        var errors = catalog.Discoveries.Where(item => !item.IsValid).Select(item => item.Error).Where(error => !string.IsNullOrWhiteSpace(error)).ToArray();
        if (errors.Length > 0)
        {
            CharacterStatus.Severity = InfoBarSeverity.Warning;
            CharacterStatus.Title = App.IsChinese ? "部分角色包无法加载" : "Some Character Packs could not load";
            CharacterStatus.Message = string.Join(Environment.NewLine, errors);
            CharacterStatus.IsOpen = true;
        }
    }

    private async void CharacterPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CharacterPicker.SelectedValue is not string id || App.Settings.SelectedCharacterPackId.Equals(id, StringComparison.OrdinalIgnoreCase)) return;
        App.Settings.SelectedCharacterPackId = id;
        await SaveWithStatusAsync();
    }

    private async void ImportCharacterButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await PickCharacterPackageAsync();
        if (path is null) return;
        await ShowCharacterImportResultAsync(await _importService.ImportCharacterPackageAsync(path));
    }

    private async void MoreCharactersButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "CharacterPackages");
        if (!Directory.Exists(directory)) directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "CharacterPackages"));
        var packages = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.planacharacter", SearchOption.AllDirectories)
                .Select(ReadCatalogCharacter)
                .Where(item => item is not null)
                .Cast<CatalogCharacter>()
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray()
            : [];
        if (packages.Length == 0)
        {
            CharacterStatus.Severity = InfoBarSeverity.Warning;
            CharacterStatus.Title = App.IsChinese ? "没有可用角色" : "No characters available";
            CharacterStatus.Message = App.IsChinese ? "当前安装包没有附带角色目录。" : "This installation does not include the character catalog.";
            CharacterStatus.IsOpen = true;
            return;
        }

        var search = new TextBox { PlaceholderText = App.IsChinese ? "搜索角色" : "Search characters" };
        var categories = new[]
        {
            new CatalogCategoryOption("pure", App.IsChinese ? "纯人物模型" : "Character only"),
            new CatalogCategoryOption("scene", App.IsChinese ? "有背景的动态场景" : "Animated scenes"),
            new CatalogCategoryOption("static", App.IsChinese ? "静态人物立绘" : "Static illustrations"),
        };
        var category = new ComboBox { ItemsSource = categories, DisplayMemberPath = nameof(CatalogCategoryOption.Name), SelectedValuePath = nameof(CatalogCategoryOption.Id), SelectedIndex = 1, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        var count = new TextBlock { Opacity = 0.72 };
        var list = new ListView { DisplayMemberPath = nameof(CatalogCharacter.DisplayName), SelectionMode = ListViewSelectionMode.Single, Height = 350 };
        var content = new StackPanel { Spacing = 10, MinWidth = 420 };
        content.Children.Add(search);
        content.Children.Add(category);
        content.Children.Add(count);
        content.Children.Add(list);
        var dialog = new ContentDialog
        {
            Title = App.IsChinese ? "更多角色" : "More characters",
            Content = content,
            PrimaryButtonText = App.IsChinese ? "下载并安装" : "Download and install",
            CloseButtonText = App.IsChinese ? "取消" : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        void ApplyCatalogFilter()
        {
            var query = search.Text.Trim();
            var selectedCategory = category.SelectedValue as string ?? "scene";
            var filtered = packages.Where(item => item.Category == selectedCategory &&
                (query.Length == 0 || item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || item.Id.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
            list.ItemsSource = filtered;
            count.Text = App.IsChinese ? $"{filtered.Length} 个可安装角色" : $"{filtered.Length} characters available";
        }
        search.TextChanged += (_, _) => ApplyCatalogFilter();
        category.SelectionChanged += (_, _) => ApplyCatalogFilter();
        ApplyCatalogFilter();
        list.SelectionChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = list.SelectedItem is CatalogCharacter;
            dialog.PrimaryButtonText = list.SelectedItem is CatalogCharacter { Installed: true }
                ? (App.IsChinese ? "使用角色" : "Use character")
                : (App.IsChinese ? "下载并使用" : "Download and use");
        };
        dialog.IsPrimaryButtonEnabled = false;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || list.SelectedItem is not CatalogCharacter selected) return;
        MoreCharactersButton.IsEnabled = false;
        try
        {
            if (!selected.Installed)
            {
                var result = await _importService.ImportCharacterPackageAsync(selected.PackagePath);
                await ShowCharacterImportResultAsync(result);
                if (!result.Succeeded) return;
            }
            App.Settings.SelectedCharacterPackId = selected.Id;
            await SaveWithStatusAsync();
            await LoadCharactersAsync();
        }
        finally { MoreCharactersButton.IsEnabled = true; }
    }

    private static CatalogCharacter? ReadCatalogCharacter(string packagePath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(packagePath));
            var manifest = document.RootElement.GetProperty("manifest");
            var id = manifest.GetProperty("id").GetString()!;
            var category = document.RootElement.TryGetProperty("category", out var categoryElement)
                ? categoryElement.GetString() ?? "static"
                : Path.GetFileName(Path.GetDirectoryName(packagePath)) ?? "static";
            return new CatalogCharacter(id, manifest.GetProperty("name").GetString()!, packagePath, category,
                Directory.Exists(Path.Combine(App.DataDirectory, "characters", id)));
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException) { return null; }
    }

    private async void ImportCharacterFolderButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is null) return;
        await ShowCharacterImportResultAsync(await _importService.ImportCharacterPackAsync(path));
    }

    private async Task ShowCharacterImportResultAsync(ExtensionImportResult result)
    {
        CharacterStatus.Severity = result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        CharacterStatus.Title = result.Succeeded
            ? (App.IsChinese ? "角色包已导入" : "Character Pack imported")
            : (App.IsChinese ? "无法导入角色包" : "Could not import Character Pack");
        CharacterStatus.Message = result.Message;
        CharacterStatus.IsOpen = true;
        if (result.Succeeded) await LoadCharactersAsync();
    }

    private void OpenCharactersButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Directory.CreateDirectory(_charactersDirectory);
        Process.Start(new ProcessStartInfo(_charactersDirectory) { UseShellExecute = true });
    }

    private async void ReloadCharactersButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await LoadCharactersAsync();

    private static async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    private static async Task<string?> PickCharacterPackageAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add(".planacharacter");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
        return (await picker.PickSingleFileAsync())?.Path;
    }

    private void AiProviderPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var api = (AiProviderPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "api";
        AiApiPanel.Visibility = api ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private async void SaveAiButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.Settings.AiProvider = (AiProviderPicker.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "codex";
        App.Settings.AiModel = AiModelInput.Text.Trim();
        App.Settings.AiApiBaseUrl = string.IsNullOrWhiteSpace(AiApiBaseUrlInput.Text) ? "https://api.openai.com/v1" : AiApiBaseUrlInput.Text.Trim();
        App.Settings.AiApiKeyEnvironmentVariable = string.IsNullOrWhiteSpace(AiApiKeyVariableInput.Text) ? "OPENAI_API_KEY" : AiApiKeyVariableInput.Text.Trim();
        await SaveWithStatusAsync();
    }

    private async Task LoadInteractionOptionsAsync()
    {
        var options = new List<InteractionActionOption>
        {
            new(string.Empty, App.IsChinese ? "无动作" : "No action"),
            new("builtin.companion.interact", App.IsChinese ? "随机互动动画" : "Random interaction animation"),
        };
        options.AddRange(App.Settings.UserActions.Select(action => new InteractionActionOption($"user.action.{action.Id}", action.Name)));
        options.AddRange(App.Settings.ProjectLaunchers.Select(project => new InteractionActionOption($"user.launcher.{project.Id}", project.Name)));
        var packs = await new ActionPackLoader().LoadDirectoryAsync(Path.Combine(App.DataDirectory, "packs"));
        options.AddRange(packs.ValidPacks.Where(pack => !App.Settings.DisabledActionPacks.Contains(pack.Id))
            .SelectMany(pack => pack.Actions.Select(action => new InteractionActionOption(action.Id, action.Label))));
        ClickActionPicker.ItemsSource = options;
        DoubleClickActionPicker.ItemsSource = options;
        ClickActionPicker.SelectedValue = App.Settings.InteractionBindings.GetValueOrDefault("click", "builtin.companion.interact");
        DoubleClickActionPicker.SelectedValue = App.Settings.InteractionBindings.GetValueOrDefault("doubleClick", string.Empty);
    }

    private async void InteractionPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || sender is not ComboBox picker || picker.Tag is not string interaction || picker.SelectedValue is not string actionId) return;
        if (string.IsNullOrEmpty(actionId)) App.Settings.InteractionBindings.Remove(interaction);
        else App.Settings.InteractionBindings[interaction] = actionId;
        await SaveWithStatusAsync();
    }

    private async void TopmostToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.AlwaysOnTop = TopmostToggle.IsOn;
        await SaveWithStatusAsync();
    }

    private async void StartupToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.StartWithWindows = StartupToggle.IsOn;
        using var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (StartupToggle.IsOn && FindCompanionExecutable() is { } executable)
            runKey.SetValue("Plana Desktop", $"\"{executable}\"");
        else
            runKey.DeleteValue("Plana Desktop", throwOnMissingValue: false);
        await SaveWithStatusAsync();
    }

    private async void ScaleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ScaleSlider is null) return;
        ScaleSlider.Header = $"{e.NewValue:P0}";
        if (_loading) return;
        App.Settings.Scale = e.NewValue;
        await SaveWithStatusAsync();
    }

    private async void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguagePicker.SelectedItem is not ComboBoxItem item) return;
        App.Settings.UiCulture = item.Tag?.ToString() ?? "en";
        await SaveWithStatusAsync();
        RestartNotice.Title = App.IsChinese ? "语言已保存" : "Language saved";
        RestartNotice.Message = App.IsChinese ? "重新打开此窗口后，全部界面会使用新语言。" : "Reopen this window to apply the language everywhere.";
        RestartNotice.IsOpen = true;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        if (!App.IsChinese) return;
        PageTitle.Text = "设置";
        BehaviorHeading.Text = "桌宠";
        TopmostTitle.Text = "保持在其他窗口上方";
        StartupTitle.Text = "登录时启动";
        ScaleTitle.Text = "桌宠缩放";
        CharacterHeading.Text = "角色";
        CharacterTitle.Text = "桌宠角色";
        CharacterPicker.PlaceholderText = "搜索角色";
        ImportCharacterLabel.Text = "导入角色包";
        MoreCharactersLabel.Text = "更多角色";
        ImportCharacterFolderLabel.Text = "导入文件夹";
        OpenCharactersLabel.Text = "打开角色文件夹";
        LanguageHeading.Text = "语言";
        LanguageTitle.Text = "显示语言";
        InteractionsHeading.Text = "交互";
        ClickTitle.Text = "单击";
        DoubleClickTitle.Text = "双击";
        AiHeading.Text = "AI 对话";
        AiProviderTitle.Text = "服务来源";
        if (AiProviderPicker.Items[0] is ComboBoxItem codex) codex.Content = "Codex CLI（订阅）";
        if (AiProviderPicker.Items[1] is ComboBoxItem api) api.Content = "OpenAI 兼容 API";
        AiModelInput.Header = "模型（Codex 可留空）";
        AiModelInput.PlaceholderText = "gpt-5.6-luna（快速默认）";
        AiApiBaseUrlInput.Header = "API Base URL";
        AiApiKeyVariableInput.Header = "API Key 环境变量";
        AiHelpText.Text = "Codex 使用本机已有登录；API 模式从指定环境变量读取 Key，不写入配置文件。";
        SaveAiLabel.Text = "保存 AI 设置";
    }

    private async Task<bool> SaveWithStatusAsync()
    {
        try
        {
            await App.SettingsStore.SaveAsync(App.Settings);
            SaveStatus.IsOpen = false;
            return true;
        }
        catch (Exception exception)
        {
            SaveStatus.Severity = InfoBarSeverity.Error;
            SaveStatus.Title = App.IsChinese ? "保存失败" : "Could not save";
            SaveStatus.Message = exception.Message;
            SaveStatus.IsOpen = true;
            return false;
        }
    }

    private static string? FindCompanionExecutable()
    {
        try
        {
            return Process.GetProcessesByName("Plana.Desktop")
                .Select(process => process.MainModule?.FileName)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record CatalogCharacter(string Id, string Name, string PackagePath, string Category, bool Installed)
{
    public string DisplayName => Installed ? $"{Name} · {(App.IsChinese ? "已安装" : "Installed")}" : Name;
}

internal sealed record CatalogCategoryOption(string Id, string Name);

public sealed record InteractionActionOption(string Id, string Name);
public sealed record CharacterOption(string Id, string Name, string Detail)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Detail) ? Name : $"{Name} · {Detail}";
}
