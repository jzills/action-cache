using System.Security.Cryptography;
using System.Text;

namespace ActionCache.Common.Keys;

/// <summary>
/// Provides functionality to generate a SHA-256 hash for a given string value.
/// </summary>
internal static class KeyHashGenerator
{
    /// <summary>
    /// Generates a SHA-256 hash for the specified string and returns it as a hexadecimal string.
    /// </summary>
    /// <param name="value">The input string to hash.</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash.</returns>
    internal static string ToHash(string value) =>
        Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
}