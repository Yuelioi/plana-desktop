using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Plana.Core.Settings;

namespace Plana.Companion.Native;

internal static class AiChatService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

    public static Task<string> SendAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken) =>
        settings.AiProvider.Equals("api", StringComparison.OrdinalIgnoreCase)
            ? SendApiAsync(settings, prompt, cancellationToken)
            : SendCodexAsync(settings, prompt, cancellationToken);

    private static async Task<string> SendCodexAsync(DesktopSettings settings, string prompt, CancellationToken cancellationToken)
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
            if (!string.IsNullOrWhiteSpace(settings.AiModel))
            {
                info.ArgumentList.Add("--model");
                info.ArgumentList.Add(settings.AiModel);
            }
            info.ArgumentList.Add(prompt);
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
            messages = new[] { new { role = "user", content = prompt } },
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
