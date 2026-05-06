using System.Security.Cryptography;

namespace DashboardApi.Services;

/// <summary>DPAPI encrypt/decrypt with application-specific entropy (salt).</summary>
public static class CryptoService
{
    private const string Prefix = "dpapi2:";

    // Application-specific entropy so only this app can decrypt under the same user.
    private static readonly byte[] Entropy = "ProjectMaster.DashboardApi.2024"u8.ToArray();

    public static string Encrypt(string plain)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(plain);
        var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(encrypted);
    }

    public static string Decrypt(string token)
    {
        if (string.IsNullOrEmpty(token)) return token;

        if (token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var raw = Convert.FromBase64String(token[Prefix.Length..]);
            var decrypted = ProtectedData.Unprotect(raw, Entropy, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }

        throw new InvalidOperationException("Unencrypted secret detected. Store credentials using DPAPI-encrypted values only.");
    }

    public static bool IsEncrypted(string token) =>
        token.StartsWith(Prefix, StringComparison.Ordinal);
}
