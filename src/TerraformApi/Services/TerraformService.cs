using System.Diagnostics;
using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using TerraformApi.Models;

namespace TerraformApi.Services;

public class TerraformService
{
    private readonly IConfiguration _configuration;
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<TerraformService> _logger;

    public TerraformService(
        IConfiguration configuration,
        IAmazonSecretsManager secretsManager,
        ILogger<TerraformService> logger)
    {
        _configuration = configuration;
        _secretsManager = secretsManager;
        _logger = logger;
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
        var result = ExecuteCommand(environment, "apply", $"-auto-approve {args}");

        // After successful apply, retrieve outputs and push OpenWire URLs to Secrets Manager
        try
        {
            var outputs = GetTerraformOutputAsync(environment).GetAwaiter().GetResult();
            if (outputs?.OpenwireSslUrls?.Value != null && outputs.OpenwireSslUrls.Value.Any())
            {
                PushOpenwireUrlsToSecretsManagerAsync(environment, outputs).GetAwaiter().GetResult();
                _logger.LogInformation("Successfully pushed OpenWire SSL URLs to Secrets Manager");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push OpenWire URLs to Secrets Manager");
            // Don't fail the entire apply if Secrets Manager update fails
        }

        return result;
    }

    public async Task<TerraformOutput?> GetTerraformOutputAsync(string environment)
    {
        try
        {
            var outputJson = ExecuteCommand(environment, "output", "-json");
            var outputs = JsonSerializer.Deserialize<TerraformOutput>(outputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return outputs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Terraform outputs");
            throw;
        }
    }

    public async Task PushOpenwireUrlsToSecretsManagerAsync(string environment, TerraformOutput outputs)
    {
        if (outputs.OpenwireSslUrls?.Value == null || !outputs.OpenwireSslUrls.Value.Any())
        {
            _logger.LogWarning("No OpenWire SSL URLs found in Terraform outputs");
            return;
        }

        var brokerName = _configuration[$"Environments:{environment}:BrokerName"];
        if (string.IsNullOrEmpty(brokerName))
        {
            throw new InvalidOperationException($"BrokerName not configured for environment '{environment}'");
        }

        var secretName = $"/{brokerName}/endpoints/openwire-ssl";
        var secretValue = new
        {
            active_url = outputs.OpenwireSslActiveUrl?.Value ?? "",
            standby_url = outputs.OpenwireSslStandbyUrl?.Value ?? "",
            all_urls = outputs.OpenwireSslUrls.Value
        };

        var secretJson = JsonSerializer.Serialize(secretValue);

        try
        {
            // Try to update existing secret
            var updateRequest = new PutSecretValueRequest
            {
                SecretId = secretName,
                SecretString = secretJson
            };

            await _secretsManager.PutSecretValueAsync(updateRequest);
            _logger.LogInformation($"Updated secret '{secretName}' with OpenWire SSL endpoints");
        }
        catch (ResourceNotFoundException)
        {
            // Secret doesn't exist, create it
            try
            {
                var createRequest = new CreateSecretRequest
                {
                    Name = secretName,
                    Description = "OpenWire SSL endpoints for ActiveMQ (active and standby)",
                    SecretString = secretJson
                };

                await _secretsManager.CreateSecretAsync(createRequest);
                _logger.LogInformation($"Created secret '{secretName}' with OpenWire SSL endpoints");
            }
            catch (Exception createEx)
            {
                _logger.LogError(createEx, $"Failed to create secret '{secretName}'");
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update secret '{secretName}'");
            throw;
        }
    }
}

