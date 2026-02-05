using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System.Text.Json;
using System.Text.Unicode;

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

// 获取用户输入的企业名称
Console.Write("企业名称: 伊利股份 ");
string? companyName = "伊利股份";

if (string.IsNullOrWhiteSpace(companyName))
{
    Console.WriteLine("企业名称不能为空！");
    return;
}

// 初始化Kernel和ChatCompletion服务
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddGoogleAIGeminiChatCompletion(
    modelId: modelId,
    apiKey: apiKey
);
Kernel kernel = kernelBuilder.Build();

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// 清理企业名称中的非法字符
string safeCompanyName = string.Join("_", companyName.Split(Path.GetInvalidFileNameChars()));

// 创建输出目录
string outputBaseDir = Path.Combine(Directory.GetCurrentDirectory(), "output", "企业分析", "企业基本");
Directory.CreateDirectory(outputBaseDir);

// ========== 1. 生成企业和行业介绍 ==========
string basicPromptTemplatePath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "stock_prompt",
    "企业分析",
    "企业基本",
    "basic.md"
);

if (!File.Exists(basicPromptTemplatePath))
{
    Console.WriteLine($"提示词模板文件不存在: {basicPromptTemplatePath}");
    return;
}

string basicPromptTemplate = await File.ReadAllTextAsync(basicPromptTemplatePath);
string basicPrompt = basicPromptTemplate.Replace("{company}", companyName);

Console.WriteLine($"\n正在生成 {companyName} 的企业和行业介绍...\n");

ChatHistory basicChatHistory = new();
basicChatHistory.AddUserMessage(basicPrompt);

var basicResponse = await chatCompletionService.GetChatMessageContentAsync(
    basicChatHistory
);

string basicResponseText = basicResponse.Content ?? string.Empty;

// 尝试提取JSON内容（可能包含markdown代码块）
string jsonContent = basicResponseText.Trim();
if (jsonContent.StartsWith("```json"))
{
    int startIndex = jsonContent.IndexOf('{');
    int endIndex = jsonContent.LastIndexOf('}');
    if (startIndex >= 0 && endIndex > startIndex)
    {
        jsonContent = jsonContent.Substring(startIndex, endIndex - startIndex + 1);
    }
}
else if (jsonContent.StartsWith("```"))
{
    int startIndex = jsonContent.IndexOf('{');
    int endIndex = jsonContent.LastIndexOf('}');
    if (startIndex >= 0 && endIndex > startIndex)
    {
        jsonContent = jsonContent.Substring(startIndex, endIndex - startIndex + 1);
    }
}

// 解析并保存JSON
try
{
    var jsonDoc = JsonDocument.Parse(jsonContent);
    var formattedJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(UnicodeRanges.All)
    });

    Console.WriteLine("企业和行业介绍生成结果：");
    Console.WriteLine(formattedJson);
    Console.WriteLine();

    string basicOutputFilePath = Path.Combine(outputBaseDir, $"{safeCompanyName}_basic.json");
    await File.WriteAllTextAsync(basicOutputFilePath, formattedJson, System.Text.Encoding.UTF8);
    Console.WriteLine($"结果已保存到: {basicOutputFilePath}\n");
}
catch (JsonException ex)
{
    Console.WriteLine($"JSON解析失败: {ex.Message}");
    Console.WriteLine($"原始响应内容:\n{basicResponseText}\n");
}

// ========== 2. 生成企业历史报告 ==========
string historyPromptTemplatePath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "stock_prompt",
    "企业分析",
    "企业基本",
    "history.md"
);

if (!File.Exists(historyPromptTemplatePath))
{
    Console.WriteLine($"历史报告提示词模板文件不存在: {historyPromptTemplatePath}");
}
else
{
    string historyPromptTemplate = await File.ReadAllTextAsync(historyPromptTemplatePath);
    string historyPrompt = historyPromptTemplate.Replace("{{company}}", companyName);

    Console.WriteLine($"正在生成 {companyName} 的企业历史报告...\n");
    Console.WriteLine("（这可能需要较长时间，请耐心等待...）\n");

    ChatHistory historyChatHistory = new();
    historyChatHistory.AddUserMessage(historyPrompt);

    var historyResponse = await chatCompletionService.GetChatMessageContentAsync(
        historyChatHistory
    );

    string historyResponseText = historyResponse.Content ?? string.Empty;

    // 保存历史报告为Markdown文件
    try
    {
        string historyOutputFilePath = Path.Combine(outputBaseDir, $"{safeCompanyName}_history.md");
        await File.WriteAllTextAsync(historyOutputFilePath, historyResponseText, System.Text.Encoding.UTF8);

        Console.WriteLine("企业历史报告生成完成！");
        Console.WriteLine($"结果已保存到: {historyOutputFilePath}\n");
        
        // 显示报告的前几行预览
        var previewLines = historyResponseText.Split('\n').Take(10);
        Console.WriteLine("报告预览（前10行）：");
        Console.WriteLine("---");
        foreach (var line in previewLines)
        {
            Console.WriteLine(line);
        }
        Console.WriteLine("---\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"保存历史报告时出错: {ex.Message}");
    }
}

Console.WriteLine("\n按回车键退出...");
Console.ReadLine();
