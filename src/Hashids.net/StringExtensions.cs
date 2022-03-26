using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace HashidsNetCore;

public static class StringExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpanMultiTokenizer<char> TokenizeAny(this string text, ReadOnlySpan<char> separator) =>
        new(text, separator);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpanMultiTokenizer<char> TokenizeAny(this ReadOnlySpan<char> text, ReadOnlySpan<char> separator) =>
        new(text, separator);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountAny(this ReadOnlySpan<char> text, ReadOnlySpan<char> separator)
    {
        var total = 0;
        foreach (var tok in text.TokenizeAny(separator))
        {
            if (tok.Length > 0)
            {
                total++;
            }
        }

        return total;
    }
}
