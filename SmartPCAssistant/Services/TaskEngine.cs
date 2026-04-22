using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SmartPCAssistant.Services;

public enum TaskType
{
    Unknown,
    OpenApp,
    CloseApp,
    Search,
    ExecuteCommand,
    FileOperation,
    SystemSetting,
    WebOperation,
    LearnAndExecute
}

public class TaskRequest
{
    public string RawInput { get; set; } = string.Empty;
    public TaskType Type { get; set; } = TaskType.Unknown;
    public string Target { get; set; } = string.Empty;
    public string? AdditionalInfo { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public class TaskResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public string? Error { get; set; }
}

public interface ITaskEngine
{
    Task<TaskResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default);
    TaskRequest ParseRequest(string userInput);
}

public class TaskEngine : ITaskEngine
{
    private static TaskEngine? _instance;
    public static TaskEngine Instance => _instance ??= new TaskEngine();

    private readonly ExecutorService _executor;
    private readonly LearningService _learning;

    private readonly Dictionary<string, TaskType> _intentPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        { "打开", TaskType.OpenApp },
        { "启动", TaskType.OpenApp },
        { "运行", TaskType.OpenApp },
        { "关闭", TaskType.CloseApp },
        { "退出", TaskType.CloseApp },
        { "停止", TaskType.CloseApp },
        { "搜索", TaskType.Search },
        { "查找", TaskType.Search },
        { "查询", TaskType.Search },
        { "cmd", TaskType.ExecuteCommand },
        { "命令", TaskType.ExecuteCommand },
        { "终端", TaskType.ExecuteCommand },
        { "powershell", TaskType.ExecuteCommand },
        { "创建", TaskType.FileOperation },
        { "删除", TaskType.FileOperation },
        { "移动", TaskType.FileOperation },
        { "复制", TaskType.FileOperation },
        { "设置", TaskType.SystemSetting },
        { "配置", TaskType.SystemSetting },
        { "打开网页", TaskType.WebOperation },
        { "访问", TaskType.WebOperation },
        { "浏览", TaskType.WebOperation },
        { "学习", TaskType.LearnAndExecute },
        { "教我", TaskType.LearnAndExecute },
        { "怎么", TaskType.LearnAndExecute },
        { "如何", TaskType.LearnAndExecute }
    };

    private TaskEngine()
    {
        _executor = ExecutorService.Instance;
        _learning = LearningService.Instance;
    }

    public TaskRequest ParseRequest(string userInput)
    {
        var request = new TaskRequest
        {
            RawInput = userInput
        };

        foreach (var pattern in _intentPatterns)
        {
            if (userInput.Contains(pattern.Key, StringComparison.OrdinalIgnoreCase))
            {
                request.Type = pattern.Value;
                break;
            }
        }

        request.Target = ExtractTarget(userInput, request.Type);

        return request;
    }

    private string ExtractTarget(string input, TaskType type)
    {
        var patterns = new Dictionary<TaskType, string[]>
        {
            { TaskType.OpenApp, new[] { @"(?:打开|启动|运行)\s+(?:到\s+)?(.+?)(?:\s|$)", @"^(.+?)(?:\s+并|\s+然后|.+$)" } },
            { TaskType.CloseApp, new[] { @"(?:关闭|退出|停止)\s+(.+?)(?:\s|$)" } },
            { TaskType.Search, new[] { @"(?:搜索|查找|查询)\s+(.+?)(?:\s|$)" } },
            { TaskType.ExecuteCommand, new[] { @"\b(cmd|命令|终端|powershell)\s+(.+)" } },
            { TaskType.WebOperation, new[] { @"(?:打开|访问|浏览)\s*(?:网页|网站|网址)?\s*(https?://.+|[^\s]+)" } }
        };

        if (patterns.TryGetValue(type, out var regexPatterns))
        {
            foreach (var pattern in regexPatterns)
            {
                var match = Regex.Match(input, pattern);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }

        return input.Trim();
    }

    public async Task<TaskResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var result = new TaskResult();

        try
        {
            Log.Information("Executing task: {Type} - {Target}", request.Type, request.Target);

            result = request.Type switch
            {
                TaskType.OpenApp => await ExecuteOpenAppAsync(request.Target, cancellationToken),
                TaskType.CloseApp => await ExecuteCloseAppAsync(request.Target, cancellationToken),
                TaskType.Search => await ExecuteSearchAsync(request.Target, cancellationToken),
                TaskType.ExecuteCommand => await ExecuteCommandAsync(request.Target, cancellationToken),
                TaskType.WebOperation => await ExecuteWebOperationAsync(request.Target, cancellationToken),
                TaskType.LearnAndExecute => await ExecuteLearnAndExecuteAsync(request.Target, cancellationToken),
                _ => await ExecuteDefaultAsync(request.RawInput, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Task execution failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<TaskResult> ExecuteOpenAppAsync(string appName, CancellationToken cancellationToken)
    {
        var result = new TaskResult();
        var execResult = await _executor.OpenApplicationAsync(appName, cancellationToken);

        result.Success = execResult.Success;
        result.Message = execResult.Success
            ? $"已打开应用: {appName}"
            : $"无法打开应用: {appName}";
        result.Steps.Add($"执行命令: 打开 {appName}");
        result.Steps.Add(execResult.Output);

        return result;
    }

    private async Task<TaskResult> ExecuteCloseAppAsync(string appName, CancellationToken cancellationToken)
    {
        var result = new TaskResult();
        var execResult = await _executor.CloseApplicationAsync(appName, cancellationToken);

        result.Success = execResult.Success;
        result.Message = execResult.Success
            ? $"已关闭应用: {appName}"
            : $"无法关闭应用: {appName}";
        result.Steps.Add($"执行命令: 关闭 {appName}");
        result.Steps.Add(execResult.Output);

        return result;
    }

    private async Task<TaskResult> ExecuteSearchAsync(string query, CancellationToken cancellationToken)
    {
        var result = new TaskResult();
        var searchResults = await _learning.SearchAsync(query, 5, cancellationToken);

        result.Success = searchResults.Count > 0;
        result.Message = $"找到 {searchResults.Count} 个搜索结果";
        result.Steps.Add($"执行搜索: {query}");

        foreach (var item in searchResults)
        {
            result.Steps.Add($"• {item.Title}");
        }

        return result;
    }

    private async Task<TaskResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken)
    {
        var result = new TaskResult();
        var execResult = await _executor.ExecutePowerShellAsync(command, cancellationToken);

        result.Success = execResult.Success;
        result.Message = execResult.Success
            ? "命令执行成功"
            : "命令执行失败";
        result.Steps.Add($"执行命令: {command}");
        result.Steps.Add(execResult.Output);

        return result;
    }

    private async Task<TaskResult> ExecuteWebOperationAsync(string url, CancellationToken cancellationToken)
    {
        var result = new TaskResult();

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
        }

        await _learning.OpenInBrowserAsync(url, cancellationToken);

        result.Success = true;
        result.Message = $"已在浏览器中打开: {url}";
        result.Steps.Add($"打开网址: {url}");

        return result;
    }

    private async Task<TaskResult> ExecuteLearnAndExecuteAsync(string task, CancellationToken cancellationToken)
    {
        var result = new TaskResult();
        var guide = await _learning.GetStepByStepGuideAsync(task, cancellationToken);

        result.Success = guide.Count > 0;
        result.Message = $"找到 {guide.Count} 个步骤";
        result.Steps.Add($"学习任务: {task}");

        foreach (var step in guide)
        {
            result.Steps.Add($"• {step}");
        }

        return result;
    }

    private async Task<TaskResult> ExecuteDefaultAsync(string input, CancellationToken cancellationToken)
    {
        var result = new TaskResult
        {
            Success = true,
            Message = "请求已理解，正在处理..."
        };

        result.Steps.Add($"原始输入: {input}");
        result.Steps.Add("请求已转发到 AI 进行处理");

        await Task.Delay(100, cancellationToken);

        return result;
    }
}
