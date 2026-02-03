using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

#pragma warning disable SKEXP0070
#pragma warning disable SKEXP0110

// 加载配置文件（先加载 appsettings.json，再加载 appsettings.secret.json，secret 文件优先级更高）
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.secret.json", optional: true, reloadOnChange: true)
    .Build();

// 从配置中读取敏感信息
string modelId = configuration["GoogleAI:ModelId"] 
    ?? throw new InvalidOperationException("GoogleAI:ModelId 配置项未找到");
string apiKey = configuration["GoogleAI:ApiKey"] 
    ?? throw new InvalidOperationException("GoogleAI:ApiKey 配置项未找到");

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddGoogleAIGeminiChatCompletion(
    modelId: modelId,
    apiKey: apiKey
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
