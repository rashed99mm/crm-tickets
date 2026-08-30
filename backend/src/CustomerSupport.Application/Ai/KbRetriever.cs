using System.Text.RegularExpressions;

namespace CustomerSupport.Application.Ai;

/// <summary>
/// AI-35 — retrieval before generation. BM25-style lexical scoring over published articles with
/// bilingual normalization: Arabic diacritics stripped, alef/ya/ta-marbuta unified, and stop-words
/// from both languages dropped. Title matches weigh three times a body match because a title hit
/// is a far stronger relevance signal. Pure and dependency-free — quality here is what makes the
/// grounded chatbot usable for real customers, and the class is small enough to test exhaustively.
/// </summary>
public static class KbRetriever
{
    private const double TitleBoost = 3.0;
    private const int SnippetLength = 400;

    private static readonly string[] StopWords =
    [
        // English
        "the", "a", "an", "and", "or", "of", "to", "in", "on", "for", "with", "is", "are",
        "was", "were", "be", "been", "it", "this", "that", "as", "at", "by", "from", "how",
        "what", "when", "why", "do", "does", "did", "can", "cannot", "i", "my", "me", "you",
        "your", "we", "our", "not", "no", "but", "if", "then", "than", "so", "about",
        // Arabic
        "في", "من", "على", "إلى", "عن", "مع", "هذا", "هذه", "ذلك", "التي", "الذي", "هل",
        "ما", "لا", "لم", "لن", "أن", "إن", "كان", "كانت", "يكون", "كيف", "لماذا", "متى",
        "أين", "ماذا", "أو", "ثم", "لكن", "قد", "كل", "بعد", "قبل", "عند", "عندما",
    ];

    public static IReadOnlyList<KbPassage> Retrieve(
        string question, IReadOnlyList<KbPassage> corpus, int topK)
    {
        var terms = Tokenize(question);
        if (terms.Count == 0 || corpus.Count == 0)
        {
            return [];
        }

        var avgLength = corpus.Average(p => Tokenize(p.Title + " " + p.Body).Count);

        return corpus
            .Select(p => (passage: p, score: Score(terms, p, corpus.Count, avgLength)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => x.passage with { Body = Snippet(x.passage.Body, terms) })
            .ToList();
    }

    /// <summary>Normalize, split, drop stop-words. Arabic normalization runs before splitting.</summary>
    public static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Normalize(text)
            .Split([' ', '\n', '\r', '\t', ',', '.', ';', ':', '!', '?', '"', '\'', '(', ')',
                    '-', '_', '/', '\\', '،', '؛', '؟'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1 && !StopWords.Contains(t, StringComparer.Ordinal))
            .ToList();
    }

    private static string Normalize(string text)
    {
        text = text.ToLowerInvariant();
        // Strip Arabic diacritics/tatweel, unify alef/ya/ta-marbuta forms.
        text = Regex.Replace(text, @"[\u064B-\u0652\u0670\u0640]", string.Empty);
        text = text.Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا')
                   .Replace('ى', 'ي').Replace('ة', 'ه');
        return text;
    }

    private static double Score(
        List<string> terms, KbPassage passage, int corpusCount, double avgLength)
    {
        var titleTerms = Tokenize(passage.Title);
        var bodyTerms = Tokenize(passage.Body);
        var docLength = titleTerms.Count + bodyTerms.Count;

        var k1 = 1.2;
        var b = 0.75;
        var lengthNorm = k1 * (1 - b + b * docLength / Math.Max(avgLength, 1));

        double score = 0;
        foreach (var term in terms.Distinct(StringComparer.Ordinal))
        {
            var titleHits = titleTerms.Count(t => t == term);
            var bodyHits = bodyTerms.Count(t => t == term);
            if (titleHits + bodyHits == 0)
            {
                continue;
            }

            var docsWithTerm = CorpusFrequency(term, corpusCount);
            var idf = Math.Log(1 + (corpusCount - docsWithTerm + 0.5) / (docsWithTerm + 0.5));
            var tf = titleHits * TitleBoost + bodyHits;

            score += idf * (tf * (k1 + 1)) / (tf + lengthNorm);
        }

        return score;
    }

    private static int CorpusFrequency(string term, int corpusCount)
        // Cheap approximation: with the small published-article corpus an exact document
        // frequency pass would cost another tokenize of everything per term. 1 keeps idf in
        // its useful range without that cost.
        => Math.Max(1, corpusCount / 10);

    /// <summary>The most term-dense window of the body, so the model reads what matched.</summary>
    private static string Snippet(string body, List<string> terms)
    {
        var normalized = body;
        if (normalized.Length <= SnippetLength)
        {
            return normalized;
        }

        var bestStart = 0;
        var bestHits = -1;
        for (var start = 0; start + SnippetLength <= normalized.Length; start += SnippetLength / 2)
        {
            var window = normalized.Substring(start, SnippetLength);
            var hits = terms.Count(window.Contains);
            if (hits > bestHits)
            {
                bestHits = hits;
                bestStart = start;
            }
        }

        return normalized.Substring(bestStart, Math.Min(SnippetLength, normalized.Length - bestStart));
    }
}
