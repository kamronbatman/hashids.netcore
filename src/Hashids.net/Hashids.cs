using System.Collections.Generic;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Hashids.net;

namespace HashidsNet;

/// <summary>
/// Generates YouTube-like hashes from one or many numbers. Use hashids when you do not want to expose your database ids to the user.
/// </summary>
public class Hashids : IHashids
{
    public const string DEFAULT_ALPHABET = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
    public const string DEFAULT_SEPS = "cfhistuCFHISTU";
    public const int MIN_ALPHABET_LENGTH = 16;

    private const double SEP_DIV = 3.5;
    private const double GUARD_DIV = 12.0;

    private const int MaxNumberHashLength = 12; // Length of long.MaxValue;

    private readonly string _alphabet;
    private readonly char[] _seps;
    private readonly char[] _guards;
    private readonly string _salt;
    private readonly int _minHashLength;

    /// <summary>
    /// Instantiates a new Hashids encoder/decoder with defaults.
    /// </summary>
    public Hashids() : this(salt: string.Empty, minHashLength: 0, alphabet: DEFAULT_ALPHABET, seps: DEFAULT_SEPS)
    {
        // empty constructor with defaults needed to allow mocking of public methods
    }

    /// <summary>
    /// Instantiates a new Hashids encoder/decoder.
    /// All parameters are optional and will use defaults unless otherwise specified.
    /// </summary>
    /// <param name="salt"></param>
    /// <param name="minHashLength"></param>
    /// <param name="alphabet"></param>
    /// <param name="seps"></param>
    public Hashids(
        string salt = "",
        int minHashLength = 0,
        string alphabet = DEFAULT_ALPHABET,
        string seps = DEFAULT_SEPS)
    {
        if (salt == null)
        {
            throw new ArgumentNullException(nameof(salt));
        }

        if (string.IsNullOrWhiteSpace(alphabet))
        {
            throw new ArgumentNullException(nameof(alphabet));
        }

        if (minHashLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minHashLength), "Value must be zero or greater.");
        }

        if (string.IsNullOrWhiteSpace(seps))
        {
            throw new ArgumentNullException(nameof(seps));
        }

        _salt = salt.Trim();
        _minHashLength = minHashLength;

        InitCharArrays(alphabet: alphabet, seps: seps, salt: _salt, alphabetChars: out _alphabet, sepChars: out _seps, guardChars: out _guards);

        if (_salt.Length >= _alphabet.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(salt), $"Salt must be less than {_alphabet.Length} characters.");
        }
    }

    /// <remarks>This method uses <c>out</c> params instead of returning a ValueTuple so it works with .NET 4.6.1.</remarks>
    private static void InitCharArrays(string alphabet, string seps, ReadOnlySpan<char> salt, out string alphabetChars, out char[] sepChars, out char[] guardChars)
    {
        var alphabetHash = new HashSet<char>();
        foreach (var letter in alphabet)
        {
            alphabetHash.Add(letter);
        }

        if (alphabetHash.Count < MIN_ALPHABET_LENGTH)
        {
            throw new ArgumentException($"Alphabet must contain at least {MIN_ALPHABET_LENGTH} unique characters.", paramName: nameof(alphabet));
        }

        var sepHash = new HashSet<char>();
        foreach (var letter in seps)
        {
            if (alphabetHash.Contains(letter))
            {
                sepHash.Add(letter);
                alphabetHash.Remove(letter);
            }
        }

        if (alphabetHash.Count < MIN_ALPHABET_LENGTH - 6)
        {
            throw new ArgumentException($"Alphabet must contain at least {MIN_ALPHABET_LENGTH} unique characters that are also not present in .", paramName: nameof(seps));
        }

        // Use this stack alloc alphabet for mutation
        Span<char> alphabetBuffer = stackalloc char[alphabetHash.Count];
        Span<char> sepsBuffer = stackalloc char[sepHash.Count];
        var index = 0;
        foreach (var letter in alphabetHash)
        {
            alphabetBuffer[index++] = letter;
        }

        index = 0;
        foreach (var letter in sepHash)
        {
            sepsBuffer[index++] = letter;
        }

        ConsistentShuffle(alphabet: sepsBuffer, alphabetLength: sepsBuffer.Length, salt: salt, saltLength: salt.Length);

        if (sepsBuffer.Length == 0 || (float)alphabetBuffer.Length / sepsBuffer.Length > SEP_DIV)
        {
            var sepsLength = Math.Max(2, (int)Math.Ceiling((float)alphabetBuffer.Length / SEP_DIV));

            if (sepsLength > sepsBuffer.Length)
            {
                var diff = sepsLength - sepsBuffer.Length;
                Span<char> adjustedSeps = stackalloc char[sepsBuffer.Length + diff];
                sepsBuffer.CopyTo(adjustedSeps);
                alphabetBuffer[..diff].CopyTo(adjustedSeps[sepsBuffer.Length..]);
                alphabetBuffer = alphabetBuffer[diff..];
                sepsBuffer = adjustedSeps;
            }
            else
            {
                sepsBuffer = sepsBuffer[..sepsLength];
            }
        }

        ConsistentShuffle(alphabet: alphabetBuffer, alphabetBuffer.Length, salt: salt, salt.Length);

        var guardCount = (int)Math.Ceiling(alphabetBuffer.Length / GUARD_DIV);

        if (alphabetBuffer.Length < 3)
        {
            guardChars = sepsBuffer[..guardCount].ToArray();
            sepChars   = sepsBuffer[guardCount..].ToArray();
            alphabetChars = alphabetBuffer.ToString();
            return;
        }

        guardChars = alphabetBuffer[..guardCount].ToArray();
        alphabetChars = alphabetBuffer[guardCount..].ToString();
        sepChars = sepsBuffer.ToArray();
    }

    /// <summary>
    /// Encodes the provided numbers into a hash string.
    /// </summary>
    /// <param name="numbers">List of integers.</param>
    /// <returns>Encoded hash string.</returns>
    public string Encode(params int[] numbers) => GenerateHashFrom(Array.ConvertAll(numbers, n => (long)n));

    /// <summary>
    /// Encodes the provided numbers into a hash string.
    /// </summary>
    /// <param name="numbers">Enumerable list of integers.</param>
    /// <returns>Encoded hash string.</returns>
    public string Encode(IEnumerable<int> numbers) => Encode(numbers.ToArray());

    /// <summary>
    /// Encodes the provided numbers into a hash string.
    /// </summary>
    /// <param name="numbers">List of 64-bit integers.</param>
    /// <returns>Encoded hash string.</returns>
    public string EncodeLong(params long[] numbers) => GenerateHashFrom(numbers);

    /// <summary>
    /// Encodes the provided numbers into a hash string.
    /// </summary>
    /// <param name="numbers">Enumerable list of 64-bit integers.</param>
    /// <returns>Encoded hash string.</returns>
    public string EncodeLong(IEnumerable<long> numbers) => EncodeLong(numbers.ToArray());

    /// <summary>
    /// Decodes the provided hash into numbers.
    /// </summary>
    /// <param name="hash">Hash string to decode.</param>
    /// <returns>Array of integers.</returns>
    /// <exception cref="T:System.OverflowException">If the decoded number overflows integer.</exception>
    public int[] Decode(string hash) => Array.ConvertAll(GetNumbersFrom(hash), n => (int)n);

    /// <summary>
    /// Decodes the provided hash into numbers.
    /// </summary>
    /// <param name="hash">Hash string to decode.</param>
    /// <returns>Array of 64-bit integers.</returns>
    public long[] DecodeLong(string hash) => GetNumbersFrom(hash);

    /// <summary>
    /// Encodes the provided hex-string into a hash string.
    /// </summary>
    /// <param name="hex">Hex string to encode.</param>
    /// <returns>Encoded hash string.</returns>
    public string EncodeHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return string.Empty;
        }

        foreach (var c in hex)
        {
            if (c < 48 || c > 57 && c < 65 || c > 70 && c < 97 || c > 102)
            {
                return string.Empty;
            }
        }

        var length = Math.DivRem(hex.Length, 12, out var partial);

        Span<long> longs = stackalloc long[length + (partial > 0 ? 1 : 0)];
        hex.GetLongs(longs);

        return GenerateHashFrom(longs);
    }

    /// <summary>
    /// Decodes the provided hash into a hex-string.
    /// </summary>
    /// <param name="hash">Hash string to decode.</param>
    /// <returns>Decoded hex string.</returns>
    public string DecodeHex(string hash)
    {
        var rawCharHash = GetRawCharHash(hash, out var lottery, out var hashLength);
        Span<long> numbers = stackalloc long[hashLength];

        return GetBytesFromRawCharHash(hash, rawCharHash, lottery, numbers) ? numbers.ToHexString() : string.Empty;
    }

    private string GenerateHashFrom(ReadOnlySpan<long> numbers)
    {
        if (numbers.Length == 0)
        {
            return string.Empty;
        }

        foreach (var number in numbers)
        {
            if (number < 0)
            {
                return string.Empty;
            }
        }

        long numbersHashInt = 0;
        for (var i = 0; i < numbers.Length; i++)
        {
            numbersHashInt += numbers[i] % (i + 100);
        }

        var builder = new ValueStringBuilder(stackalloc char[16]);

        // We are going to mutate the alphabet, so we need a copy
        var originalAlphabet = _alphabet.AsSpan();
        Span<char> alphabet = stackalloc char[originalAlphabet.Length];
        originalAlphabet.CopyTo(alphabet);

        Span<char> shuffleBuffer = stackalloc char[originalAlphabet.Length];
        Span<char> hashBuffer = stackalloc char[originalAlphabet.Length];

        var lottery = alphabet[(int)(numbersHashInt % alphabet.Length)];
        builder.Append(lottery);
        PrepareBuffer(shuffleBuffer, lottery);

        var startIndex = 1 + _salt.Length;
        var length = alphabet.Length - startIndex;

        for (var i = 0; i < numbers.Length; i++)
        {
            var number = numbers[i];

            if (length > 0)
            {
                alphabet[..length].CopyTo(shuffleBuffer.Slice(startIndex, length));
            }

            ConsistentShuffle(alphabet, alphabet.Length, shuffleBuffer, alphabet.Length);
            var hashLength = BuildReversedHash(number, alphabet, hashBuffer);
            builder.Append(hashBuffer[hashLength..]);

            // Don't append the last one?
            if (i + 1 >= numbers.Length)
            {
                continue;
            }

            number %= hashBuffer[^1] + i;
            var sepsIndex = (int)(number % _seps.Length);

            builder.Append(_seps[sepsIndex]);
        }

        if (builder.Length < _minHashLength)
        {
            var guardIndex = (int)((numbersHashInt + builder[0]) % _guards.Length);
            var guard = _guards[guardIndex];

            builder.Insert(0, guard);

            if (builder.Length < _minHashLength)
            {
                guardIndex = (int)((numbersHashInt + builder[2]) % _guards.Length);
                guard = _guards[guardIndex];

                builder.Append(guard);
            }
        }

        var halfLength = alphabet.Length / 2;

        while (builder.Length < _minHashLength)
        {
            alphabet.CopyTo(shuffleBuffer);
            ConsistentShuffle(alphabet, alphabet.Length, shuffleBuffer, alphabet.Length);
            builder.Insert(0, alphabet.Slice(halfLength, alphabet.Length - halfLength));
            builder.Append(alphabet[..halfLength]);

            var excess = builder.Length - _minHashLength;
            if (excess > 0)
            {
                builder.Remove(0, excess / 2);
                builder.Remove(_minHashLength, builder.Length - _minHashLength);
            }
        }

        var result = builder.ToString();
        builder.Dispose();
        return result;
    }

    private int BuildReversedHash(long input, ReadOnlySpan<char> alphabet, Span<char> buffer)
    {
        var length = buffer.Length;
        do
        {
            input = Math.DivRem(input, _alphabet.Length, out var index);
            var chr = alphabet[(int)index];
            buffer[--length] = chr;
        }
        while (input > 0);

        return length;
    }

    private long Unhash(ReadOnlySpan<char> input, ReadOnlySpan<char> alphabet)
    {
        long number = 0;

        foreach (var t in input)
        {
            var pos = alphabet.IndexOf(t);
            number = number * _alphabet.Length + pos;
        }

        return number;
    }

    private ReadOnlySpan<char> GetRawCharHash(string hash, out char lottery, out int hashLength)
    {
        hashLength = 0;
        lottery = '\0';
        if (string.IsNullOrWhiteSpace(hash))
        {
            return ReadOnlySpan<char>.Empty;
        }

        var totalTokens = 0;
        ReadOnlySpan<char> hashBreakdown0 = null;
        ReadOnlySpan<char> hashBreakdown = null;
        foreach (var tok in hash.TokenizeAny(_guards))
        {
            if (tok.Length == 0)
            {
                continue;
            }

            totalTokens++;

            if (totalTokens > 3)
            {
                hashBreakdown = hashBreakdown0;
                break;
            }

            switch (totalTokens)
            {
                case 1:
                    {
                        hashBreakdown0 = tok; // Store this, if we have more than 3 tokens, we revert back.
                        hashBreakdown = tok;
                        break;
                    }
                case 2:
                    {
                        hashBreakdown = tok;
                        break;
                    }
            }
        }

        if (totalTokens == 0)
        {
            return ReadOnlySpan<char>.Empty;
        }

        lottery = hashBreakdown[0];
        // TODO: CountAny is O(N), look into a better way of doing this
        hashLength = hashBreakdown.CountAny(_seps);

        return lottery == '\0' ? ReadOnlySpan<char>.Empty : hashBreakdown[1..];
    }

    private bool GetBytesFromRawCharHash(string hash, ReadOnlySpan<char> rawCharHash, char lottery, Span<long> numbers)
    {
        var originalAlphabet = _alphabet.AsSpan();
        Span<char> alphabet = stackalloc char[originalAlphabet.Length];
        originalAlphabet.CopyTo(alphabet);

        Span<char> buffer = stackalloc char[_alphabet.Length];
        PrepareBuffer(buffer, lottery);

        var index = 0;
        var startIndex = 1 + _salt.Length;
        var length = alphabet.Length - startIndex;

        foreach (var tok in rawCharHash.TokenizeAny(_seps))
        {
            if (tok.Length == 0)
            {
                continue;
            }

            if (length > 0)
            {
                alphabet[..length].CopyTo(buffer.Slice(startIndex, length));
            }

            ConsistentShuffle(alphabet, alphabet.Length, buffer, alphabet.Length);
            var number = Unhash(tok, alphabet);
            numbers[index++] = number;
        }

        return GenerateHashFrom(numbers) == hash;
    }

    private long[] GetNumbersFrom(string hash)
    {
        var rawCharHash = GetRawCharHash(hash, out var lottery, out var hashLength);
        if (rawCharHash.Length == 0)
        {
            return Array.Empty<long>();
        }

        var originalAlphabet = _alphabet.AsSpan();
        Span<char> alphabet = stackalloc char[originalAlphabet.Length];
        originalAlphabet.CopyTo(alphabet);

        Span<char> buffer = stackalloc char[_alphabet.Length];
        PrepareBuffer(buffer, lottery);

        var result = new long[hashLength];

        var index = 0;
        var startIndex = 1 + _salt.Length;
        var length = alphabet.Length - startIndex;

        foreach (var tok in rawCharHash.TokenizeAny(_seps))
        {
            if (tok.Length == 0)
            {
                continue;
            }

            if (length > 0)
            {
                alphabet[..length].CopyTo(buffer.Slice(startIndex, length));
            }

            ConsistentShuffle(alphabet, alphabet.Length, buffer, alphabet.Length);
            result[index++] = Unhash(tok, alphabet);
        }

        return GenerateHashFrom(result) == hash ? result : Array.Empty<long>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareBuffer(Span<char> buffer, char lottery)
    {
        buffer[0] = lottery;
        _salt.AsSpan().CopyTo(buffer[1..]);
    }

    /// <summary>NOTE: This method mutates the <paramref name="alphabet"/> argument in-place.</summary>
    private static void ConsistentShuffle(Span<char> alphabet, int alphabetLength, ReadOnlySpan<char> salt, int saltLength)
    {
        if (salt.Length == 0)
        {
            return;
        }

        // TODO: Document or rename these cryptically-named variables: i, v, p, n.
        for (int i = alphabetLength - 1, v = 0, p = 0; i > 0; i--, v++)
        {
            v %= saltLength;
            int n = salt[v];
            p += n;
            var j = (n + v + p) % i;

            // swap characters at positions i and j:
            (alphabet[j], alphabet[i]) = (alphabet[i], alphabet[j]);
        }
    }
}
