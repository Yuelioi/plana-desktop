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
        你可以直接管理桌宠的动作和动作组。老师要求创建、修改或删除配置时，不要让老师去设置页，也不要只给步骤；直接执行。
        在给老师看的短句后追加机器可读块：<plana-config>{"operations":[...]}</plana-config>。可用操作：
        每个操作必须使用 type 字段。upsert_action：name、description、kind、target，可选 arguments、script。kind 只能是 url.open、file.open、folder.open、process.launch、command.run、script.run。
        delete_action：name。upsert_group：name、actions（动作名称数组）。delete_group：name。
        同一请求需要新建动作再加入组时，先输出 upsert_action，再输出 upsert_group。不要用 Markdown 代码块包裹配置。
        """;

    static AiChatService() => CodexClient = new CodexAppServerClient(PersonaPrompt);

    public static async Task<AiConfigurationResult> SendAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken)
    {
        var configuredPrompt = AiConfigurationEditor.BuildPrompt(settings, prompt);
        var response = await SendRawAsync(settings, configuredPrompt, cancellationToken);
        try { return AiConfigurationEditor.Apply(settings, response); }
        catch (Exception exception) when (IsIncompleteConfiguration(exception))
        {
            var repairPrompt = "你上一次返回的 <plana-config> JSON 被截断或不是有效 JSON。请重新完整执行老师刚才的要求，只返回简短结果和一份完整、可解析的 <plana-config> 配置块。";
            response = await SendRawAsync(settings, repairPrompt, cancellationToken);
            try { return AiConfigurationEditor.Apply(settings, response); }
            catch (Exception retryException) when (IsIncompleteConfiguration(retryException))
            {
                throw new InvalidOperationException("动作配置生成失败，请再说一次。", retryException);
            }
        }
    }

    private static bool IsIncompleteConfiguration(Exception exception) =>
        exception is JsonException || exception is InvalidDataException data && data.Message.Contains("不完整", StringComparison.Ordinal);

    private static Task<string> SendRawAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken) =>
        settings.AiProvider.Equals("api", StringComparison.OrdinalIgnoreCase)
            ? SendApiAsync(settings, prompt, cancellationToken)
            : SendCodexAsync(settings, prompt, cancellationToken);

    public static async Task WarmUpAsync(DesktopSettings settings)
    {
        if (!settings.AiProvider.Equals("codex", StringComparison.OrdinalIgnoreCase)) return;
        try { await CodexClient.WarmUpAsync(EffectiveCodexModel(settings), CancellationToken.None); }
        catch { /* Warm-up is opportunistic; the real request retries and surfaces its error. */ }
    }

    private static Task<string> SendCodexAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken) =>
        CodexClient.SendAsync(EffectiveCodexModel(settings), prompt, cancellationToken);

    private static string EffectiveCodexModel(DesktopSettings settings) =>
        string.IsNullOrWhiteSpace(settings.AiModel) ? "gpt-5.6-luna" : settings.AiModel.Trim();

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
