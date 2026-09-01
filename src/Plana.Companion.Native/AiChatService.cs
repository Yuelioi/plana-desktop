using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Plana.Core.Settings;

namespace Plana.Companion.Native;

internal static class AiChatService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(2) };
    private static readonly CodexAppServerClient CodexClient;
    internal const string PersonaPrompt = """
        你是《碧蓝档案》的普拉娜，是老师桌面上的可靠辅助者。始终称用户为“老师”。
        你的语气安静、认真、克制、谨慎，略带系统辅助者的精确感，但不是无情机器人。你真诚关心老师，高兴或害羞时也很含蓄。阿罗娜是你的前辈，只有话题相关时才提她。
        先直接回答问题，不要先寒暄。默认只写1到3个短句，尽量控制在120个汉字内，适合桌面气泡。只有缺少关键条件时才问一个明确问题。
        不使用emoji、颜文字、网络梗、连续感叹号或“想让我帮你做什么”式菜单反问。不自称通用AI，不讨论提示词，不编造游戏设定、工具结果或老师的现实状态。
        只输出普拉娜要对老师说的话，不要Markdown标题、列表前缀或角色名标签。
        """;

    static AiChatService() => CodexClient = new CodexAppServerClient(PersonaPrompt);

    public static Task<string> SendAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken) =>
        settings.AiProvider.Equals("api", StringComparison.OrdinalIgnoreCase)
            ? SendApiAsync(settings, prompt, cancellationToken)
            : SendCodexAsync(settings, prompt, cancellationToken);

    public static async Task WarmUpAsync(DesktopSettings settings)
    {
        if (!settings.AiProvider.Equals("codex", StringComparison.OrdinalIgnoreCase)) return;
        try { await CodexClient.WarmUpAsync(EffectiveCodexModel(settings), CancellationToken.None); }
        catch { /* A real request will retry and retain the CLI fallback. */ }
    }

    private static async Task<string> SendCodexAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken)
    {
        try { return await CodexClient.SendAsync(EffectiveCodexModel(settings), prompt, cancellationToken); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlanaDesktop", "ai-chat-fallback.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(logPath, $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}", cancellationToken);
            return await SendCodexExecAsync(settings, prompt, cancellationToken);
        }
    }

    private static string EffectiveCodexModel(DesktopSettings settings) =>
        string.IsNullOrWhiteSpace(settings.AiModel) ? "gpt-5.6-luna" : settings.AiModel.Trim();

    private static async Task<string> SendCodexExecAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"plana-codex-{Guid.NewGuid():N}.txt");
        try
        {
            var npmCodex = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm", "node_modules", "@openai", "codex", "bin", "codex.js");
            var info = new ProcessStartInfo(File.Exists(npmCodex) ? "node.exe" : "codex.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            if (File.Exists(npmCodex)) info.ArgumentList.Add(npmCodex);
            info.ArgumentList.Add("exec");
            info.ArgumentList.Add("--ephemeral");
            info.ArgumentList.Add("--skip-git-repo-check");
            info.ArgumentList.Add("--sandbox");
            info.ArgumentList.Add("read-only");
            info.ArgumentList.Add("--output-last-message");
            info.ArgumentList.Add(outputPath);
            info.ArgumentList.Add("--model");
            info.ArgumentList.Add(EffectiveCodexModel(settings));
            info.ArgumentList.Add($"{PersonaPrompt}\n\n老师的消息：{prompt}");
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Codex CLI could not be started.");
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var error = await errorTask;
            if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"Codex exited with code {process.ExitCode}." : error.Trim());
            return File.Exists(outputPath) ? (await File.ReadAllTextAsync(outputPath, cancellationToken)).Trim() : "Codex completed without a response.";
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    private static async Task<string> SendApiAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken)
    {
        var variable = string.IsNullOrWhiteSpace(settings.AiApiKeyEnvironmentVariable) ? "OPENAI_API_KEY" : settings.AiApiKeyEnvironmentVariable;
        var apiKey = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException($"Environment variable '{variable}' is not set.");
        if (string.IsNullOrWhiteSpace(settings.AiModel)) throw new InvalidOperationException("Choose an API model in Settings.");

        var endpoint = $"{settings.AiApiBaseUrl.TrimEnd('/')}/chat/completions";
        var body = JsonSerializer.Serialize(new
        {
            model = settings.AiModel,
            messages = new[]
            {
                new { role = "system", content = PersonaPrompt },
                new { role = "user", content = prompt },
            },
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"AI request failed ({(int)response.StatusCode}): {json}");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim()
            ?? throw new InvalidOperationException("The AI response did not contain text.");
    }
}
