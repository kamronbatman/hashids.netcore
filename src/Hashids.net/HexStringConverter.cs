using System;
using System.Buffers.Binary;

namespace HashidsNetCore;

public static class HexStringConverter
{
    private static readonly uint[] Lookup32Chars = CreateLookup32Chars();

    private static uint[] CreateLookup32Chars()
    {
        var result = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var s = i.ToString("X2");
            if (BitConverter.IsLittleEndian)
            {
                result[i] = s[0] + ((uint)s[1] << 16);
            }
            else
            {
                result[i] = s[1] + ((uint)s[0] << 16);
            }
        }

        return result;
    }

    public static string ToHexString(this Span<long> bytes) => ((ReadOnlySpan<long>)bytes).ToHexString();

    public static unsafe string ToHexString(this ReadOnlySpan<long> longs)
    {
        Span<char> result = stackalloc char[longs.Length * 13];
        Span<byte> numBuffer = stackalloc byte[8];
        var length = 0;
        var partials = 0;

        fixed (char* resultP = result)
        {
            var resultP2 = (uint*)resultP;
            foreach (var num in longs)
            {
                BinaryPrimitives.WriteInt64BigEndian(numBuffer, num);

                var firstDigit = true;
                for (var n = 0; n < 8; n++)
                {
                    if (firstDigit && numBuffer[n] == 0)
                    {
                        continue;
                    }

                    var lookup = Lookup32Chars[numBuffer[n]];
                    if (firstDigit)
                    {
                        if (numBuffer[n] > 0x10)
                        {
                            var newCharP = (char*)resultP2;
                            newCharP[0] = (char)(lookup >> 0x10);
                            resultP2 = (uint*)(newCharP + 1); // Reset pointer
                            partials++; // Track single characters
                        }

                        firstDigit = false;
                    }
                    else
                    {
                        resultP2[length++] = lookup;
                    }
                }
            }
        }

        return result[..(length * 2 + partials)].ToString();
    }

    public static unsafe void GetLongs(this string str, Span<long> longs)
    {
        var totalLongs = 0;

        fixed (char* strP = str)
        {
            var i = 0;
            Span<byte> buffer = stackalloc byte[8];

            while (i < str.Length)
            {
                var charCount = Math.Min(12, str.Length - i);
                var isEvenCount = charCount % 2 == 0;
                var bIndex = 8 - charCount / 2;

                // If even, then we add 01 to the first byte
                if (!isEvenCount)
                {
                    charCount++;
                    bIndex--;
                }
                else
                {
                    buffer[bIndex - 1] = 1;
                }

                for (var c = 0; c < charCount; c += 2)
                {
                    int chr1 = c == 0 && !isEvenCount ? '1' : strP[i++];
                    if (chr1 is >= 'a' and <= 'f')
                    {
                        chr1 = chr1 - 'a' + 'A';
                    }

                    int chr2 = strP[i++];
                    if (chr2 is >= 'a' and <= 'f')
                    {
                        chr2 = chr2 - 'a' + 'A';
                    }

                    if (BitConverter.IsLittleEndian)
                    {
                        buffer[bIndex++] = (byte)(((chr1 - (chr1 >= 65 ? 55 : 48)) << 4) | (chr2 - (chr2 >= 65 ? 55 : 48)));
                    }
                    else
                    {
                        buffer[bIndex++] = (byte)((chr1 - (chr1 >= 65 ? 55 : 48)) | ((chr2 - (chr2 >= 65 ? 55 : 48)) << 4));
                    }
                }

                longs[totalLongs++] = BinaryPrimitives.ReadInt64BigEndian(buffer);
                buffer.Clear();
            }
        }
    }
}
