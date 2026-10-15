using System.Buffers.Text;
using System.Security.Cryptography;

namespace MMV.Application.Identity;

/// <summary>
/// Jetons de session opaques : 256 bits issus du générateur cryptographique du système, encodés en
/// Base64Url (43 caractères). Seul le SHA-256 du jeton est persisté.
/// </summary>
public static class SessionTokens
{
    public const int TokenBytes = 32;

    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>SHA-256 des octets décodés ; <c>null</c> si le jeton n'a pas le format attendu.</summary>
    public static byte[]? TryHash(string token)
    {
        if (token.Length != 43)
        {
            return null;
        }

        Span<byte> raw = stackalloc byte[TokenBytes];
        if (Base64Url.DecodeFromChars(token, raw, out var consumed, out var written) != System.Buffers.OperationStatus.Done
            || consumed != token.Length
            || written != TokenBytes)
        {
            return null;
        }

        return SHA256.HashData(raw);
    }
}
