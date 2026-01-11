using System.Diagnostics;

namespace TerraformApi.Services;

public class TerraformService
{
    private readonly IConfiguration _configuration;

    public TerraformService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ExecuteCommand(string environment, string command, string args)
    {
        var envConfig = _configuration.GetSection($"Environments:{environment}");
        if (!envConfig.Exists())
        {
            throw new ArgumentException($"Environment '{environment}' not found in configuration.");
        }

        var workingDir = envConfig["WorkingDirectory"];
        if (string.IsNullOrEmpty(workingDir) || !Directory.Exists(workingDir))
        {
            throw new DirectoryNotFoundException($"Working directory '{workingDir}' does not exist.");
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = "terraform",
            Arguments = $"{command} {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };

        using var process = new Process();
        process.StartInfo = processInfo;
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Terraform command failed: {error}");
        }

        return output;
    }

    public string Init(string environment)
    {
        return ExecuteCommand(environment, "init", "-upgrade");
    }

    public string Plan(string environment)
    {
        var varFile = _configuration[$"Environments:{environment}:VarFile"];
        var args = !string.IsNullOrEmpty(varFile) ? $"-var-file={varFile}" : "";
        return ExecuteCommand(environment, "plan", args);
    }

    public string Apply(string environment)
    {
        var varFile = _configuration[$"Environments:{environment}:VarFile"];
        var args = !string.IsNullOrEmpty(varFile) ? $"-var-file={varFile}" : "";
        return ExecuteCommand(environment, "apply", $"-auto-approve {args}");
    }
}
