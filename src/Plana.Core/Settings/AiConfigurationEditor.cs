using System.Text.Json;
using Plana.Core.Actions;

namespace Plana.Core.Settings;

public sealed record AiConfigurationResult(string Message, bool Changed, IReadOnlyList<string> Changes);

public static class AiConfigurationEditor
{
    private const string StartTag = "<plana-config>";
    private const string EndTag = "</plana-config>";
    private static readonly HashSet<string> SupportedKinds =
    [
        ActionKinds.OpenUrl, ActionKinds.OpenFile, ActionKinds.OpenFolder,
        ActionKinds.LaunchProcess, ActionKinds.RunCommand, ActionKinds.RunScript,
    ];

    public static string BuildPrompt(DesktopSettings settings, string userPrompt)
    {
        var inventory = JsonSerializer.Serialize(new
        {
            actions = settings.UserActions.Select(action => new { action.Name, action.Description, action.Kind, action.Parameters }),
            groups = settings.ToolGroups.Select(group => new
            {
                group.Name,
                actions = group.ActionIds.Select(id => settings.UserActions.FirstOrDefault(action => $"user.action.{action.Id}".Equals(id, StringComparison.OrdinalIgnoreCase))?.Name ?? id),
            }),
        });
        return $"当前动作配置：{inventory}\n老师的要求：{userPrompt}";
    }

    public static AiConfigurationResult Apply(DesktopSettings settings, string response)
    {
        var start = response.IndexOf(StartTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return new(response.Trim(), false, []);
        var contentStart = start + StartTag.Length;
        var end = response.IndexOf(EndTag, contentStart, StringComparison.OrdinalIgnoreCase);
        var hasClosingTag = end >= 0;
        if (!hasClosingTag)
        {
            var lastBrace = response.LastIndexOf('}');
            if (lastBrace < contentStart) throw new InvalidDataException("AI 返回了不完整的动作配置。");
            end = lastBrace + 1;
        }

        var changes = new List<string>();
        using var document = JsonDocument.Parse(response[contentStart..end]);
        if (document.RootElement.TryGetProperty("operations", out var operations))
            foreach (var operation in operations.EnumerateArray()) ApplyOperation(settings, operation, changes);

        var suffixStart = hasClosingTag ? end + EndTag.Length : end;
        var message = (response[..start] + response[suffixStart..]).Trim();
        if (string.IsNullOrWhiteSpace(message) && changes.Count > 0) message = string.Join("；", changes) + "。";
        return new(message, changes.Count > 0, changes);
    }

    private static void ApplyOperation(DesktopSettings settings, JsonElement operation, List<string> changes)
    {
        var type = operation.TryGetProperty("type", out var typeValue) ? typeValue.GetString()
            : operation.TryGetProperty("op", out var opValue) ? opValue.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(type)) throw new InvalidDataException("配置操作缺少 type。");
        var name = Required(operation, "name").Trim();
        if (type.Equals("upsert_action", StringComparison.OrdinalIgnoreCase))
        {
            var kind = Required(operation, "kind");
            if (!SupportedKinds.Contains(kind)) throw new InvalidDataException($"不支持的动作类型：{kind}");
            var target = Required(operation, "target").Trim();
            var action = settings.UserActions.FirstOrDefault(item => item.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (action is null) { action = new UserActionSettings(); settings.UserActions.Add(action); }
            action.Name = name;
            action.Description = Optional(operation, "description");
            action.Kind = kind;
            action.Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [TargetKey(kind)] = target,
            };
            if (operation.TryGetProperty("script", out var script) && !string.IsNullOrWhiteSpace(script.GetString())) action.Parameters["script"] = script.GetString()!;
            if (operation.TryGetProperty("arguments", out var arguments))
            {
                var index = 0;
                foreach (var argument in arguments.EnumerateArray()) action.Parameters[$"arg.{index++}"] = argument.GetString() ?? string.Empty;
            }
            changes.Add($"已保存动作“{name}”");
            return;
        }
        if (type.Equals("delete_action", StringComparison.OrdinalIgnoreCase))
        {
            var action = settings.UserActions.FirstOrDefault(item => item.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (action is null) return;
            var id = $"user.action.{action.Id}";
            settings.UserActions.Remove(action);
            foreach (var group in settings.ToolGroups) group.ActionIds.RemoveAll(item => item.Equals(id, StringComparison.OrdinalIgnoreCase));
            settings.PinnedCompanionActionIds.RemoveAll(item => item.Equals(id, StringComparison.OrdinalIgnoreCase));
            changes.Add($"已删除动作“{name}”");
            return;
        }
        if (type.Equals("upsert_group", StringComparison.OrdinalIgnoreCase))
        {
            var group = settings.ToolGroups.FirstOrDefault(item => item.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (group is null) { group = new ToolGroupSettings(); settings.ToolGroups.Add(group); }
            group.Name = name;
            group.ActionIds = operation.TryGetProperty("actions", out var actions)
                ? actions.EnumerateArray().Select(item => ResolveAction(settings, item.GetString() ?? string.Empty)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : [];
            settings.SelectedToolGroupId ??= group.Id;
            changes.Add($"已保存动作组“{name}”");
            return;
        }
        if (type.Equals("delete_group", StringComparison.OrdinalIgnoreCase))
        {
            var group = settings.ToolGroups.FirstOrDefault(item => item.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (group is null) return;
            settings.ToolGroups.Remove(group);
            if (settings.SelectedToolGroupId == group.Id) settings.SelectedToolGroupId = settings.ToolGroups.FirstOrDefault()?.Id;
            changes.Add($"已删除动作组“{name}”");
            return;
        }
        throw new InvalidDataException($"不支持的配置操作：{type}");
    }

    private static string ResolveAction(DesktopSettings settings, string nameOrId)
    {
        var action = settings.UserActions.FirstOrDefault(item => item.Name.Equals(nameOrId, StringComparison.CurrentCultureIgnoreCase));
        if (action is not null) return $"user.action.{action.Id}";
        if (nameOrId.StartsWith("user.", StringComparison.OrdinalIgnoreCase)) return nameOrId;
        throw new InvalidDataException($"动作组引用了不存在的动作：{nameOrId}");
    }

    private static string TargetKey(string kind) => kind switch
    {
        ActionKinds.OpenUrl => "url",
        ActionKinds.OpenFile or ActionKinds.OpenFolder => "path",
        ActionKinds.RunCommand => "command",
        ActionKinds.RunScript => "interpreter",
        _ => "executable",
    };

    private static string Required(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"配置操作缺少 {property}。");

    private static string Optional(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
