using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CninfoCrawler;

/// <summary>
/// 巨潮资讯网年报/半年报下载器
/// </summary>
class Program
{
    // 巨潮资讯网API地址
    private const string QueryUrl = "https://www.cninfo.com.cn/new/hisAnnouncement/query";
    private const string DownloadBaseUrl = "https://static.cninfo.com.cn/finalpage";
    
    // 公告分类映射表
    private static readonly Dictionary<string, string> AllCategories = new()
    {
        ["年度报告"] = "category_ndbg_szsh",
        ["半年度报告"] = "category_bndbg_szsh",
        ["首发"] = "category_sf_szsh",
        ["一季度报告"] = "category_yjdbg_szsh",
        ["三季度报告"] = "category_sjdbg_szsh",
        ["首次公开发行及上市"] = "category_scgkfx_szsh",
        ["中介报告"] = "category_zj_szsh",
        ["股东大会"] = "category_gddh_szsh",
        ["增发"] = "category_zf_szsh"
    };

    private static readonly HttpClient _httpClient;

    static Program()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        
        // 设置请求头，模拟浏览器访问
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://www.cninfo.com.cn");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.cninfo.com.cn/");
    }

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        // 解析命令行参数
        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        string stockCode = args[0];
        
        // 根据股票代码判断市场类型（沪市/深市）
        string orgId = GetOrgId(stockCode);
        
        // 解析要下载的分类
        var categoriesToDownload = new Dictionary<string, string>();
        if (args.Length >= 2)
        {
            // 用户指定了分类，用逗号分隔
            var categoryNames = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var name in categoryNames)
            {
                string trimmedName = name.Trim();
                if (AllCategories.TryGetValue(trimmedName, out string? categoryCode))
                {
                    categoriesToDownload[trimmedName] = categoryCode;
                }
                else
                {
                    Console.WriteLine($"警告: 未知的分类名称 '{trimmedName}'，已跳过");
                }
            }
            
            if (categoriesToDownload.Count == 0)
            {
                Console.WriteLine("错误: 没有有效的分类");
                PrintUsage();
                return;
            }
        }
        else
        {
            // 默认下载年度报告和半年度报告
            categoriesToDownload["年度报告"] = AllCategories["年度报告"];
            categoriesToDownload["半年度报告"] = AllCategories["半年度报告"];
        }

        string saveDirectory = Path.Combine(Environment.CurrentDirectory, "Downloads", stockCode);
        
        Console.WriteLine($"=== 巨潮资讯网公告下载器 ===");
        Console.WriteLine($"股票代码: {stockCode}");
        Console.WriteLine($"组织ID: {orgId}");
        Console.WriteLine($"下载分类: {string.Join(", ", categoriesToDownload.Keys)}");
        Console.WriteLine($"保存目录: {saveDirectory}");
        Console.WriteLine();

        // 确保保存目录存在
        Directory.CreateDirectory(saveDirectory);

        foreach (var category in categoriesToDownload)
        {
            Console.WriteLine($">>> 正在获取【{category.Key}】列表...");
            
            try
            {
                var announcements = await GetAnnouncementsAsync(stockCode, orgId, category.Value);
                Console.WriteLine($"    找到 {announcements.Count} 个公告");

                // 为每个分类创建子目录
                string categoryDir = Path.Combine(saveDirectory, category.Key);
                Directory.CreateDirectory(categoryDir);

                foreach (var announcement in announcements)
                {
                    await DownloadAnnouncementAsync(announcement, categoryDir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    获取失败: {ex.Message}");
            }
            
            Console.WriteLine();
        }

        Console.WriteLine("=== 下载完成 ===");
    }

    /// <summary>
    /// 打印使用说明
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("用法: CninfoCrawler <股票代码> [分类1,分类2,...]");
        Console.WriteLine();
        Console.WriteLine("参数:");
        Console.WriteLine("  股票代码    必填，如 600887、000858、300750");
        Console.WriteLine("  分类        可选，多个分类用逗号分隔，默认为 年度报告,半年度报告");
        Console.WriteLine();
        Console.WriteLine("支持的分类:");
        foreach (var cat in AllCategories)
        {
            Console.WriteLine($"  - {cat.Key}");
        }
        Console.WriteLine();
        Console.WriteLine("示例:");
        Console.WriteLine("  CninfoCrawler 600887");
        Console.WriteLine("  CninfoCrawler 600887 年度报告");
        Console.WriteLine("  CninfoCrawler 600887 年度报告,半年度报告,首发");
        Console.WriteLine("  CninfoCrawler 000858 年度报告,一季度报告,三季度报告");
    }

    /// <summary>
    /// 根据股票代码获取组织ID
    /// </summary>
    private static string GetOrgId(string stockCode)
    {
        // 判断市场类型
        // 沪市主板: 600xxx, 601xxx, 603xxx, 605xxx
        // 科创板: 688xxx
        // 深市主板: 000xxx, 001xxx
        // 中小板: 002xxx
        // 创业板: 300xxx, 301xxx
        // 北交所: 8xxxxx, 4xxxxx
        
        if (stockCode.StartsWith("6"))
        {
            return $"gssh0{stockCode}";  // 沪市
        }
        else if (stockCode.StartsWith("0") || stockCode.StartsWith("3"))
        {
            return $"gssz0{stockCode}";  // 深市
        }
        else if (stockCode.StartsWith("8") || stockCode.StartsWith("4"))
        {
            return $"gsbj0{stockCode}";  // 北交所
        }
        else
        {
            return $"gssh0{stockCode}";  // 默认沪市
        }
    }

    /// <summary>
    /// 根据股票代码获取column参数
    /// </summary>
    private static string GetColumn(string stockCode)
    {
        if (stockCode.StartsWith("6"))
        {
            return "sse";  // 沪市
        }
        else if (stockCode.StartsWith("0") || stockCode.StartsWith("3"))
        {
            return "szse";  // 深市
        }
        else if (stockCode.StartsWith("8") || stockCode.StartsWith("4"))
        {
            return "bse";  // 北交所
        }
        else
        {
            return "sse";  // 默认沪市
        }
    }

    /// <summary>
    /// 根据股票代码获取plate参数
    /// </summary>
    private static string GetPlate(string stockCode)
    {
        if (stockCode.StartsWith("6"))
        {
            return "sh";  // 沪市
        }
        else if (stockCode.StartsWith("0") || stockCode.StartsWith("3"))
        {
            return "sz";  // 深市
        }
        else if (stockCode.StartsWith("8") || stockCode.StartsWith("4"))
        {
            return "bj";  // 北交所
        }
        else
        {
            return "sh";  // 默认沪市
        }
    }

    /// <summary>
    /// 获取公告列表
    /// </summary>
    private static async Task<List<AnnouncementInfo>> GetAnnouncementsAsync(string stockCode, string orgId, string category)
    {
        var allAnnouncements = new List<AnnouncementInfo>();
        int pageNum = 1;
        int pageSize = 30;
        int totalPages = 1;

        do
        {
            var formData = new Dictionary<string, string>
            {
                ["stock"] = $"{stockCode},{orgId}",
                ["tabName"] = "fulltext",
                ["pageSize"] = pageSize.ToString(),
                ["pageNum"] = pageNum.ToString(),
                ["column"] = GetColumn(stockCode),
                ["category"] = $"{category};",
                ["plate"] = GetPlate(stockCode),
                ["seDate"] = "",
                ["searchkey"] = "",
                ["secid"] = "",
                ["sortName"] = "",
                ["sortType"] = "",
                ["isHLtitle"] = "true"
            };

            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(QueryUrl, content);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(json);

            // 获取总页数
            if (pageNum == 1)
            {
                int totalCount = result["totalAnnouncement"]?.Value<int>() ?? 0;
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            }

            // 解析公告列表
            var announcements = result["announcements"];
            if (announcements != null)
            {
                foreach (var item in announcements)
                {
                    var info = new AnnouncementInfo
                    {
                        Title = item["announcementTitle"]?.Value<string>() ?? "",
                        AnnouncementId = item["announcementId"]?.Value<string>() ?? "",
                        AnnouncementTime = item["announcementTime"]?.Value<long>() ?? 0,
                        AdjunctUrl = item["adjunctUrl"]?.Value<string>() ?? ""
                    };
                    
                    // 过滤：只保留正式的年报/半年报，排除摘要、英文版等
                    if (IsValidReport(info.Title))
                    {
                        allAnnouncements.Add(info);
                    }
                }
            }

            pageNum++;
            
            // 避免请求过于频繁
            await Task.Delay(500);
            
        } while (pageNum <= totalPages);

        return allAnnouncements;
    }

    /// <summary>
    /// 判断是否为有效的报告（排除摘要、英文版等）
    /// </summary>
    private static bool IsValidReport(string title)
    {
        // 排除摘要
        if (title.Contains("摘要")) return false;
        // 排除英文版
        if (title.Contains("英文")) return false;
        // 排除修订稿、更正等
        if (title.Contains("修订") || title.Contains("更正") || title.Contains("补充")) return false;
        // 排除取消
        if (title.Contains("取消")) return false;
        
        return true;
    }

    /// <summary>
    /// 下载公告文件
    /// </summary>
    private static async Task DownloadAnnouncementAsync(AnnouncementInfo announcement, string saveDirectory)
    {
        try
        {
            // 从标题中提取年份信息
            string year = ExtractYear(announcement.Title);
            
            // 构建文件名：年份_标题.PDF
            string safeTitle = GetSafeFileName(announcement.Title);
            string fileName = $"{year}_{safeTitle}.PDF";
            string filePath = Path.Combine(saveDirectory, fileName);

            // 如果文件已存在，跳过下载
            if (File.Exists(filePath))
            {
                Console.WriteLine($"    [跳过] {fileName} (已存在)");
                return;
            }

            // 构建下载URL
            string downloadUrl = $"https://static.cninfo.com.cn/{announcement.AdjunctUrl}";
            
            Console.WriteLine($"    [下载] {fileName}");
            
            var response = await _httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(filePath, bytes);
            
            Console.WriteLine($"    [完成] {fileName} ({bytes.Length / 1024.0 / 1024.0:F2} MB)");
            
            // 避免请求过于频繁
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [失败] {announcement.Title}: {ex.Message}");
        }
    }

    /// <summary>
    /// 从标题中提取年份
    /// </summary>
    private static string ExtractYear(string title)
    {
        // 尝试匹配年份，如 "2024年年度报告"、"2024年半年度报告"
        var match = System.Text.RegularExpressions.Regex.Match(title, @"(\d{4})年");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return "未知年份";
    }

    /// <summary>
    /// 获取安全的文件名（移除非法字符）
    /// </summary>
    private static string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new StringBuilder(fileName);
        
        foreach (var c in invalidChars)
        {
            safeName.Replace(c, '_');
        }
        
        // 限制长度
        string result = safeName.ToString();
        if (result.Length > 100)
        {
            result = result.Substring(0, 100);
        }
        
        return result;
    }
}

/// <summary>
/// 公告信息
/// </summary>
class AnnouncementInfo
{
    public string Title { get; set; } = "";
    public string AnnouncementId { get; set; } = "";
    public long AnnouncementTime { get; set; }
    public string AdjunctUrl { get; set; } = "";
}
