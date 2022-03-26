using System.Runtime.CompilerServices;

namespace Hashids.net;

public static class NumberExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(this uint value)
    {
        var digits = 1;
        if (value >= 100000)
        {
            value /= 100000;
            digits += 5;
        }

        if (value < 10)
        {
            // no-op
        }
        else if (value < 100)
        {
            digits++;
        }
        else if (value < 1000)
        {
            digits += 2;
        }
        else if (value < 10000)
        {
            digits += 3;
        }
        else
        {
            digits += 4;
        }

        return digits;
    }
}
