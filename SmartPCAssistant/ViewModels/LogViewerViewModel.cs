using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace SmartPCAssistant.ViewModels;

public partial class LogViewerViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<LogEntry> _logEntries = new();

    [ObservableProperty]
    private string _selectedLogLevel = "全部";

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _currentLogFile = string.Empty;

    public ObservableCollection<string> LogLevels { get; } = new()
    {
        "全部",
        "信息",
        "警告",
        "错误"
    };

    public LogViewerViewModel()
    {
        LoadLogsAsync();
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartPCAssistant",
                "logs");

            if (!Directory.Exists(logPath))
            {
                return;
            }

            var latestLog = Directory.GetFiles(logPath, "app-*.log")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .FirstOrDefault();

            if (latestLog != null)
            {
                CurrentLogFile = latestLog;
                var lines = await File.ReadAllLinesAsync(latestLog);

                LogEntries.Clear();
                foreach (var line in lines.TakeLast(500))
                {
                    var entry = ParseLogLine(line);
                    if (entry != null)
                    {
                        LogEntries.Add(entry);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load logs");
        }
    }

    private LogEntry? ParseLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var entry = new LogEntry { RawLine = line };

        if (line.Contains("Information"))
            entry.Level = "信息";
        else if (line.Contains("Warning") || line.Contains("Warn"))
            entry.Level = "警告";
        else if (line.Contains("Error"))
            entry.Level = "错误";
        else if (line.Contains("Fatal"))
            entry.Level = "严重";
        else
            entry.Level = "调试";

        var timestampMatch = System.Text.RegularExpressions.Regex.Match(line, @"\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}");
        if (timestampMatch.Success)
        {
            entry.Timestamp = timestampMatch.Value;
        }

        var messageMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[\w+\]\s+(.+)$");
        if (messageMatch.Success)
        {
            entry.Message = messageMatch.Groups[1].Value;
        }
        else
        {
            entry.Message = line;
        }

        return entry;
    }

    [RelayCommand]
    private async Task RefreshLogs()
    {
        await LoadLogsAsync();
    }

    [RelayCommand]
    private async Task ClearLogs()
    {
        try
        {
            if (!string.IsNullOrEmpty(CurrentLogFile) && File.Exists(CurrentLogFile))
            {
                await File.WriteAllTextAsync(CurrentLogFile, string.Empty);
                LogEntries.Clear();
                Log.Information("Logs cleared");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to clear logs");
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartPCAssistant",
                "logs");

            if (Directory.Exists(logPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", logPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open log folder");
        }
    }
}

public class LogEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RawLine { get; set; } = string.Empty;
}
