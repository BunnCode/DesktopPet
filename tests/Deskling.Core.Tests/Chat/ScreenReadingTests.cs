using Deskling.Core.Chat;
using Deskling.Core.Math;

namespace Deskling.Core.Tests.Chat;

public class ScreenReadingTests
{
    private static readonly ChatRules Rules = new();

    private static ScreenLine Line(string text, int left, int top, int width = 200, int height = 16) =>
        new(text, RectI.FromSize(left, top, width, height));

    // ---- Which windows may be read ----

    [Theory]
    [InlineData("notepad", "Shopping list - Notepad", true)]
    [InlineData("KeePass", "Database - KeePass", false)]
    [InlineData("keepass.exe", "Anything", false)]          // any case, ".exe" optional
    [InlineData("msedge", "News - [InPrivate] - Microsoft Edge", false)]
    [InlineData("firefox", "Mozilla Firefox Private Browsing", false)]
    [InlineData("chrome", "Sign in - Google Accounts - Google Chrome", false)]
    [InlineData("chrome", "Change your password", false)]
    [InlineData("chrome", "Recipes - Google Chrome", true)]
    public void Excluded_apps_and_title_words_are_never_read(string process, string title, bool allowed) =>
        Assert.Equal(allowed, ScreenReading.Allows(process, title, Rules));

    [Fact]
    public void Exclusions_come_from_the_settings()
    {
        var rules = new ChatRules { ReadExcludedApps = ["Notepad.exe"], ReadExcludedTitleWords = [] };

        Assert.False(ScreenReading.Allows("notepad", "List", rules));
        Assert.True(ScreenReading.Allows("KeePass", "Password", rules)); // nothing else is excluded any more
    }

    // ---- Which part of a window ----

    [Fact]
    public void The_area_is_a_box_around_the_pet()
    {
        var window = new RectI(0, 0, 1920, 1080);
        var area = ScreenReading.Area(window, new Vec2(1000, 500), scale: 1);

        Assert.Equal(new RectI(550, 300, 1450, 1000), area); // 450 to each side, 200 up, 500 down
    }

    [Fact]
    public void On_a_window_top_the_area_slides_down_into_the_window()
    {
        var window = new RectI(100, 300, 1900, 1200);
        var area = ScreenReading.Area(window, new Vec2(1000, 300), scale: 1);

        Assert.Equal(new RectI(550, 300, 1450, 1000), area);
    }

    [Fact]
    public void In_front_of_a_window_bottom_the_area_slides_up_and_in_from_the_side()
    {
        var window = new RectI(0, 0, 1200, 1040);
        var area = ScreenReading.Area(window, new Vec2(1150, 1040), scale: 1); // on the taskbar, near the window's right edge

        Assert.Equal(new RectI(300, 340, 1200, 1040), area);
    }

    [Fact]
    public void The_area_grows_with_the_scale_and_stops_at_the_window()
    {
        var window = new RectI(0, 0, 1000, 600);
        var area = ScreenReading.Area(window, new Vec2(100, 0), scale: 2);

        Assert.Equal(new RectI(0, 0, 1000, 600), area);
    }

    // ---- Which lines are the window's own ----

    [Fact]
    public void Lines_under_windows_in_front_or_outside_the_window_are_dropped()
    {
        var window = new RectI(0, 0, 1000, 1000);
        var lines = new[]
        {
            Line("On the window", 100, 100),
            Line("Under a window in front", 600, 100),
            Line("Off the window", 1100, 100),
        };

        var visible = ScreenReading.Visible(lines, window, [new RectI(500, 0, 900, 400)]);

        Assert.Equal(["On the window"], visible.Select(l => l.Text));
    }

    // ---- The snippet ----

    [Fact]
    public void The_snippet_is_the_nearest_lines_in_reading_order()
    {
        var lines = new[]
        {
            Line("Far away at the bottom", 0, 900),
            Line("Second line near the pet", 100, 140),
            Line("First line near the pet", 100, 120),
        };

        var snippet = ScreenReading.Snippet(lines, new Vec2(150, 100), maxChars: 60);

        Assert.Equal("First line near the pet\nSecond line near the pet", snippet);
    }

    [Fact]
    public void A_long_line_is_cut_at_a_word()
    {
        var snippet = ScreenReading.Snippet([Line("The quick brown fox jumps over the lazy dog", 0, 0)], Vec2.Zero, maxChars: 20);

        Assert.Equal("The quick brown fox", snippet);
    }

    [Theory]
    [InlineData("|| -- 12:45 ##")]
    [InlineData("ok")]
    [InlineData("1,234.56 €")]
    public void Noise_and_numbers_are_left_out(string text) =>
        Assert.Null(ScreenReading.Snippet([Line(text, 0, 0)], Vec2.Zero, maxChars: 300));

    [Fact]
    public void The_title_bar_is_not_read_again()
    {
        var lines = new[] { Line("Recipes - Browser", 0, 0), Line("Carrot cake", 0, 40) };

        Assert.Equal("Carrot cake", ScreenReading.Snippet(lines, Vec2.Zero, maxChars: 300, title: "Recipes  - browser"));
    }

    [Fact]
    public void Spacing_is_tidied()
    {
        var snippet = ScreenReading.Snippet([Line("  Hello    there   world ", 0, 0)], Vec2.Zero, maxChars: 300);

        Assert.Equal("Hello there world", snippet);
    }
}
