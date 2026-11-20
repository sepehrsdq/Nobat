using System.Globalization;
using System.Security.Cryptography;

namespace Nobat.Domain.Verification;

/// <summary>
/// The short numeric code a patient receives by SMS. Persian and Arabic digits are accepted
/// on the way in, because that is what a phone keyboard produces here.
/// </summary>
public static class OtpCode
{
    public const int Length = 6;

    public static string Generate()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[input.Length];
        var count = 0;

        foreach (var raw in input)
        {
            if (char.IsWhiteSpace(raw) || raw is '-' or '\u200C')
            {
                continue;
            }

            var c = ToLatinDigit(raw);

            if (!char.IsAsciiDigit(c))
            {
                return string.Empty;
            }

            buffer[count++] = c;
        }

        return new string(buffer[..count]);
    }

    public static bool IsWellFormed(string? normalized)
        => normalized is { Length: Length } && normalized.All(char.IsAsciiDigit);

    private static char ToLatinDigit(char c) => c switch
    {
        >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')),
        >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')),
        _ => c,
    };
}
