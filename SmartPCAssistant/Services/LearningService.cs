using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SmartPCAssistant.Services;

public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

public interface ILearningService
{
    Task<List<SearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default);
    Task<string> GetPageContentAsync(string url, CancellationToken cancellationToken = default);
    Task<string> LearnAndExecuteAsync(string task, CancellationToken cancellationToken = default);
    void SetBrowser(string browser);
    string GetDefaultBrowser();
}

public class LearningService : ILearningService
{
    private static LearningService? _instance;
    public static LearningService Instance => _instance ??= new LearningService();

    private readonly HttpClient _httpClient;
    private string _defaultBrowser = "chrome";
    private readonly List<string> _searchEngines = new()
    {
        "https://www.bing.com/search?q=",
        "https://www.google.com/search?q=",
        "https://search.yahoo.com/search?p="
    };

    private LearningService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public string GetDefaultBrowser() => _defaultBrowser;

    public void SetBrowser(string browser)
    {
        _defaultBrowser = browser.ToLower();
        Log.Information("Learning service browser set to: {Browser}", _defaultBrowser);
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResult>();

        Log.Information("Searching for: {Query}", query);

        var results = new List<SearchResult>();

        try
        {
            var searchUrl = $"{_searchEngines[0]}{Uri.EscapeDataString(query)}";
            var html = await _httpClient.GetStringAsync(searchUrl, cancellationToken);

            results = ParseSearchResults(html, maxResults);

            Log.Information("Found {Count} search results", results.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Search failed for query: {Query}", query);
        }

        return results;
    }

    private List<SearchResult> ParseSearchResults(string html, int maxResults)
    {
        var results = new List<SearchResult>();

        try
        {
            var titleRegex = new Regex(@"<h2[^>]*>\s*<a[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase);
            var urlRegex = new Regex(@"href=""(https?://[^""]+)""", RegexOptions.IgnoreCase);
            var snippetRegex = new Regex(@"<p[^>]*>([^<]+)</p>", RegexOptions.IgnoreCase);

            var titles = titleRegex.Matches(html);
            var urls = urlRegex.Matches(html);
            var snippets = snippetRegex.Matches(html);

            var count = Math.Min(Math.Min(titles.Count, urls.Count), snippets.Count);

            for (int i = 0; i < Math.Min(count, maxResults); i++)
            {
                var title = Regex.Replace(titles[i].Groups[1].Value, @"<[^>]+>", "").Trim();
                var url = urls[i].Groups[1].Value;

                if (url.StartsWith("http") && !title.Contains("google") && !title.Contains("bing"))
                {
                    results.Add(new SearchResult
                    {
                        Title = title,
                        Url = url,
                        Snippet = Regex.Replace(snippets[i].Groups[1].Value, @"<[^>]+>", "").Trim()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse search results");
        }

        return results;
    }

    public async Task<string> GetPageContentAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        Log.Information("Fetching page content: {Url}", url);

        try
        {
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            var text = ExtractTextFromHtml(html);

            if (text.Length > 5000)
            {
                text = text[..5000] + "...\n\n[内容已截断]";
            }

            return text;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch page: {Url}", url);
            return $"获取页面失败: {ex.Message}";
        }
    }

    private string ExtractTextFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        try
        {
            html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<[^>]+>", " ");
            html = Regex.Replace(html, @"\s+", " ");
            html = Regex.Replace(html, @"&nbsp;", " ");
            html = Regex.Replace(html, @"&amp;", "&");
            html = Regex.Replace(html, @"&lt;", "<");
            html = Regex.Replace(html, @"&gt;", ">");
            html = Regex.Replace(html, @"&quot;", "\"");

            return html.Trim();
        }
        catch
        {
            return html;
        }
    }

    public async Task<string> LearnAndExecuteAsync(string task, CancellationToken cancellationToken = default)
    {
        Log.Information("Learning to execute task: {Task}", task);

        try
        {
            var searchResults = await SearchAsync(task + " 教程 步骤", 3, cancellationToken);

            if (searchResults.Count == 0)
            {
                return "抱歉，无法找到相关教程。请尝试更具体的描述。";
            }

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"找到 {searchResults.Count} 个相关教程：\n");

            foreach (var result in searchResults)
            {
                summary.AppendLine($"📄 {result.Title}");
                summary.AppendLine($"   {result.Snippet}");
                summary.AppendLine($"   链接: {result.Url}\n");
            }

            summary.AppendLine("\n正在获取详细内容...");

            if (searchResults.Count > 0)
            {
                var content = await GetPageContentAsync(searchResults[0].Url, cancellationToken);
                summary.AppendLine($"\n📝 详细内容：\n{content}");
            }

            return summary.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Learning failed for task: {Task}", task);
            return $"学习失败: {ex.Message}";
        }
    }

    public async Task<List<string>> GetStepByStepGuideAsync(string task, CancellationToken cancellationToken = default)
    {
        var steps = new List<string>();
        var searchQuery = $"{task} 步骤 教程";

        try
        {
            var searchResults = await SearchAsync(searchQuery, 3, cancellationToken);

            foreach (var result in searchResults)
            {
                var content = await GetPageContentAsync(result.Url, cancellationToken);
                var extractedSteps = ExtractSteps(content, task);
                steps.AddRange(extractedSteps);
            }

            if (steps.Count == 0)
            {
                steps.Add("无法找到具体步骤");
                steps.Add("请尝试更具体的任务描述");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get step-by-step guide");
            steps.Add($"获取步骤失败: {ex.Message}");
        }

        return steps;
    }

    private List<string> ExtractSteps(string content, string task)
    {
        var steps = new List<string>();

        try
        {
            var stepPatterns = new[]
            {
                @"(?:第[一二三四五六七八九十\d]+[步部个点]|step\s*\d+[:\.])?\s*([^。\n]+)",
                @"(?:首先|然后|接着|最后|下一步)[：:]?\s*([^。\n]+)",
                @"\d+[．.、]\s*([^。\n]+)"
            };

            foreach (var pattern in stepPatterns)
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var matches = regex.Matches(content);

                foreach (Match match in matches)
                {
                    var step = match.Groups[1].Value.Trim();
                    if (step.Length > 10 && step.Length < 200)
                    {
                        steps.Add(step);
                    }
                }

                if (steps.Count > 0)
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to extract steps");
        }

        return steps;
    }

    public async Task OpenInBrowserAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        Log.Information("Opening URL in browser: {Url}", url);

        var browserCommand = _defaultBrowser.ToLower() switch
        {
            "chrome" => $"start chrome \"{url}\"",
            "edge" => $"start msedge \"{url}\"",
            "firefox" => $"start firefox \"{url}\"",
            "brave" => $"start brave \"{url}\"",
            _ => $"start \"{url}\""
        };

        await ExecutorService.Instance.ExecutePowerShellAsync(browserCommand, cancellationToken);
    }
}
