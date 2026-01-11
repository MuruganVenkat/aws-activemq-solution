using System.Text;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace ActiveMQProducer;

class Program
{
    private static readonly HttpClient HttpClient = new();
    
    // Configuration (Ideally from Environment Variables or args)
    private const string BrokerName = "private-activemq-cluster";
    private const string User = "app_user";     // Should be retrieved from Secrets Manager in Prod
    private const string Password = "Password123!"; // Should be retrieved from Secrets Manager in Prod
    private const string TopicName = "events.status";

    static async Task Main(string[] args)
    {
        Console.WriteLine("--- ActiveMQ REST Producer ---");

        // 1. Get Endpoints from SSM
        var endpoints = await GetEndpointsFromSSM();
        if (endpoints.Count == 0)
        {
            Console.WriteLine("No endpoints found for broker.");
            return;
        }

        while (true)
        {
            Console.WriteLine("Enter message to publish (or 'exit'):");
            var input = Console.ReadLine();
            if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase)) break;

            if (string.IsNullOrWhiteSpace(input)) continue;

            // 2. Publish with Failover
            bool success = false;
            foreach (var endpoint in endpoints)
            {
                // Format: https://<endpoint>:8161/api/message/<dest>?type=topic
                // Note: 'console_url' from AWS includes the https prefix mostly, but we need API port.
                // Output from Terraform was console_url (port 8161).
                // Example: https://b-123...mq.us-east-1.amazonaws.com:8161/web-console
                // We need to strip /web-console and append /api/message/events.status?type=topic
                
                var baseUrl = endpoint.Replace("/web-console", ""); 
                var url = $"{baseUrl}/api/message/{TopicName}?type=topic";

                Console.WriteLine($"Trying endpoint: {url}");

                if (await SendMessage(url, input))
                {
                    success = true;
                    break; // Exit loop on success
                }
                
                Console.WriteLine($"Failed to send to {url}. Trying next...");
            }

            if (!success)
            {
                Console.WriteLine("CRITICAL: Failed to publish message to ANY endpoint.");
            }
        }
    }

    private static async Task<List<string>> GetEndpointsFromSSM()
    {
        try
        {
            var ssmClient = new AmazonSimpleSystemsManagementClient();
            var response = await ssmClient.GetParameterAsync(new GetParameterRequest
            {
                Name = $"/{BrokerName}/endpoints/https",
                WithDecryption = false 
            });

            var value = response.Parameter.Value;
            return value.Split(',').ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving endpoints from SSM: {ex.Message}");
            // Return dummy list for local testing if AWS creds fail
            return new List<string>(); 
        }
    }

    private static async Task<bool> SendMessage(string url, string body)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            // Basic Auth
            var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{User}:{Password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);
            
            request.Content = new StringContent(body, Encoding.UTF8, "text/plain");

            var response = await HttpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine(" [✔] Message Sent Successfully!");
                return true;
            }
            else
            {
                Console.WriteLine($" [X] Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" [X] Exception: {ex.Message}");
            return false;
        }
    }
}
