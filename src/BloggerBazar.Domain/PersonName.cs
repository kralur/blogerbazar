using System.Globalization;
using System.Text;

namespace BloggerBazar.Domain;

// Names typed in Telegram often come in "fancy" Unicode letters (mathematical bold, script, small capitals, fullwidth)
// that fonts do not have, so one name on a list is drawn in a fallback face (owner, 2026-11-09: "the font must be the
// same for everyone"). Every name is folded to plain letters when it is saved: compatibility decomposition, the small
// capitals mapped by hand, invisible format and control characters dropped, whitespace collapsed. Emoji stay.
public static class PersonName
{
    private const string SmallCapitals = "ᴀʙᴄᴅᴇꜰɢʜɪᴊᴋʟᴍɴᴏᴘǫʀꜱᴛᴜᴠᴡʏᴢ";
    private const string SmallCapitalsPlain = "abcdefghijklmnopqrstuvwyz";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value?.Trim() ?? "";
        var folded = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(folded.Length);
        var pendingSpace = false;
        foreach (var rune in folded.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Format or UnicodeCategory.Control or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Surrogate) continue;
            if (pendingSpace) { builder.Append(' '); pendingSpace = false; }
            var small = rune.IsBmp ? SmallCapitals.IndexOf((char)rune.Value) : -1;
            if (small >= 0) builder.Append(SmallCapitalsPlain[small]);
            else builder.Append(rune.ToString());
        }
        var result = builder.ToString();
        // A name that was nothing but symbols stays as it was rather than vanishing.
        return result.Length > 0 ? result : value.Trim();
    }
}
