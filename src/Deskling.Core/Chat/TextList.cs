namespace Deskling.Core.Chat;

/// <summary>
/// A list of strings in the settings that compares by its items, so settings records stay equal after a round trip
/// through <c>settings.json</c> (plain lists compare by reference).
/// </summary>
public sealed class TextList : List<string>, IEquatable<TextList>
{
    public TextList()
    {
    }

    public TextList(IEnumerable<string> items)
        : base(items)
    {
    }

    public bool Equals(TextList? other) => other is not null && this.SequenceEqual(other, StringComparer.Ordinal);

    public override bool Equals(object? obj) => obj is TextList other && Equals(other);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var item in this)
        {
            hash.Add(item, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
