using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Plana.Core.Companion;
using Plana_ControlCenter.Services;

namespace Plana_ControlCenter.Pages;

public sealed partial class ChatPage : Page
{
    public ChatPage()
    {
        InitializeComponent();
        if (App.IsChinese)
        {
            PageTitle.Text = "和普拉娜对话";
            PageDescription.Text = "使用设置中选择的 AI 服务。";
            PromptInput.PlaceholderText = "想和普拉娜说什么？";
            SendLabel.Text = "发送";
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendAsync();

    private async void PromptInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter || string.IsNullOrWhiteSpace(PromptInput.Text)) return;
        e.Handled = true;
        await SendAsync();
    }

    private async Task SendAsync()
    {
        var prompt = PromptInput.Text.Trim();
        if (prompt.Length == 0 || !SendButton.IsEnabled) return;
        SendButton.IsEnabled = false;
        ChatStatus.IsOpen = false;
        ResponseText.Text = App.IsChinese ? "正在思考…" : "Thinking…";
        try
        {
            await TryPerformAsync(new CharacterPerformanceIntent(IsSpeaking: true));
            var response = await AiChatService.SendAsync(App.Settings, prompt, CancellationToken.None);
            ResponseText.Text = response;
            PromptInput.Text = string.Empty;
            await TryPerformAsync(new CharacterPerformanceIntent(CharacterEmotion.Happy));
        }
        catch (Exception exception)
        {
            ResponseText.Text = string.Empty;
            ChatStatus.Title = App.IsChinese ? "对话失败" : "Chat failed";
            ChatStatus.Message = exception.Message;
            ChatStatus.Severity = InfoBarSeverity.Error;
            ChatStatus.IsOpen = true;
            await TryPerformAsync(new CharacterPerformanceIntent(CharacterEmotion.Worried));
        }
        finally
        {
            SendButton.IsEnabled = true;
            PromptInput.Focus(FocusState.Programmatic);
        }
    }

    private static async Task TryPerformAsync(CharacterPerformanceIntent intent)
    {
        try { await CompanionControlClient.PerformAsync(intent); } catch (Exception) { }
    }
}
