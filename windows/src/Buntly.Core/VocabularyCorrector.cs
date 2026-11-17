using System.Text.RegularExpressions;

namespace Buntly.Core;

/// <summary>One row the person added in Settings. Supplements the built-in list.</summary>
public sealed record UserCorrection(string Wrong, string Correct);

/// <summary>
/// Fixes known mishearings after Whisper has already written the text.
/// Exact, case-insensitive, whole-word matches only. A "close enough"
/// match would rewrite words the person actually said. The built-in rows
/// are the same list as <c>VocabularyCorrector.swift</c>. Rows from
/// Settings are applied after those, and can add a fix but cannot remove one.
/// </summary>
public static class VocabularyCorrector
{
    public static readonly (string Correct, string[] WrongForms)[] BuiltIn =
    [
        ("resyndication", ["resindication", "re-syndication"]),
        ("kBtu", ["kbtu"]),
        ("HAP contract", ["cap contract", "happ contract", "hap contract"]),
        ("LIHTC", ["lightech", "lite tech", "litech"]),
        ("ASHRAE", ["ashray", "ash ray", "ashra"]),
        ("site EUI", ["site UI", "sight UI", "site you I"]),
        ("LIHTC", ["lihtc"]),
        ("QAP", ["qap"]),
        ("EUI", ["eui"]),
        ("LURA", ["lura"]),
        ("ASHRAE", ["ashrae"]),
        ("NHPD", ["nhpd"]),
        ("HUD", ["hud"]),
        ("RAD", ["r a d"]),
        ("PHA", ["p h a"]),
        ("AMI", ["a m i"]),
    ];

    public static string Apply(string text, IReadOnlyList<UserCorrection>? userCorrections = null)
    {
        var result = text;
        foreach (var (correct, wrongForms) in BuiltIn)
        {
            foreach (var wrong in wrongForms)
                result = ReplaceWholeWord(result, wrong, correct);
        }

        if (userCorrections is null)
            return result;

        foreach (var entry in userCorrections)
        {
            if (string.IsNullOrWhiteSpace(entry.Wrong) || string.IsNullOrEmpty(entry.Correct))
                continue;
            result = ReplaceWholeWord(result, entry.Wrong.Trim(), entry.Correct);
        }
        return result;
    }

    /// <summary>
    /// A lambda replacement, not a replacement pattern: a "$" typed in
    /// Settings would otherwise be read as a regex capture reference.
    /// </summary>
    private static string ReplaceWholeWord(string text, string target, string replacement)
    {
        var pattern = $@"\b{Regex.Escape(target)}\b";
        return Regex.Replace(
            text,
            pattern,
            _ => replacement,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }
}
