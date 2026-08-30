using System.Text;

namespace CustomerSupport.Application.Common;

/// <summary>AC-182 — folds Arabic diacritics (tashkeel) so "كِتَاب" and "كتاب" compare equal.
/// A no-op on text that carries none, so English search (AC-183) is unaffected.</summary>
public static class ArabicTextNormalizer
{
    // Arabic diacritics block: U+064B–U+065F, plus the superscript alef U+0670.
    private static bool IsDiacritic(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ';

    public static string Fold(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!IsDiacritic(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
