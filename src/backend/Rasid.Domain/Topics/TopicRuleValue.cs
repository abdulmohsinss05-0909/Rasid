using System.Text.RegularExpressions;

namespace Rasid.Domain.Topics;

/// <summary>
/// Single source of truth for what a valid rule value is and how it is normalised.
/// Used by the domain (invariants) and by application validators (user-facing messages),
/// so the two can never disagree. Whitelist-based to keep X query construction injection-safe.
/// </summary>
public static partial class TopicRuleValue
{
    public const int KeywordMinLength = 2;
    public const int KeywordMaxLength = 100;
    public const int HashtagMaxLength = 100;
    public const int AccountMaxLength = 15;

    public static bool TryNormalize(RuleType type, string? raw, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        var value = CollapseWhitespace(raw ?? string.Empty);

        switch (type)
        {
            case RuleType.Keyword:
                if (value.Length is < KeywordMinLength or > KeywordMaxLength)
                {
                    error = $"Keyword must be {KeywordMinLength}–{KeywordMaxLength} characters.";
                    return false;
                }

                if (!KeywordPattern().IsMatch(value))
                {
                    error = "Keyword may contain letters, numbers, spaces and . , ' & - _ only.";
                    return false;
                }

                normalized = value;
                return true;

            case RuleType.Hashtag:
                value = value.TrimStart('#');
                if (!HashtagPattern().IsMatch(value))
                {
                    error = $"Hashtag must be 1–{HashtagMaxLength} letters, numbers or underscores (no spaces).";
                    return false;
                }

                normalized = value;
                return true;

            case RuleType.Account:
                value = value.TrimStart('@');
                if (!AccountPattern().IsMatch(value))
                {
                    error = $"Account must be a valid X username: 1–{AccountMaxLength} letters, numbers or underscores.";
                    return false;
                }

                normalized = value;
                return true;

            default:
                error = "Unknown rule type.";
                return false;
        }
    }

    public static string Normalize(RuleType type, string? raw) =>
        TryNormalize(type, raw, out var normalized, out var error)
            ? normalized
            : throw new Common.DomainException(error);

    private static string CollapseWhitespace(string value) => WhitespacePattern().Replace(value.Trim(), " ");

    // Letters/numbers in any script (Arabic included), spaces and a small set of safe punctuation.
    // Quotes, parentheses and colons are excluded because they are X query operators.
    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} .,'&_\-]*$")]
    private static partial Regex KeywordPattern();

    [GeneratedRegex(@"^[\p{L}\p{N}_]{1,100}$")]
    private static partial Regex HashtagPattern();

    [GeneratedRegex("^[A-Za-z0-9_]{1,15}$")]
    private static partial Regex AccountPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
