using GigaChat.Net.SemanticKernel;

namespace GigaChat.Net.Tests;

public class ReActInstructionTests
{
    [Theory]
    [InlineData(nameof(GigaChatReActInstructions.DefaultRussian))]
    [InlineData(nameof(GigaChatReActInstructions.DefaultEnglish))]
    [InlineData(nameof(GigaChatReActInstructions.ToolFirst))]
    [InlineData(nameof(GigaChatReActInstructions.ReadOnlyResearch))]
    [InlineData(nameof(GigaChatReActInstructions.SupportAgent))]
    public void InstructionTemplate_IsNotNullOrWhiteSpace(string name)
    {
        var value = (string?)typeof(GigaChatReActInstructions)
            .GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .GetValue(null);

        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public void DefaultRussian_ContainsKeywordInRussian()
    {
        Assert.Contains("инструмент", GigaChatReActInstructions.DefaultRussian, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultEnglish_DoesNotRequireThoughtPrefix()
    {
        Assert.DoesNotContain("Thought:", GigaChatReActInstructions.DefaultEnglish, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyResearch_ForbidsModifyingActions()
    {
        Assert.Contains("read-only", GigaChatReActInstructions.ReadOnlyResearch, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolFirst_MandatesToolCallBeforeAnswer()
    {
        Assert.Contains("tool", GigaChatReActInstructions.ToolFirst, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportAgent_MentionsEscalation()
    {
        Assert.Contains("escalate", GigaChatReActInstructions.SupportAgent, StringComparison.OrdinalIgnoreCase);
    }
}
