using System.Net.Sockets;
using Polly;
using Polly.Retry;

namespace AmazonMQ.AcceptanceTests;

/// <summary>
/// Polls the broker's TCP port until it accepts connections, mirroring the
/// Go <c>waitForBrokerReady</c> helper that used <c>retry.DoWithRetry</c>.
/// </summary>
public static class BrokerReadinessPoller
{
    private const int MaxAttempts       = 40;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Parses <paramref name="endpoint"/> (e.g. <c>ssl://host:61617</c>) and
    /// retries a TCP connection until it succeeds or <paramref name="timeout"/>
    /// is exceeded.
    /// </summary>
    public static async Task WaitForBrokerReadyAsync(
        string endpoint,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var (host, port) = ParseEndpoint(endpoint);

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = MaxAttempts,
                Delay            = RetryDelay,
                BackoffType      = DelayBackoffType.Constant,
                ShouldHandle     = new PredicateBuilder().Handle<Exception>(),
                OnRetry          = static args =>
                {
                    Console.WriteLine(
                        $"[BrokerReadinessPoller] Attempt {args.AttemptNumber + 1}/{MaxAttempts} – " +
                        $"broker not yet reachable: {args.Outcome.Exception?.Message}");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(timeout)
            .Build();

        await pipeline.ExecuteAsync(async innerCt =>
        {
            using var tcpClient = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(innerCt);
            cts.CancelAfter(ConnectTimeout);
            await tcpClient.ConnectAsync(host, port, cts.Token);
            Console.WriteLine($"[BrokerReadinessPoller] Broker is reachable at {host}:{port}");
        }, ct);
    }

    /// <summary>
    /// Strips the scheme prefix and splits <c>host:port</c>.
    /// Handles <c>ssl://</c> and <c>stomp+ssl://</c> prefixes returned by AWS.
    /// </summary>
    internal static (string Host, int Port) ParseEndpoint(string endpoint)
    {
        var stripped = endpoint
            .Replace("stomp+ssl://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("ssl://",       string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("stomp://",     string.Empty, StringComparison.OrdinalIgnoreCase);

        var lastColon = stripped.LastIndexOf(':');
        if (lastColon < 0)
            throw new FormatException($"Cannot parse endpoint '{endpoint}': no port separator found.");

        var host = stripped[..lastColon];
        var port = int.Parse(stripped[(lastColon + 1)..]);
        return (host, port);
    }
}
