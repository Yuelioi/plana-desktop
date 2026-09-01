using Plana.Core.Actions;
using Plana.Core.Settings;

namespace Plana.Core.Tests;

public sealed class AiConfigurationEditorTests
{
    [Fact]
    public void Apply_CreatesActionAndGroupInOneResponse()
    {
        var settings = new DesktopSettings();
        var response = "已为老师配置好了。<plana-config>{\"operations\":[" +
            "{\"type\":\"upsert_action\",\"name\":\"打开哔哩哔哩\",\"description\":\"打开网站\",\"kind\":\"url.open\",\"target\":\"https://www.bilibili.com/\"}," +
            "{\"type\":\"upsert_group\",\"name\":\"娱乐\",\"actions\":[\"打开哔哩哔哩\"]}]}" +
            "</plana-config>";

        var result = AiConfigurationEditor.Apply(settings, response);

        Assert.True(result.Changed);
        Assert.Equal("已为老师配置好了。", result.Message);
        var action = Assert.Single(settings.UserActions);
        Assert.Equal(ActionKinds.OpenUrl, action.Kind);
        Assert.Equal("https://www.bilibili.com/", action.Parameters["url"]);
        Assert.Equal([$"user.action.{action.Id}"], Assert.Single(settings.ToolGroups).ActionIds);
    }

    [Fact]
    public void Apply_UpdatesExistingActionWithoutDuplicatingIt()
    {
        var settings = new DesktopSettings
        {
            UserActions = [new UserActionSettings { Name = "网站", Kind = ActionKinds.OpenUrl, Parameters = new() { ["url"] = "https://old.example" } }],
        };

        var result = AiConfigurationEditor.Apply(settings,
            "完成。<plana-config>{\"operations\":[{\"type\":\"upsert_action\",\"name\":\"网站\",\"kind\":\"url.open\",\"target\":\"https://new.example\"}]}</plana-config>");

        Assert.True(result.Changed);
        Assert.Single(settings.UserActions);
        Assert.Equal("https://new.example", settings.UserActions[0].Parameters["url"]);
    }

    [Fact]
    public void Apply_AcceptsCompleteJsonWhenClosingTagIsMissing()
    {
        var settings = new DesktopSettings();

        var result = AiConfigurationEditor.Apply(settings,
            "已经创建。<plana-config>{\"operations\":[{\"type\":\"upsert_action\",\"name\":\"网站\",\"kind\":\"url.open\",\"target\":\"https://example.com\"}]}");

        Assert.True(result.Changed);
        Assert.Equal("已经创建。", result.Message);
        Assert.Equal("https://example.com", Assert.Single(settings.UserActions).Parameters["url"]);
    }
}
