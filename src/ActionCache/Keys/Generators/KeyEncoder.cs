using System.Text;

namespace ActionCache.Common.Keys;

/// <summary>
/// Provides methods to encode and decode strings to and from hexadecimal format using UTF-8 encoding.
/// This is a reversible encoding, not encryption; encoded values are not confidential.
/// </summary>
public class KeyEncoder
{
    /// <summary>
    /// Encodes the specified string into its hexadecimal representation.
    /// </summary>
    /// <param name="value">The string to encode.</param>
    /// <returns>A hexadecimal string representation of the input.</returns>
    public string Encode(string value) =>
        Convert.ToHexString(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Decodes the specified hexadecimal string back into its original string using UTF-8 encoding.
    /// </summary>
    /// <param name="value">The hexadecimal string to decode.</param>
    /// <returns>The original string represented by the hexadecimal input.</returns>
    /// <exception cref="FormatException">
    /// Thrown if the input string is not a valid hexadecimal representation.
    /// </exception>
    public string Decode(string value) =>
        Encoding.UTF8.GetString(Convert.FromHexString(value));
}