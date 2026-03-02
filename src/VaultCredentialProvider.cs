using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace AmazonMQ.AcceptanceTests;

/// <summary>
/// Reads ActiveMQ broker credentials from a HashiCorp Vault KV store.
/// </summary>
public sealed class VaultCredentialProvider
{
    private readonly IVaultClient _client;
    private readonly string _secretPath;

    public VaultCredentialProvider(string vaultAddress, string vaultToken, string secretPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultAddress,  nameof(vaultAddress));
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultToken,    nameof(vaultToken));
        ArgumentException.ThrowIfNullOrWhiteSpace(secretPath,    nameof(secretPath));

        _secretPath = secretPath;

        var authMethod = new TokenAuthMethodInfo(vaultToken);
        var settings   = new VaultClientSettings(vaultAddress, authMethod);
        _client        = new VaultClient(settings);
    }

    /// <summary>
    /// Returns the (username, password) pair stored at the configured secret path.
    /// Supports both KV v1 and KV v2 paths.
    /// </summary>
    public async Task<(string Username, string Password)> GetCredentialsAsync(
        CancellationToken ct = default)
    {
        // Try KV v2 first, then fall back to KV v1.
        IDictionary<string, object> data;
        try
        {
            Secret<SecretData> secret = await _client.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(_secretPath, mountPoint: "secret")
                .WaitAsync(ct);
            data = secret.Data.Data;
        }
        catch
        {
            Secret<Dictionary<string, object>> secret = await _client.V1.Secrets.KeyValue.V1
                .ReadSecretAsync(_secretPath, mountPoint: "secret")
                .WaitAsync(ct);
            data = secret.Data;
        }

        if (!data.TryGetValue("username", out var u) || u is not string username || username == string.Empty)
            throw new InvalidOperationException(
                $"Vault secret at '{_secretPath}' is missing a non-empty 'username' key.");

        if (!data.TryGetValue("password", out var p) || p is not string password || password == string.Empty)
            throw new InvalidOperationException(
                $"Vault secret at '{_secretPath}' is missing a non-empty 'password' key.");

        return (username, password);
    }
}
