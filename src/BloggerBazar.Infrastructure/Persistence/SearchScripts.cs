namespace BloggerBazar.Infrastructure.Persistence;

// A name search works in both scripts (owner, 2026-11-09: "I typed али and Ali did not come up"): the query is
// transliterated between Cyrillic and Uzbek Latin and every spelling is searched. Letters with two usual Latin
// spellings (х as x or h, ц as ts or s, ў and ғ with either apostrophe; h as ҳ or х the other way) give extra
// variants; the list is capped so a long query cannot explode. No table changes: the variants feed the same ILIKE.
public static class SearchScripts
{
    private const int MaxVariants = 8;

    private static readonly Dictionary<char, string[]> CyrillicToLatin = new()
    {
        ['а'] = ["a"], ['б'] = ["b"], ['в'] = ["v"], ['г'] = ["g"], ['д'] = ["d"], ['е'] = ["e"], ['ё'] = ["yo"], ['ж'] = ["j"],
        ['з'] = ["z"], ['и'] = ["i"], ['й'] = ["y"], ['к'] = ["k"], ['л'] = ["l"], ['м'] = ["m"], ['н'] = ["n"], ['о'] = ["o"],
        ['п'] = ["p"], ['р'] = ["r"], ['с'] = ["s"], ['т'] = ["t"], ['у'] = ["u"], ['ф'] = ["f"], ['х'] = ["x", "h"], ['ц'] = ["ts", "s"],
        ['ч'] = ["ch"], ['ш'] = ["sh"], ['щ'] = ["sh"], ['ъ'] = [""], ['ы'] = ["i"], ['ь'] = [""], ['э'] = ["e"], ['ю'] = ["yu"],
        ['я'] = ["ya"], ['ў'] = ["o‘", "o'"], ['қ'] = ["q"], ['ғ'] = ["g‘", "g'"], ['ҳ'] = ["h"]
    };

    // Digraphs and apostrophe letters first, then single letters.
    private static readonly (string Latin, string[] Cyrillic)[] LatinToCyrillic =
    [
        ("sh", ["ш"]), ("ch", ["ч"]), ("yo", ["ё"]), ("yu", ["ю"]), ("ya", ["я"]), ("ts", ["ц"]),
        ("o‘", ["ў"]), ("oʻ", ["ў"]), ("o'", ["ў"]), ("g‘", ["ғ"]), ("gʻ", ["ғ"]), ("g'", ["ғ"]),
        ("a", ["а"]), ("b", ["б"]), ("c", ["с"]), ("d", ["д"]), ("e", ["е"]), ("f", ["ф"]), ("g", ["г"]), ("h", ["ҳ", "х"]),
        ("i", ["и"]), ("j", ["ж"]), ("k", ["к"]), ("l", ["л"]), ("m", ["м"]), ("n", ["н"]), ("o", ["о"]), ("p", ["п"]),
        ("q", ["қ"]), ("r", ["р"]), ("s", ["с"]), ("t", ["т"]), ("u", ["у"]), ("v", ["в"]), ("w", ["в"]), ("x", ["х"]),
        ("y", ["й"]), ("z", ["з"])
    ];

    public static IReadOnlyList<string> Variants(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return [];
        var lower = trimmed.ToLowerInvariant();
        var variants = new List<string> { trimmed };
        var hasCyrillic = lower.Any(IsCyrillic);
        var hasLatin = lower.Any(character => character is >= 'a' and <= 'z');
        if (hasCyrillic) Add(variants, Expand(lower, Transliterate(lower, CyrillicToLatinStep)));
        if (hasLatin) Add(variants, Expand(lower, Transliterate(lower, LatinToCyrillicStep)));
        return variants;
    }

    private static bool IsCyrillic(char character) => character is >= 'а' and <= 'я' or 'ё' or 'ў' or 'қ' or 'ғ' or 'ҳ';

    private static void Add(List<string> variants, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (variants.Count >= MaxVariants) return;
            if (candidate.Length > 0 && !variants.Contains(candidate, StringComparer.OrdinalIgnoreCase)) variants.Add(candidate);
        }
    }

    // Each position of the query becomes a list of spellings; the product of those lists is the set of variants.
    private static List<string[]> Transliterate(string text, Func<string, int, (string[] Spellings, int Consumed)> step)
    {
        var pieces = new List<string[]>();
        var index = 0;
        while (index < text.Length)
        {
            var (spellings, consumed) = step(text, index);
            pieces.Add(spellings);
            index += consumed;
        }
        return pieces;
    }

    private static (string[] Spellings, int Consumed) CyrillicToLatinStep(string text, int index)
        => CyrillicToLatin.TryGetValue(text[index], out var spellings) ? (spellings, 1) : ([text[index].ToString()], 1);

    private static (string[] Spellings, int Consumed) LatinToCyrillicStep(string text, int index)
    {
        foreach (var (latin, cyrillic) in LatinToCyrillic)
        {
            if (string.CompareOrdinal(text, index, latin, 0, latin.Length) == 0) return (cyrillic, latin.Length);
        }
        return ([text[index].ToString()], 1);
    }

    private static IEnumerable<string> Expand(string original, List<string[]> pieces)
    {
        IEnumerable<string> results = [""];
        foreach (var spellings in pieces)
        {
            var current = results;
            results = spellings.SelectMany(spelling => current.Select(prefix => prefix + spelling)).Take(MaxVariants).ToList();
        }
        return results.Where(variant => !string.Equals(variant, original, StringComparison.Ordinal));
    }
}
