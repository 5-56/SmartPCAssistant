using System.IO;
using Serilog;
using SmartPCAssistant.Services;
using SmartPCAssistant.Models;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPCAssistant",
            "logs",
            "app-.log");

        var logDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("SmartPCAssistant starting...");

            await AiProviderService.Instance.InitializeAsync();

            if (args.Length > 0 && args[0] == "--cli")
            {
                await RunCliModeAsync(args);
            }
            else
            {
                await RunInteractiveModeAsync();
            }

            Log.Information("SmartPCAssistant shutting down...");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static async Task RunInteractiveModeAsync()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              智能电脑助手 - SmartPCAssistant                  ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  一个全智能控制电脑的AI助手                                   ║");
        Console.WriteLine("║  输入 'help' 查看帮助，输入 'quit' 退出                      ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                ShowHelp();
                continue;
            }

            if (input.Equals("history", StringComparison.OrdinalIgnoreCase))
            {
                await ShowHistoryAsync();
                continue;
            }

            if (input.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                await SessionService.Instance.StartNewSessionAsync();
                Console.WriteLine("已开启新对话");
                continue;
            }

            Console.WriteLine("正在思考...");
            var response = await SessionService.Instance.ProcessUserInputAsync(input);
            Console.WriteLine();
            Console.WriteLine($"助手: {response}");
            Console.WriteLine();
        }
    }

    static async Task RunCliModeAsync(string[] args)
    {
        var commandArgsList = args.Skip(1).ToList();
        if (commandArgsList.Count == 0)
        {
            Console.WriteLine("CLI模式用法: SmartPCAssistant --cli \"你的指令\"");
            return;
        }

        var command = string.Join(" ", commandArgsList);
        Console.WriteLine($"执行: {command}");
        var response = await SessionService.Instance.ProcessUserInputAsync(command);
        Console.WriteLine(response);
    }

    static void ShowHelp()
    {
        Console.WriteLine("可用命令:");
        Console.WriteLine("  help    - 显示此帮助信息");
        Console.WriteLine("  history  - 显示对话历史");
        Console.WriteLine("  new     - 开始新对话");
        Console.WriteLine("  quit    - 退出程序");
        Console.WriteLine();
        Console.WriteLine("也可以直接输入任何问题或指令，AI会帮你处理。");
    }

    static async Task ShowHistoryAsync()
    {
        var sessions = await SessionService.Instance.GetAllSessionsAsync();
        Console.WriteLine();
        Console.WriteLine("对话历史:");
        Console.WriteLine("─────────────────────────────────────────────────────────");

        if (sessions.Count == 0)
        {
            Console.WriteLine("暂无历史记录");
        }
        else
        {
            foreach (var session in sessions.Take(10))
            {
                var statusIcon = session.Status switch
                {
                    SessionStatus.Active => "🔄",
                    SessionStatus.Completed => "✅",
                    SessionStatus.Paused => "⏸️",
                    _ => "❓"
                };

                Console.WriteLine($"{statusIcon} [{session.StartedAt:yyyy-MM-dd HH:mm}] {session.Title}");
                Console.WriteLine($"   耗时: {session.Duration}秒 | 状态: {session.Status}");
            }
        }
        Console.WriteLine("─────────────────────────────────────────────────────────");
        Console.WriteLine();
    }
}
