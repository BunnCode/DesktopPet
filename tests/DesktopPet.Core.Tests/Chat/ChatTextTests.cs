using DesktopPet.Core.Chat;
using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Tests.Chat;

public class ChatTextTests
{
    [Theory]
    [InlineData("I'm hungry!", "I'm hungry!")]
    [InlineData("  \"I'm hungry!\"  ", "I'm hungry!")]
    [InlineData("“Feed me, please.”", "Feed me, please.")]
    [InlineData("Mia: Feed me!", "Feed me!")]
    [InlineData("Mia says: Feed me!", "Feed me!")]
    [InlineData("*wiggles nose* Feed me!", "Feed me!")]
    [InlineData("Feed me! 🥕🐰", "Feed me!")]
    [InlineData("<think>\nhmm\n</think>\n\nFeed me!", "Feed me!")]
    [InlineData("Feed me!<|im_end|>", "Feed me!")]
    [InlineData("Line one.\nLine two!", "Line one. Line two!")]
    [InlineData("One. Two! Three? Four.", "One. Two!")]
    [InlineData("\"Hello there", "Hello there")]
    [InlineData("Mia jumps on your desk and says, \"Why did the bunny nap? It was tired!", "Why did the bunny nap? It was tired!")]
    [InlineData("She whispers: “Carrots are life.”", "Carrots are life.")]
    public void Cleans_up_model_replies(string raw, string expected)
    {
        Assert.Equal(expected, ChatText.Clean(raw, "Mia"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("🥕🥕")]
    [InlineData("<think>still thinking")]
    [InlineData("*hops*")]
    public void Rejects_replies_with_nothing_to_say(string? raw)
    {
        Assert.Null(ChatText.Clean(raw, "Mia"));
    }

    [Fact]
    public void Long_replies_are_cut_at_a_word()
    {
        var raw = string.Join(" ", Enumerable.Repeat("carrots", 60));
        var text = ChatText.Clean(raw, "Mia")!;

        Assert.True(text.Length <= ChatText.MaxLength);
        Assert.EndsWith("carrots…", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_limits_can_be_changed()
    {
        var raw = "One carrot. Two carrots. Three carrots, four carrots and more carrots.";

        Assert.Equal("One carrot. Two carrots. Three carrots, four carrots and more carrots.", ChatText.Clean(raw, "Mia", 3, 500));
        Assert.True(ChatText.Clean(raw, "Mia", 3, 30)!.Length <= 30);
    }

    [Fact]
    public void ChatMl_switches_thinking_off()
    {
        var text = ChatMl.Format(new ChatPrompt("sys", "usr"));

        Assert.Equal(
            "<|im_start|>system\nsys<|im_end|>\n<|im_start|>user\nusr<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n", text);
    }

    [Theory]
    [InlineData(80, 80, 80, "great")]
    [InlineData(70, 60, 80, "fine")]
    [InlineData(40, 80, 10, "hungry and very sleepy")]
    public void Mood_in_words(double hunger, double happiness, double energy, string expected)
    {
        Assert.Equal(expected, ChatPrompts.Mood(new PetStats(hunger, happiness, energy)));
    }

    [Fact]
    public void The_lowest_need_below_the_threshold_is_the_one_mentioned()
    {
        Assert.Null(ChatDirector.LowestNeed(new PetStats(50, 40, 60), 30));
        Assert.Equal((NeedKind.Energy, 12.0), ChatDirector.LowestNeed(new PetStats(25, 40, 12), 30));
    }
}
