using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AksFunctionApp
{
    public class SbqTriggerExample
    {
        private const string QueueName = "aks-azfunc";

        [FunctionName("Aks-AzFunc_Queue_Triggered_Function")]
        public static void Run(
          [ServiceBusTrigger(QueueName, Connection = "SbNamespaceCS")]
          ServiceBusReceivedMessage message,
          ILogger log
        )
        {
            log.LogInformation($"C# ServiceBus queue trigger invoked on queue: {QueueName}");
            log.LogInformation($"   MessageId:    {message.MessageId}");
            log.LogInformation($"   Body/Content: {message.Body}");
        }
    }
}
