namespace HashidsNetCore;

/// <summary>
/// Describes a Hashids provider
/// </summary>
public interface IHashids
{
    /// <summary>
    /// Decodes the provided hash into a number.
    /// </summary>
    /// <param name="hash">Hash string to decode.</param>
    /// <returns>32-bit integer.</returns>
    /// <exception cref="T:System.OverflowException">If the decoded number overflows integer.</exception>
    int DecodeSingle(string hash);

    /// <summary>
    /// Decodes the provided hashed string.
    /// </summary>
    /// <param name="hash">the hashed string</param>
    /// <exception cref="T:System.OverflowException">if one or many of the numbers in the hash overflowing the integer storage</exception>
    /// <returns>the numbers</returns>
    int[] Decode(string hash);

    /// <summary>
    /// Decodes the provided hashed string into longs
    /// </summary>
    /// <param name="hash">the hashed string</param>
    /// <returns>the numbers</returns>
    long[] DecodeLong(string hash);

    /// <summary>
    /// Decodes the provided hash into a single number.
    /// </summary>
    /// <param name="hash">Hash string to decode.</param>
    /// <returns>64-bit integer or 0 if the value could be decoded.</returns>
    /// /// <exception cref="T:System.ArgumentOutOfRangeException">If the hash represents more than one number.</exception>
    long DecodeSingleLong(string hash);

    /// <summary>
    /// Decodes the provided hashed string into a hex string
    /// </summary>
    /// <param name="hash">the hashed string</param>
    /// <returns>the hex string</returns>
    string DecodeHex(string hash);

    /// <summary>
    /// Encodes the provided number into a hash string.
    /// </summary>
    /// <param name="number">32-bit integer.</param>
    /// <returns>Encoded hash string.</returns>
    string Encode(int number);

    /// <summary>
    /// Encodes the provided numbers into a hashed string
    /// </summary>
    /// <param name="numbers">the numbers</param>
    /// <returns>the hashed string</returns>
    string Encode(params int[] numbers);

    /// <summary>
    /// Encodes the provided number into a hash string.
    /// </summary>
    /// <param name="number">64-bit integer.</param>
    /// <returns>Encoded hash string.</returns>
    string EncodeLong(long number);

    /// <summary>
    /// Encodes the provided numbers into a hashed string
    /// </summary>
    /// <param name="numbers">the numbers</param>
    /// <returns>the hashed string</returns>
    string EncodeLong(params long[] numbers);

    /// <summary>
    /// Encodes the provided hex string
    /// </summary>
    /// <param name="hex">the hex string</param>
    /// <returns>the hashed string</returns>
    string EncodeHex(string hex);
}
