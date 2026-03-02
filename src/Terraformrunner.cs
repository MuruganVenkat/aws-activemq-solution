using System.Text;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;

namespace AmazonMQ.AcceptanceTests;

/// <summary>
/// Thin wrapper that drives the <c>terraform</c> CLI for provisioning
/// and tear-down during acceptance tests.
/// </summary>
public sealed class TerraformRunner : IAsyncDisposable
{
    private readonly string _workingDir;
    private bool _applied;

    public TerraformRunner(string workingDir)
    {
        _workingDir = Path.GetFullPath(workingDir);
    }

    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>Runs <c>terraform init</c>.</summary>
    public Task InitAsync(CancellationToken ct = default) =>
        RunAsync(["init", "-no-color", "-input=false"], ct);

    /// <summary>Runs <c>terraform apply</c> and sets the applied flag.</summary>
    public async Task ApplyAsync(CancellationToken ct = default)
    {
        await RunAsync(["apply", "-auto-approve", "-no-color", "-input=false"], ct);
        _applied = true;
    }

    /// <summary>Runs <c>terraform destroy</c> if apply was previously called.</summary>
    public async Task DestroyAsync(CancellationToken ct = default)
    {
        if (!_applied) return;
        await RunAsync(["destroy", "-auto-approve", "-no-color", "-input=false"], ct);
        _applied = false;
    }

    /// <summary>
    /// Returns the string value of a Terraform output variable.
    /// </summary>
    public async Task<string> OutputAsync(string name, CancellationToken ct = default)
    {
        var result = await Cli.Wrap("terraform")
            .WithArguments(["output", "-raw", name])
            .WithWorkingDirectory(_workingDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"terraform output {name} failed (exit {result.ExitCode}):\n{result.StandardError}");

        return result.StandardOutput.Trim();
    }

    /// <summary>
    /// Returns all outputs as a <see cref="JsonDocument"/> so callers can
    /// inspect the full output map when needed.
    /// </summary>
    public async Task<JsonDocument> OutputJsonAsync(CancellationToken ct = default)
    {
        var result = await Cli.Wrap("terraform")
            .WithArguments(["output", "-json"])
            .WithWorkingDirectory(_workingDir)
            .ExecuteBufferedAsync(ct);

        return JsonDocument.Parse(result.StandardOutput);
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DestroyAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[TerraformRunner] Destroy failed during dispose: {ex.Message}");
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RunAsync(string[] args, CancellationToken ct)
    {
        var sb = new StringBuilder();

        var result = await Cli.Wrap("terraform")
            .WithArguments(args)
            .WithWorkingDirectory(_workingDir)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(sb))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(sb))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(ct);

        Console.WriteLine(sb.ToString());

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"terraform {args[0]} exited with code {result.ExitCode}.\n{sb}");
    }
}
