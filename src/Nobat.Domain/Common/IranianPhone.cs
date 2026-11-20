using System.Globalization;

namespace Nobat.Domain.Common;

/// <summary>
/// Normalises Iranian mobile numbers to the canonical <c>09XXXXXXXXX</c> form.
/// Persian and Arabic digits are accepted because that is what patients type.
/// </summary>
public static class IranianPhone
{
    public const int Length = 11;

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        Span<char> buffer = stackalloc char[input.Length];
        var count = 0;

        foreach (var raw in input)
        {
            var c = ToLatinDigit(raw);

            if (c == '+')
            {
                // A plus sign is only meaningful as the very first character.
                if (count == 0)
                {
                    continue;
                }

                return false;
            }

            if (char.IsWhiteSpace(c) || c is '-' or '.' or '(' or ')' or '\u200C')
            {
                continue;
            }

            if (!char.IsAsciiDigit(c))
            {
                return false;
            }

            buffer[count++] = c;
        }

        var digits = buffer[..count];

        if (digits.StartsWith("+98", StringComparison.Ordinal))
        {
            digits = digits[3..];
        }
        else if (digits.StartsWith("0098", StringComparison.Ordinal))
        {
            digits = digits[4..];
        }
        else if (digits.StartsWith("98", StringComparison.Ordinal) && digits.Length == 12)
        {
            digits = digits[2..];
        }

        if (digits.Length == 10 && digits[0] == '9')
        {
            normalized = string.Concat("0", digits);
            return true;
        }

        if (digits.Length == Length && digits[0] == '0' && digits[1] == '9')
        {
            normalized = digits.ToString();
            return true;
        }

        return false;
    }

    public static string Normalize(string input)
    {
        if (!TryNormalize(input, out var normalized))
        {
            throw new DomainRuleViolationException(
                "phone.invalid",
                "شمارهٔ موبایل معتبر نیست. نمونهٔ درست: ۰۹۱۲۱۲۳۴۵۶۷");
        }

        return normalized;
    }

    /// <summary>Masks a canonical number for logs and lists: <c>0912***4567</c>.</summary>
    public static string Mask(string normalized)
    {
        if (normalized is null || normalized.Length != Length)
        {
            return "***";
        }

        return string.Concat(normalized.AsSpan(0, 4), "***", normalized.AsSpan(7));
    }

    private static char ToLatinDigit(char c) => c switch
    {
        >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')),
        >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')),
        _ => c,
    };

    internal static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}
