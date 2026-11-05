using Deskling.Core.Chat;
using Deskling.Core.Math;
using Deskling.Core.Stats;
using Deskling.Core.Tests.Fakes;
using Deskling.Core.Traits;

namespace Deskling.Core.Tests.Chat;

/// <summary>Pets reading the screen (M12b): remarks about what they read, and notes about it.</summary>
public class ChatReadingTests
{
    private const double Dt = 0.1;
    private const long Window = 42;

    private static readonly ChatRules Quiet = new()
    {
        NeedyPerHour = 0,
        BanterPerHour = 0,
        JokesPerHour = 0,
        NotesPerHour = 0,
        RemarksPerHour = 0,
        NoteReadChance = 0,
        MinGapSeconds = 0,
        PetGapSeconds = 0,
        NoteGapSeconds = 0,
        PetNoteGapSeconds = 0,
    };

    private static ChatPet Pet(long? onWindow = null, long? readWindow = Window, ChatTraits? traits = null) =>
        new(1, "Mia", "Mia likes carrots.", new PetStats(80, 80, 80), new Vec2(500, 500), 1, true, onWindow, traits ?? new ChatTraits(),
            readWindow);

    private static List<ChatLine> Run(ChatDirector director, ChatPet pet, ChatRules rules, int ticks = 2) // 2 ticks: start, then collect
    {
        var lines = new List<ChatLine>();
        for (var i = 0; i < ticks; i++)
        {
            director.Tick(Dt, [pet], rules, speech: true, notes: true, notesOnScreen: 0);
            lines.AddRange(director.TakeLines());
        }

        return lines;
    }

    private static ChatDirector Director(FakeChatModel model, FakeScreenReader? reader) =>
        new(new FakeRandom(0.0)) { Model = model, Reader = reader };

    private static FakeScreenReader Reads(params string[] lines) =>
        new(new ScreenText("Recipes - Browser", [.. lines.Select((t, i) => new ScreenLine(t, RectI.FromSize(400, 520 + (i * 20), 300, 16)))]));

    [Fact]
    public void A_pet_remarks_on_what_it_reads_on_its_window()
    {
        var model = new FakeChatModel("Ooh, carrot cake!");
        var reader = Reads("Carrot cake with walnuts", "Bake for 40 minutes");
        var lines = Run(Director(model, reader), Pet(), Quiet with { RemarksPerHour = 10 });

        var line = Assert.Single(lines);
        Assert.Equal(ChatKind.Remark, line.Kind);
        Assert.Equal("Ooh, carrot cake!", line.Text);
        Assert.Equal(Window, reader.Reads[0].WindowId);
        var prompt = model.Prompts[0];
        Assert.Contains("Carrot cake with walnuts\nBake for 40 minutes", prompt.User, StringComparison.Ordinal);
        Assert.Contains("\"Recipes - Browser\"", prompt.User, StringComparison.Ordinal);
        Assert.DoesNotContain("you feel", prompt.System, StringComparison.Ordinal); // spoken, without the mood
    }

    [Fact]
    public void Nothing_readable_means_no_remark()
    {
        var model = new FakeChatModel("Hmm.");
        var lines = Run(Director(model, new FakeScreenReader(null)), Pet(), Quiet with { RemarksPerHour = 10 });

        Assert.Empty(lines);
        Assert.Empty(model.Prompts);
    }

    [Fact]
    public void A_failed_read_is_just_nothing_read()
    {
        var model = new FakeChatModel("Hmm.");
        var director = Director(model, new FakeScreenReader(null) { Fail = true });
        var lines = Run(director, Pet(), Quiet with { RemarksPerHour = 10 });

        Assert.Empty(lines);
        Assert.Empty(director.TakeProblems());
    }

    [Theory]
    [InlineData(false, true)]  // reading switched off (no reader)
    [InlineData(true, false)]  // not on or beside a window
    public void No_remarks_without_reading_or_a_window(bool reading, bool besideWindow)
    {
        var model = new FakeChatModel("Hmm.");
        var reader = Reads("Carrot cake");
        var lines = Run(Director(model, reading ? reader : null), Pet(readWindow: besideWindow ? Window : null), Quiet with { RemarksPerHour = 1000 }, 50);

        Assert.Empty(lines);
        Assert.Empty(reader.Reads);
    }

    [Fact]
    public void Curiosity_scales_remarks_and_zero_means_never()
    {
        var model = new FakeChatModel("Hmm.");
        var reader = Reads("Carrot cake");
        var lines = Run(Director(model, reader), Pet(traits: new ChatTraits(Curiosity: 0)), Quiet with { RemarksPerHour = 1000 }, 50);

        Assert.Empty(lines);
    }

    [Fact]
    public void A_note_may_be_about_what_the_pet_reads_there()
    {
        var model = new FakeChatModel("Save me a slice!");
        var reader = Reads("Carrot cake with walnuts");
        var lines = Run(Director(model, reader), Pet(onWindow: Window), Quiet with { NotesPerHour = 10, NoteReadChance = 1 });

        var note = Assert.Single(lines);
        Assert.Equal(ChatKind.Note, note.Kind);
        Assert.Equal(Window, note.WindowId);
        Assert.Contains("Carrot cake with walnuts", model.Prompts[0].User, StringComparison.Ordinal);
        Assert.Contains("sticky note", model.Prompts[0].User, StringComparison.Ordinal);
    }

    [Fact]
    public void A_note_with_nothing_readable_falls_back_to_a_usual_topic()
    {
        var model = new FakeChatModel("Drink some water!");
        var lines = Run(Director(model, new FakeScreenReader(null)), Pet(onWindow: Window), Quiet with { NotesPerHour = 10, NoteReadChance = 1 });

        Assert.Equal("Drink some water!", Assert.Single(lines).Text);
        Assert.Equal(ChatPrompts.Note(ChatPrompts.NoteTopics[0]), model.Prompts[0].User);
    }

    [Fact]
    public void Notes_dont_read_when_the_chance_is_zero()
    {
        var model = new FakeChatModel("Stretch!");
        var reader = Reads("Carrot cake");
        Run(Director(model, reader), Pet(onWindow: Window), Quiet with { NotesPerHour = 10, NoteReadChance = 0 });

        Assert.Empty(reader.Reads);
        Assert.DoesNotContain("Carrot", model.Prompts[0].User, StringComparison.Ordinal);
    }
}

internal sealed class FakeScreenReader(ScreenText? text) : IScreenReader
{
    public List<(long WindowId, Vec2 Near)> Reads { get; } = [];

    public bool Fail { get; init; }

    public Task<ScreenText?> ReadAsync(long windowId, Vec2 near, double scale, CancellationToken cancellationToken)
    {
        Reads.Add((windowId, near));
        return Fail ? Task.FromException<ScreenText?>(new InvalidOperationException("OCR broke")) : Task.FromResult(text);
    }
}
