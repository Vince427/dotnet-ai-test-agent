using DesktopAiTestAgent.AgentRunner;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class LlmResponseParserTests
{
    [Fact]
    public void ParseReturnsActionFromCleanJson()
    {
        var raw = "{\"actionType\":\"Click\",\"automationId\":\"btnLogin\",\"value\":\"go\",\"confidence\":90}";

        var action = LlmResponseParser.Parse(raw);

        Assert.Equal("Click", action.ActionType);
        Assert.Equal("btnLogin", action.AutomationId);
        Assert.Equal("go", action.Value);
        Assert.Equal(90, action.Confidence);
    }

    [Fact]
    public void ParseIsCaseInsensitiveOnPropertyNames()
    {
        var raw = "{\"ActionType\":\"Done\",\"Confidence\":50}";

        var action = LlmResponseParser.Parse(raw);

        Assert.Equal("Done", action.ActionType);
        Assert.Equal(50, action.Confidence);
    }

    [Fact]
    public void ParseStripsJsonCodeFence()
    {
        var raw = "```json\n{\"actionType\":\"Click\",\"automationId\":\"btnOk\"}\n```";

        var action = LlmResponseParser.Parse(raw);

        Assert.Equal("Click", action.ActionType);
        Assert.Equal("btnOk", action.AutomationId);
    }

    [Fact]
    public void ParseStripsPlainCodeFence()
    {
        var raw = "```\n{\"actionType\":\"Wait\"}\n```";

        var action = LlmResponseParser.Parse(raw);

        Assert.Equal("Wait", action.ActionType);
    }

    [Fact]
    public void ParseHandlesSurroundingWhitespace()
    {
        var raw = "   \n  {\"actionType\":\"Explore\"}  \n ";

        var action = LlmResponseParser.Parse(raw);

        Assert.Equal("Explore", action.ActionType);
    }

    [Fact]
    public void ParseFallsBackToWaitOnInvalidJson()
    {
        var action = LlmResponseParser.Parse("this is not json at all");

        Assert.Equal("Wait", action.ActionType);
        Assert.StartsWith("Parse error", action.Reason);
    }

    [Fact]
    public void ParseFallsBackToWaitOnEmptyString()
    {
        var action = LlmResponseParser.Parse("");

        Assert.Equal("Wait", action.ActionType);
        Assert.StartsWith("Parse error", action.Reason);
    }

    [Fact]
    public void ParseFallsBackToWaitOnNull()
    {
        var action = LlmResponseParser.Parse(null);

        Assert.Equal("Wait", action.ActionType);
        Assert.StartsWith("Parse error", action.Reason);
    }

    [Fact]
    public void ParseReturnsWaitOnJsonNullLiteral()
    {
        var action = LlmResponseParser.Parse("null");

        Assert.Equal("Wait", action.ActionType);
        Assert.Equal("Deserialized null", action.Reason);
    }
}
