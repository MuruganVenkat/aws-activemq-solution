using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Apache.NMS;
using Apache.NMS.STOMP;

namespace AmazonMQ.AcceptanceTests;

/// <summary>
/// Creates authenticated NMS connections to the Amazon MQ ActiveMQ broker
/// over STOMP+TLS (port 61614).
/// </summary>
public sealed class BrokerConnectionFactory : IDisposable
{
    private IConnection? _connection;
    private bool _disposed;

    // Amazon MQ STOMP+SSL endpoint format:  stomp+ssl://host:61614
    private readonly Uri _brokerUri;
    private readonly string _username;
    private readonly string _password;

    public BrokerConnectionFactory(string endpoint, string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, nameof(endpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(username, nameof(username));
        ArgumentException.ThrowIfNullOrWhiteSpace(password, nameof(password));

        _username  = username;
        _password  = password;

        // Normalise the endpoint AWS returns (ssl:// → stomp+ssl://)
        var normalised = endpoint
            .Replace("ssl://", "stomp+ssl://", StringComparison.OrdinalIgnoreCase);

        // Append transport options accepted by Apache.NMS.STOMP
        _brokerUri = new Uri(
            $"{normalised}?transport.acceptInvalidBrokerCert=true" +
            $"&transport.clientCertSubject=");
    }

    /// <summary>
    /// Opens a connection and starts it so message delivery begins immediately.
    /// </summary>
    public IConnection CreateAndStartConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var factory    = new ConnectionFactory(_brokerUri);
        _connection    = factory.CreateConnection(_username, _password);
        _connection.Start();
        return _connection;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _connection?.Stop();  } catch { /* best-effort */ }
        try { _connection?.Close(); } catch { /* best-effort */ }
        try { _connection?.Dispose(); } catch { /* best-effort */ }
    }
}
