using System.Globalization;
using Deskling.Core.Abstractions;

namespace Deskling.Core.Traits;

/// <summary>
/// Built-in names for new pets: 100 of the most popular US baby names (the top 50 girls' and top 50 boys' names of
/// recent years in the Social Security Administration's data) and 100 rare ones (old-fashioned names that hardly
/// anyone gives a baby any more). Compiled by hand; the SSA download wasn't available when this was written.
/// </summary>
public static class PetNames
{
    public static IReadOnlyList<string> Popular { get; } =
    [
        "Olivia", "Emma", "Amelia", "Charlotte", "Mia", "Sophia", "Isabella", "Evelyn", "Ava", "Sofia",
        "Camila", "Harper", "Luna", "Eleanor", "Violet", "Aurora", "Elizabeth", "Eliana", "Hazel", "Chloe",
        "Ellie", "Nora", "Gianna", "Lily", "Emily", "Aria", "Scarlett", "Penelope", "Abigail", "Ivy",
        "Layla", "Avery", "Mila", "Delilah", "Ella", "Isla", "Willow", "Grace", "Lainey", "Nova",
        "Madison", "Riley", "Elena", "Zoey", "Lucy", "Stella", "Emilia", "Naomi", "Josephine", "Hannah",
        "Liam", "Noah", "Oliver", "Theodore", "James", "Henry", "Mateo", "Elijah", "Lucas", "William",
        "Benjamin", "Levi", "Ezra", "Sebastian", "Jack", "Daniel", "Samuel", "Michael", "Ethan", "Asher",
        "John", "Hudson", "Luca", "Leo", "Elias", "Owen", "Alexander", "Maverick", "Jackson", "Kai",
        "Grayson", "Wyatt", "Julian", "Aiden", "Gabriel", "Jayden", "Luke", "David", "Isaiah", "Miles",
        "Josiah", "Waylon", "Jacob", "Matthew", "Carter", "Logan", "Ezekiel", "Thomas", "Isaac", "Mason",
    ];

    public static IReadOnlyList<string> Rare { get; } =
    [
        "Agatha", "Bertha", "Clotilde", "Dorcas", "Edna", "Fenella", "Gertrude", "Hortense", "Imelda", "Jemima",
        "Kunigunde", "Lavinia", "Mildred", "Nerissa", "Ottoline", "Perpetua", "Rosamund", "Sybil", "Temperance", "Ursula",
        "Verity", "Winifred", "Zenobia", "Ermengarde", "Philomena", "Euphemia", "Griselda", "Honoria", "Myrtle", "Ethel",
        "Henrietta", "Wilhelmina", "Prudence", "Marigold", "Dorothea", "Bernadette", "Gwendolyn", "Petronella", "Leocadia", "Hildegard",
        "Clementina", "Araminta", "Eulalia", "Philippa", "Theodosia", "Cordelia", "Millicent", "Ottilie", "Albertina", "Beatrix",
        "Bartholomew", "Cuthbert", "Ebenezer", "Fitzgerald", "Gulliver", "Horatio", "Ignatius", "Jedediah", "Lancelot", "Marmaduke",
        "Mortimer", "Neville", "Ozias", "Peregrine", "Quentin", "Reginald", "Sylvester", "Thaddeus", "Ulysses", "Wilbur",
        "Barnaby", "Cornelius", "Eustace", "Humphrey", "Archibald", "Percival", "Algernon", "Bertram", "Clarence", "Desmond",
        "Ephraim", "Ferdinand", "Godfrey", "Hezekiah", "Isidore", "Lysander", "Montgomery", "Nathaniel", "Obadiah", "Phineas",
        "Rupert", "Septimus", "Tobias", "Valentine", "Wendell", "Alistair", "Benedict", "Crispin", "Dashiell", "Evander",
    ];

    /// <summary>Both lists together.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Popular, .. Rare];

    /// <summary>
    /// A random name that no existing pet has (ignoring case). Once every name is taken, names get a number:
    /// "Olivia 2".
    /// </summary>
    public static string Pick(IRandom random, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(taken);
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        var free = All.Where(n => !used.Contains(n)).ToList();
        if (free.Count > 0)
        {
            return free[Index(random, free.Count)];
        }

        var name = All[Index(random, All.Count)];
        for (var n = 2; ; n++)
        {
            var numbered = string.Create(CultureInfo.InvariantCulture, $"{name} {n}");
            if (!used.Contains(numbered))
            {
                return numbered;
            }
        }
    }

    private static int Index(IRandom random, int count) =>
        System.Math.Clamp((int)(random.NextDouble() * count), 0, count - 1);
}
