using Apache.NMS;
using Apache.NMS.ActiveMQ;

namespace ActiveMQConsumer;

class Program
{
    // Configuration
    private const string BrokerUri = "activemq:tcp://localhost:61616"; // Update with AWS Endpoint (ssl://...)
    // Note: In AWS, use: "activemq:ssl://b-123...mq.us-east-1.amazonaws.com:61617"
    
    private const string User = "app_user";
    private const string Password = "Password123!";
    private const string TopicName = "events.status";

    static void Main(string[] args)
    {
        Console.WriteLine("--- ActiveMQ OpenWire Consumer ---");
        Console.WriteLine($"Connecting to: {BrokerUri}");

        try
        {
            // 1. Create Connection Factory
            IConnectionFactory factory = new ConnectionFactory(BrokerUri);

            // 2. Create Connection
            using IConnection connection = factory.CreateConnection(User, Password);
            connection.Start();
            
            // 3. Create Session (Client Acknowledge for manual control)
            using ISession session = connection.CreateSession(AcknowledgementMode.ClientAcknowledge);

            // 4. Create Destination (Topic)
            IDestination destination = session.GetTopic(TopicName);

            // 5. Create Consumer
            using IMessageConsumer consumer = session.CreateConsumer(destination);

            Console.WriteLine($"Listening on Topic: {TopicName}");
            
            // 6. Attach Listener
            consumer.Listener += new MessageListener(OnMessage);

            // Keep alive
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void OnMessage(IMessage message)
    {
        try
        {
            if (message is ITextMessage textMessage)
            {
                Console.WriteLine($"Received: '{textMessage.Text}'");
                
                // Simulate processing
                // Thread.Sleep(100);

                // 7. ACK (Success)
                message.Acknowledge();
                Console.WriteLine(" [ACK] Message processed.");
            }
            else
            {
                Console.WriteLine("Received non-text message.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" [NACK] Failed to process: {ex.Message}");
            // In ClientAcknowledge, failing to ACK effectively leaves it ... 
            // To be explicit about recovery/redelivery we might use a Transacted session and session.Rollback()
            // Or just not acknowledge and let it timeout/redeliver based on policy.
        }
    }
}
