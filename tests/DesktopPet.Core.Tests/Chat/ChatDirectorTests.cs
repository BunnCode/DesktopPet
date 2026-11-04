using DesktopPet.Core.Chat;
using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.Traits;

namespace DesktopPet.Core.Tests.Chat;

public class ChatDirectorTests
{
    private const double Dt = 0.1;

    /// <summary>Only the kind under test happens, as often as possible and with no gaps, unless a test says otherwise.</summary>
    private static readonly ChatRules Quiet = new()
    {
        NeedyPerHour = 0,
        BanterPerHour = 0,
        JokesPerHour = 0,
        NotesPerHour = 0,
        MinGapSeconds = 0,
        PetGapSeconds = 0,
        AnswerDelaySeconds = 0,
        NoteGapSeconds = 0,
        PetNoteGapSeconds = 0,
    };

    private static ChatPet Pet(
        int id, double x = 100, PetStats? stats = null, bool canTalk = true, long? window = null, ChatTraits? traits = null) =>
        new(id, $"Pet{id}", $"Pet{id} likes carrots.", stats ?? new PetStats(80, 80, 80), new Vec2(x, 500), 1, canTalk, window,
            traits ?? new ChatTraits());

    /// <summary>Rolls always succeed (0 is below any chance) and pick the first kind with a rate.</summary>
    private static ChatDirector Director(FakeChatModel model) => new(new FakeRandom(0.0)) { Model = model };

    private static List<ChatLine> Run(
        ChatDirector director, IReadOnlyList<ChatPet> pets, ChatRules rules, int ticks, int notes = 0, bool speech = true, bool notesOn = true)
    {
        var lines = new List<ChatLine>();
        for (var i = 0; i < ticks; i++)
        {
            director.Tick(Dt, pets, rules, speech, notesOn, notes);
            lines.AddRange(director.TakeLines());
        }

        return lines;
    }

    [Fact]
    public void A_hungry_pet_says_so_and_mentions_food()
    {
        var model = new FakeChatModel("I'm starving!");
        var director = Director(model);
        var lines = Run(director, [Pet(1, stats: new PetStats(10, 80, 80))], Quiet with { NeedyPerHour = 10 }, 2);

        var line = Assert.Single(lines);
        Assert.Equal(ChatKind.Needy, line.Kind);
        Assert.Equal("I'm starving!", line.Text);
        Assert.Contains("very hungry", model.Prompts[0].User, StringComparison.Ordinal);
        Assert.Contains("Pet1 likes carrots.", model.Prompts[0].System, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_lines_about_needs_are_told_the_mood()
    {
        var model = new FakeChatModel("Line.");
        var hungry = new PetStats(10, 80, 80);
        Run(Director(model), [Pet(1, stats: hungry)], Quiet with { NeedyPerHour = 10 }, 1);
        Run(Director(model), [Pet(1, stats: hungry)], Quiet with { JokesPerHour = 10 }, 1);

        Assert.Contains("you feel very hungry", model.Prompts[0].System, StringComparison.Ordinal);
        Assert.DoesNotContain("you feel", model.Prompts[1].System, StringComparison.Ordinal);
    }

    [Fact]
    public void No_needy_lines_while_needs_are_fine()
    {
        var model = new FakeChatModel("Feed me");
        var lines = Run(Director(model), [Pet(1)], Quiet with { NeedyPerHour = 1000 }, 50);

        Assert.Empty(lines);
        Assert.Empty(model.Prompts);
    }

    [Fact]
    public void Lines_wait_for_the_model_to_load()
    {
        var model = new FakeChatModel("Hi");
        var director = new ChatDirector(new FakeRandom(0.0));
        var rules = Quiet with { JokesPerHour = 10 };

        Assert.Empty(Run(director, [Pet(1)], rules, 10));
        director.Model = model;
        Assert.Single(Run(director, [Pet(1)], rules, 2));
    }

    [Fact]
    public void Only_one_request_runs_at_a_time()
    {
        var model = new FakeChatModel { Manual = true };
        var director = Director(model);
        var pets = new[] { Pet(1), Pet(2, x: 5000) };
        Run(director, pets, Quiet with { JokesPerHour = 10 }, 20);

        Assert.Single(model.Prompts);
        Assert.True(director.Busy);

        model.Release("Why did the carrot blush?");
        var lines = Run(director, pets, Quiet with { JokesPerHour = 10 }, 1);
        Assert.Equal("Why did the carrot blush?", Assert.Single(lines).Text);
    }

    [Fact]
    public void Gaps_space_lines_out()
    {
        var model = new FakeChatModel("Knock knock.");
        var rules = Quiet with { JokesPerHour = 3600, MinGapSeconds = 5, PetGapSeconds = 0 };
        var lines = Run(Director(model), [Pet(1), Pet(2, x: 5000)], rules, 100); // 10 s

        Assert.InRange(lines.Count, 2, 3); // at 0 s, about 5 s (and maybe 10 s)
    }

    [Fact]
    public void Each_pet_waits_its_own_gap()
    {
        var model = new FakeChatModel("Knock knock.");
        var rules = Quiet with { JokesPerHour = 3600, PetGapSeconds = 1000 };
        var lines = Run(Director(model), [Pet(1), Pet(2, x: 5000)], rules, 100);

        Assert.Equal([1, 2], lines.Select(l => l.PetId).ToArray());
    }

    [Fact]
    public void Sleeping_pets_say_nothing()
    {
        var model = new FakeChatModel("Zzz");
        var lines = Run(Director(model), [Pet(1, canTalk: false)], Quiet with { JokesPerHour = 1000 }, 50);

        Assert.Empty(lines);
    }

    [Fact]
    public void A_trait_of_zero_silences_that_kind()
    {
        var model = new FakeChatModel("Joke");
        var lines = Run(Director(model), [Pet(1, traits: new ChatTraits(Jokes: 0))], Quiet with { JokesPerHour = 1000 }, 50);

        Assert.Empty(lines);
    }

    [Fact]
    public void Chance_follows_the_rate_and_the_trait()
    {
        // FakeRandom(0.5): a roll succeeds only when the chance per tick is above 0.5.
        var model = new FakeChatModel("Joke");
        var director = new ChatDirector(new FakeRandom(0.5)) { Model = model };
        var perTickHalf = 0.5 * 3600 / Dt; // per-hour rate that makes the chance exactly 0.5

        Assert.Empty(Run(director, [Pet(1, traits: new ChatTraits(Jokes: 0.9))], Quiet with { JokesPerHour = perTickHalf }, 3));
        Assert.NotEmpty(Run(director, [Pet(1, traits: new ChatTraits(Jokes: 1.1))], Quiet with { JokesPerHour = perTickHalf }, 3));
    }

    [Fact]
    public void Banter_needs_a_pet_close_by_and_gets_an_answer()
    {
        var model = new FakeChatModel("Hi Pet2, nice ears!", "Thanks Pet1!");
        var rules = Quiet with { BanterPerHour = 10, BanterDistanceDip = 200, MinGapSeconds = 100 };

        Assert.Empty(Run(Director(new FakeChatModel("x")), [Pet(1, x: 100), Pet(2, x: 400)], rules, 20)); // too far

        var lines = Run(Director(model), [Pet(1, x: 100), Pet(2, x: 250)], rules, 5);
        Assert.Equal(2, lines.Count);
        Assert.Equal((1, ChatKind.Banter), (lines[0].PetId, lines[0].Kind));
        Assert.Equal((2, ChatKind.Answer), (lines[1].PetId, lines[1].Kind));
        Assert.Contains("Pet2", model.Prompts[0].User, StringComparison.Ordinal);
        Assert.Contains("Hi Pet2, nice ears!", model.Prompts[1].User, StringComparison.Ordinal);
        Assert.Contains("You are Pet2", model.Prompts[1].System, StringComparison.Ordinal);
    }

    [Fact]
    public void An_answer_waits_until_the_first_line_has_been_read()
    {
        var model = new FakeChatModel("Hello!", "Hi!");
        var rules = Quiet with { BanterPerHour = 10, AnswerDelaySeconds = 1, MinGapSeconds = 100 };
        var director = Director(model);
        var pets = new[] { Pet(1), Pet(2, x: 150) };

        Assert.Single(Run(director, pets, rules, 3)); // the line, and the answer is generated but held
        Assert.Equal(2, model.Prompts.Count);
        var later = Run(director, pets, rules, 10);
        Assert.Equal(ChatKind.Answer, Assert.Single(later).Kind);
    }

    [Fact]
    public void Notes_go_on_the_window_the_pet_stands_on_up_to_the_cap()
    {
        var model = new FakeChatModel("Drink some water!");
        var rules = Quiet with { NotesPerHour = 10, MaxNotes = 2 };

        Assert.Empty(Run(Director(model), [Pet(1)], rules, 20)); // on the floor: no window

        var line = Assert.Single(Run(Director(model), [Pet(1, x: 321, window: 42)], rules, 2));
        Assert.Equal((ChatKind.Note, 42L, 321.0), (line.Kind, line.WindowId, line.NoteX));

        Assert.Empty(Run(Director(model), [Pet(1, window: 42)], rules, 20, notes: 2)); // at the cap
    }

    [Fact]
    public void Notes_and_speech_are_separate_behaviors()
    {
        var model = new FakeChatModel("Hello!");
        var both = Quiet with { JokesPerHour = 10, NotesPerHour = 10 };

        // Speech off: notes still come. Notes off: speech still comes.
        var notesOnly = Run(Director(model), [Pet(1, window: 7)], both, 4, speech: false);
        Assert.NotEmpty(notesOnly);
        Assert.All(notesOnly, l => Assert.Equal(ChatKind.Note, l.Kind));
        var speechOnly = Run(Director(model), [Pet(1, window: 7)], both, 4, notesOn: false);
        Assert.NotEmpty(speechOnly);
        Assert.DoesNotContain(speechOnly, l => l.Kind == ChatKind.Note);
    }

    [Fact]
    public void A_line_just_said_does_not_hold_back_a_note()
    {
        var model = new FakeChatModel("Hello!");
        var rules = Quiet with { JokesPerHour = 10, NotesPerHour = 10, MinGapSeconds = 1000, PetGapSeconds = 1000, NoteGapSeconds = 1000 };
        var lines = Run(Director(model), [Pet(1, window: 7)], rules, 10);

        Assert.Equal([ChatKind.Joke, ChatKind.Note], lines.Select(l => l.Kind).ToArray()); // one of each; each waits its own gap
    }

    [Fact]
    public void Notes_are_written_not_spoken_and_remembered_apart()
    {
        var model = new FakeChatModel("Stretch your legs.", "Ha!");
        var director = Director(model);
        Run(director, [Pet(1, window: 7)], Quiet with { NotesPerHour = 10, NoteGapSeconds = 1000 }, 2);
        Run(director, [Pet(1, window: 7)], Quiet with { JokesPerHour = 10 }, 2, notesOn: false);

        Assert.Contains("sticky notes", model.Prompts[0].System, StringComparison.Ordinal);
        Assert.DoesNotContain("out loud", model.Prompts[0].System, StringComparison.Ordinal);
        Assert.Contains("out loud", model.Prompts[1].System, StringComparison.Ordinal);
        Assert.DoesNotContain("Stretch your legs.", model.Prompts[1].System, StringComparison.Ordinal);
        Assert.Equal(["Stretch your legs."], director.RecentNotes(1));
        Assert.Contains(ChatPrompts.NoteTopics[0], model.Prompts[0].User, StringComparison.Ordinal); // FakeRandom(0) picks the first topic
    }

    [Fact]
    public void A_clicked_pet_always_says_something_skipping_the_gaps()
    {
        var model = new FakeChatModel("Hehe, that tickles!");
        var director = Director(model);
        var rules = Quiet with { JokesPerHour = 10, MinGapSeconds = 1000, PetGapSeconds = 1000 };
        Run(director, [Pet(1)], rules, 2); // a joke: now both gaps are running

        director.Clicked(1, PetClick.Petted);
        var line = Assert.Single(Run(director, [Pet(1)], rules, 2));
        Assert.Equal((ChatKind.Clicked, "Hehe, that tickles!"), (line.Kind, line.Text));
        Assert.Contains("petted you", model.Prompts[^1].User, StringComparison.Ordinal);
        Assert.Contains("you feel", model.Prompts[^1].System, StringComparison.Ordinal); // its mood colours the reaction
    }

    [Fact]
    public void A_pet_woken_by_a_click_reacts_even_though_it_was_asleep()
    {
        var model = new FakeChatModel("Five more minutes...");
        var director = Director(model);
        director.Clicked(1, PetClick.WokenUp);

        Assert.Single(Run(director, [Pet(1, canTalk: false)], Quiet, 2));
        Assert.Contains("woke you up", model.Prompts[0].User, StringComparison.Ordinal);
    }

    [Fact]
    public void A_click_waits_for_the_model_and_the_latest_click_wins()
    {
        var model = new FakeChatModel { Manual = true };
        var director = Director(model);
        var rules = Quiet with { JokesPerHour = 10 };
        Run(director, [Pet(1)], rules, 1); // a joke in flight

        director.Clicked(1, PetClick.Poked);
        director.Clicked(1, PetClick.Petted);
        Run(director, [Pet(1)], Quiet, 3);
        Assert.Single(model.Prompts);

        model.Release("Ha.");
        Run(director, [Pet(1)], Quiet, 2);
        Assert.Equal(2, model.Prompts.Count);
        Assert.Contains("petted you", model.Prompts[1].User, StringComparison.Ordinal);
    }

    [Fact]
    public void A_click_the_model_cannot_get_to_in_time_is_dropped()
    {
        var model = new FakeChatModel { Manual = true };
        var director = Director(model);
        Run(director, [Pet(1)], Quiet with { JokesPerHour = 10 }, 1);

        director.Clicked(1, PetClick.Petted);
        Run(director, [Pet(1)], Quiet with { ClickLineWithinSeconds = 1 }, 20); // 2 s
        model.Release("Ha.");
        Run(director, [Pet(1)], Quiet with { ClickLineWithinSeconds = 1 }, 3);

        Assert.Single(model.Prompts);
    }

    [Fact]
    public void No_click_reactions_while_talking_is_off()
    {
        var model = new FakeChatModel("Hi!");
        var director = Director(model);
        director.Clicked(1, PetClick.Petted);

        Assert.Empty(Run(director, [Pet(1)], Quiet, 3, speech: false));
        Assert.Empty(Run(director, [Pet(1)], Quiet, 3)); // and the click isn't kept for later
    }

    [Fact]
    public void A_slow_reply_is_dropped_and_the_model_is_cancelled()
    {
        var model = new FakeChatModel { Manual = true };
        var director = Director(model);
        var rules = Quiet with { JokesPerHour = 10, ReplyTimeoutSeconds = 1 };
        Run(director, [Pet(1)], rules, 15);

        Assert.True(model.LastToken.IsCancellationRequested);
        model.Release("Too late");
        Assert.Empty(Run(director, [Pet(1)], rules, 1));
    }

    [Fact]
    public void Failures_are_reported_once_and_chat_carries_on()
    {
        var model = new FakeChatModel { Fail = "out of memory" };
        var director = Director(model);
        var rules = Quiet with { JokesPerHour = 1000 };

        Run(director, [Pet(1)], rules, 10);
        Assert.Equal(["out of memory"], director.TakeProblems());
        Assert.True(model.Prompts.Count > 1); // kept trying
        Run(director, [Pet(1)], rules, 10);
        Assert.Empty(director.TakeProblems());
    }

    [Fact]
    public void Turning_speech_off_cancels_and_drops_the_line()
    {
        var model = new FakeChatModel { Manual = true };
        var director = Director(model);
        var rules = Quiet with { JokesPerHour = 10 };
        Run(director, [Pet(1)], rules, 1);

        director.Tick(Dt, [Pet(1)], rules, speech: false, notes: true, notesOnScreen: 0);
        Assert.True(model.LastToken.IsCancellationRequested);
        model.Release("Hi");
        Assert.Empty(Run(director, [Pet(1)], Quiet, 2));
    }

    [Fact]
    public void A_removed_pet_says_nothing_more()
    {
        var model = new FakeChatModel { Manual = true };
        var director = Director(model);
        var rules = Quiet with { JokesPerHour = 10 };
        Run(director, [Pet(1)], rules, 1);

        director.Forget(1);
        model.Release("Hi");
        Assert.Empty(Run(director, [], Quiet, 2));
    }

    [Fact]
    public void Recent_lines_are_remembered_and_passed_on()
    {
        var model = new FakeChatModel("One.", "Two.", "Three.");
        var director = Director(model);
        Run(director, [Pet(1)], Quiet with { JokesPerHour = 1000, RecentLines = 2 }, 4);

        Assert.Equal(["Two.", "Three."], director.Recent(1));
        Assert.Contains("One. / Two.", model.Prompts[2].System, StringComparison.Ordinal);
    }

    [Fact]
    public void Unusable_replies_are_skipped()
    {
        var model = new FakeChatModel("  \"\" ");
        Assert.Empty(Run(Director(model), [Pet(1)], Quiet with { JokesPerHour = 10 }, 3));
    }
}

/// <summary>
/// A model that answers with the given replies in turn (cycling), immediately; or, with <see cref="Manual"/>, only when
/// <see cref="Release"/> is called; or fails with <see cref="Fail"/>.
/// </summary>
internal sealed class FakeChatModel(params string[] replies) : IChatModel
{
    private TaskCompletionSource<string>? _pending;
    private int _next;

    public List<ChatPrompt> Prompts { get; } = [];

    public bool Manual { get; init; }

    public string? Fail { get; init; }

    public CancellationToken LastToken { get; private set; }

    public Task<string> CompleteAsync(ChatPrompt prompt, int maxTokens, double temperature, CancellationToken cancellationToken)
    {
        Prompts.Add(prompt);
        LastToken = cancellationToken;
        if (Fail is not null)
        {
            return Task.FromException<string>(new InvalidOperationException(Fail));
        }

        if (Manual)
        {
            _pending = new TaskCompletionSource<string>();
            return _pending.Task;
        }

        return Task.FromResult(replies[_next++ % replies.Length]);
    }

    public void Release(string reply) => _pending?.SetResult(reply);
}
