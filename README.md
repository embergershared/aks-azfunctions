# Azure Functions in AKS

## Overview

Weeks ago, I saw it is possible to create, deploy and run [`Azure Functions`](https://learn.microsoft.com/en-us/azure/azure-functions/) in AKS, instead of an App Service Plan or ACA as described here: [Azure Functions hosting options](https://learn.microsoft.com/en-us/azure/azure-functions/functions-scale#overview-of-plans).

This sample is giving it a shot with 2 different triggers (in `C#` with `In-process model`):

- The classic example of an [`HTTP trigger`](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger?tabs=python-v2%2Cin-process%2Cnodejs-v4%2Cfunctionsv2&pivots=programming-language-csharp),
- A more complex [`Service Bus Queue trigger`](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-service-bus-trigger?tabs=python-v2%2Cin-process%2Cnodejs-v4%2Cextensionv5&pivots=programming-language-csharp),

It also look at the resulting Kubernetes objects created by the `func kubernetes deploy` command.

## Samples

### Create a function

```powershell
cd src/
func init AksFunctionApp --worker-runtime dotnet --docker
cd AksFunctionApp/
```

### "Http trigger" example

```powershell
func new --name HttpExample --template "HTTP trigger"
dotnet build
# Set the kubectl context to the target AKS cluster + namespace
$ACR = "<acr name>"
az acr login -n $ACR
func kubernetes deploy --name aks-func-app --registry "$ACR.azurecr.io"
```

> Important: The function is deployed in the current AKS + namespace context. It is set by the `current-context:` node of the `kubeconfig` file (In windows, located here: `~/.kube/config`).

Result:

![HTTP trigger deployment output](./img/deploy-http-output.jpg)

The resulting Kubernetes objects created for this trigger are:

- 2 `secret`s
- 1 `serviceaccount`
- 1 `role`
- 1 `rolebinding`
- 1 `service`
- 1 `deployment`

Basic test:

[http://172.111.222.142/api/httpexample?name=test&code="ApiKey"](http://172.111.222.142/api/httpexample?name=test&code="ApiKey")



### "Azure Service Bus Queue trigger" example

#### Create and Test locally

To see if we can run an Azure Function triggered by an Azure Service Bus Queue message, we:

1. Comment the entire content of the Http trigger function file

It ensures we are only focusing on the new function & trigger.

2. Create a `ServiceBusQueueTrigger` Function

    ```powershell
    func new --name AsbqExample --template "ServiceBusQueueTrigger"
    ```

3. Decide and Set a Connection String **Key** in the Function code + adapt the code

    ```csharp
    public void Run(
      [ServiceBusTrigger(QueueName, Connection = "SbNamespaceCS")]
      ServiceBusReceivedMessage message,
      ILogger log
    )
    {
      log.LogInformation($"C# ServiceBus queue trigger invoked on queue: {QueueName}");
      log.LogInformation($"   MessageId:    {message.MessageId}");
      log.LogInformation($"   Body/Content: {message.Body}");
    }
    ```

4. Create the chosen Connection String **Key** in the file `local.settings.json`, creating a block `"ConnectionStrings:"`

    ```json
    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet"
      },
      "ConnectionStrings": {
        "SbNamespaceCS": "Endpoint=sb://****************"
      }
    }
    ```

   > Important: The Connection String value must give access to the Service Bus Namespace, not the queue!

5. Set the Queue Name in the Function code

    I used this approach for the sample, but there many (better/other) ways:

    ```csharp
    private const string QueueName = "<queue name>";
    ```

6. Run/Debug locally

#### Adapt and deploy to AKS

1. Create an entry for the Connection String **Key** in **the `"Values"` section** of `local.settings.json`

    ```json
    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet",
        "SbNamespaceCS": "Endpoint=sb://****************"
      },
      "ConnectionStrings": {
        "SbNamespaceCS": "Endpoint=sb://****************"
      }
    }
    ```

   The `func kubernetes deploy` uses the `Values` node to create a Kubernetes Secret with the Key / Value pairs in the node, exposed in the pod.

2. Create a new `namespace` (for clean separation) and make it default

3. Deploy the function

    ```powershell
    func kubernetes deploy --name aks-func-app-asbq --registry "$ACR.azurecr.io"
    ```

    ![Service Bus Queue trigger deployment output](./img/deploy-asbq-output.jpg)

    > Note: The `func kubernetes deploy` uses the current KubeConfig context in the session to deploy to the AKS cluster + namespace.
    > If you need to change the context, use `kubectl config use-context <context name>`.
    > For more information, see [func kubernetes deploy](https://learn.microsoft.com/en-us/azure/azure-functions/functions-core-tools-reference?tabs=v2#func-kubernetes-deploy).

4. Check the Deployment runs

    ![Service Bus Queue trigger Kubernetes deployment](./img/asbq-deployment-result.jpg)

    > Note: The deployment has no pods running because there are no messages in the queue.

5. Test it works

    1. Create messages in the queue
    ![Created Messages](./img/created-messages.jpg)
    2. See the `KEDA` scaling the deployment creating replica(s)
    ![Scaled Deployment](./img/scaled-deployment.jpg)
    3. See the pod(s) logs to check they got the message(s)
    ![Pod Logs](./img/pods-logs.jpg)
    4. After the messages are processed, the pod(s) are scaled down (back to 0 in this sample)

### What's next

If there's a direct link between a Message in the Queue and a Function, using `KEDA` to scale the Messages processing compute power on an AKS cluster is very efficient.

It allows more complex deployment and Compute resources allocations, as all Kubernetes features become available to the Azure Function Pod(s), like `NodeSelector` to tune compute resources.

## References

[Azure Functions documentation](https://learn.microsoft.com/en-us/azure/azure-functions/)

[Azure Functions on Kubernetes with KEDA](https://learn.microsoft.com/en-us/azure/azure-functions/functions-kubernetes-keda)

[func kubernetes deploy](https://learn.microsoft.com/en-us/azure/azure-functions/functions-core-tools-reference?tabs=v2#func-kubernetes-deploy)

[Azure.Functions.Cli/Kubernetes source code](https://github.com/Azure/azure-functions-core-tools/tree/v4.x/src/Azure.Functions.Cli/Kubernetes)
