using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddGoogleAIGeminiChatCompletion(
    modelId: "gemini-3-flash-preview",
    apiKey: "AIzaSyCUGzkEObs4mXozvcDTX0Yp6fKiKG2BFuY"
);
Kernel kernel = kernelBuilder.Build();

ChatCompletionAgent agent =
    new()
    {
        Name = "SK-Agent",
        Instructions = "You are a helpful assistant.",
        Kernel = kernel,
    };

await foreach (AgentResponseItem<ChatMessageContent> response
    in agent.InvokeAsync("what's your name?"))
{
    Console.WriteLine(response.Message);
}

Console.ReadLine();
