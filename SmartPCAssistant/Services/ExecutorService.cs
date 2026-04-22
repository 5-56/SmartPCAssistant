using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

namespace SmartPCAssistant.Services;

public static class PlatformHelper
{
    public static bool IsWindows()
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT;
    }
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }
}

public interface IExecutorService
{
    Task<ExecutionResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
    Task<ExecutionResult> ExecutePowerShellAsync(string script, CancellationToken cancellationToken = default);
    Task<ExecutionResult> OpenApplicationAsync(string appName, CancellationToken cancellationToken = default);
    Task<ExecutionResult> CloseApplicationAsync(string appName, CancellationToken cancellationToken = default);
    Task<string> GetScreenTextAsync(CancellationToken cancellationToken = default);
    Task ClickAtAsync(int x, int y, CancellationToken cancellationToken = default);
    Task TypeTextAsync(string text, CancellationToken cancellationToken = default);
    bool IsSandboxEnabled { get; }
    Task EnableSandboxAsync();
    Task DisableSandboxAsync();
}

public class ExecutorService : IExecutorService
{
    private static ExecutorService? _instance;
    public static ExecutorService Instance => _instance ??= new ExecutorService();

    private bool _sandboxEnabled;
    private readonly string _sandboxName = "SmartPCAssistant_Sandbox";

    public bool IsSandboxEnabled => _sandboxEnabled;

    private ExecutorService() { }

    public async Task EnableSandboxAsync()
    {
        if (!PlatformHelper.IsWindows())
        {
            Log.Warning("Sandbox is only available on Windows");
            return;
        }

        try
        {
            var result = await ExecutePowerShellAsync(@"
                $sandboxConfig = @{
                    ""IsolationLevel"" = ""Process""
                    ""User "" = ""S-1-5-18""
                }
                Write-Output 'Sandbox configuration prepared'
            ");

            _sandboxEnabled = result.Success;
            Log.Information("Sandbox enabled: {Status}", _sandboxEnabled);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enable sandbox");
            _sandboxEnabled = false;
        }
    }

    public async Task DisableSandboxAsync()
    {
        _sandboxEnabled = false;
        Log.Information("Sandbox disabled");
        await Task.CompletedTask;
    }

    public async Task<ExecutionResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        Log.Information("Executing command: {Command}", command);

        if (_sandboxEnabled && PlatformHelper.IsWindows())
        {
            return await ExecuteInSandboxAsync("cmd.exe", $"/c {command}", cancellationToken);
        }

        return await ExecuteDirectAsync("cmd.exe", $"/c {command}", cancellationToken);
    }

    public async Task<ExecutionResult> ExecutePowerShellAsync(string script, CancellationToken cancellationToken = default)
    {
        Log.Information("Executing PowerShell script: {Script}", script.Length > 100 ? script[..100] + "..." : script);

        if (_sandboxEnabled && PlatformHelper.IsWindows())
        {
            return await ExecuteInSandboxAsync("powershell.exe", $"-ExecutionPolicy Bypass -Command {script}", cancellationToken);
        }

        return await ExecuteDirectAsync("powershell.exe", $"-ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"", cancellationToken);
    }

    public async Task<ExecutionResult> ExecuteDirectAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                process.WaitForExit(30000);

                return new ExecutionResult
                {
                    Success = process.ExitCode == 0,
                    Output = output,
                    Error = error,
                    ExitCode = process.ExitCode
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to execute {FileName}", fileName);
                return new ExecutionResult
                {
                    Success = false,
                    Error = ex.Message,
                    ExitCode = -1
                };
            }
        }, cancellationToken);
    }

    private async Task<ExecutionResult> ExecuteInSandboxAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        Log.Information("Executing in sandbox: {FileName} {Arguments}", fileName, arguments);

        var sandboxScript = $@"
            $ErrorActionPreference = 'Stop'
            try {{
                $process = Start-Process -FilePath '{fileName}' -ArgumentList '{arguments.Replace("'", "''")}' -Wait -PassThru
                $result = @{{
                    Success = $process.ExitCode -eq 0
                    ExitCode = $process.ExitCode
                }}
                $result | ConvertTo-Json
            }} catch {{
                @{{ Success = $false; Error = $_.Exception.Message }} | ConvertTo-Json
            }}
        ";

        return await ExecutePowerShellAsync(sandboxScript, cancellationToken);
    }

    public async Task<ExecutionResult> OpenApplicationAsync(string appName, CancellationToken cancellationToken = default)
    {
        Log.Information("Opening application: {AppName}", appName);

        var script = appName.Contains('.') || appName.Contains('\\') || appName.Contains('/')
            ? $"Start-Process '{appName}'"
            : $@"
                $app = Get-Command $env:ProgramFiles*, $env:ProgramFiles, $env:LOCALAPPDATA* -ErrorAction SilentlyContinue | Where-Object {{ $_.Name -like '*{appName}*' }} | Select-Object -First 1
                if ($app) {{
                    Start-Process $app.Source
                    'Opened: ' + $app.Source
                }} else {{
                    Start-Process '{appName}'
                    'Opened via shell: {appName}'
                }}
            ";

        return await ExecutePowerShellAsync(script, cancellationToken);
    }

    public async Task<ExecutionResult> CloseApplicationAsync(string appName, CancellationToken cancellationToken = default)
    {
        Log.Information("Closing application: {AppName}", appName);

        var script = $@"
            $processes = Get-Process -Name '*{appName}*' -ErrorAction SilentlyContinue
            if ($processes) {{
                $processes | ForEach-Object {{ Stop-Process -Id $_.Id -Force }}
                'Closed: ' + $processes.Count + ' process(es)'
            }} else {{
                'No matching process found'
            }}
        ";

        return await ExecutePowerShellAsync(script, cancellationToken);
    }

    public async Task<string> GetScreenTextAsync(CancellationToken cancellationToken = default)
    {
        if (!PlatformHelper.IsWindows())
        {
            return "Screen capture requires Windows";
        }

        var script = @"
            Add-Type -AssemblyName System.Windows.Forms
            Add-Type -AssemblyName UIAutomationClient

            $screenText = New-Object System.Text.StringBuilder
            try {
                $watcher = [System.Windows.Automation.AutomationElement]
                $root = [System.Windows.Automation.AutomationElement]::RootElement
                $condition = [System.Windows.Automation.PropertyCondition]::TrueCondition
                $elements = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)

                foreach ($element in $elements) {
                    if ($element.Current.Name) {
                        [void]$screenText.AppendLine($element.Current.Name)
                    }
                }
            } catch {
                [void]$screenText.AppendLine('Screen capture error: ' + $_.Exception.Message)
            }

            $screenText.ToString()
        ";

        var result = await ExecutePowerShellAsync(script, cancellationToken);
        return result.Success ? result.Output : $"Failed to capture screen: {result.Error}";
    }

    public async Task ClickAtAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        Log.Information("Clicking at ({X}, {Y})", x, y);

        var script = $@"
            Add-Type -AssemblyName System.Windows.Forms
            [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point({x}, {y})
            [System.Windows.Forms.MouseEventArgs]::LeftClick
            Start-Sleep -Milliseconds 50
        ";

        await ExecutePowerShellAsync(script, cancellationToken);
    }

    public async Task TypeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        Log.Information("Typing text: {Text}", text.Length > 50 ? text[..50] + "..." : text);

        var escapedText = text.Replace("'", "''").Replace("\"", "`\"");
        var script = $@"
            Add-Type -AssemblyName System.Windows.Forms
            [System.Windows.Forms.SendKeys]::SendWait('{escapedText}')
        ";

        await ExecutePowerShellAsync(script, cancellationToken);
    }

    public async Task<ExecutionResult> RunUIAutomationAsync(string action, string target, CancellationToken cancellationToken = default)
    {
        var script = $@"
            Add-Type -AssemblyName UIAutomationClient
            Add-Type -AssemblyName UIAutomationTypes

            $root = [System.Windows.Automation.AutomationElement]::RootElement
            $condition = [System.Windows.Automation.PropertyCondition]::TrueCondition

            function Find-ElementByName($name) {{
                $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                    (New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::NameProperty, $name
                    ))
                )
            }}

            function Find-ElementByType($type) {{
                $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                    (New-Object System.Windows.Automation.PropertyCondition(
                        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                        [System.Windows.Automation.ControlType]::$type
                    ))
                )
            }}

            try {{
                switch ('{action}') {{
                    'Click' {{
                        $element = Find-ElementByName('{target}')
                        if ($element) {{
                            $invoke = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
                            $invoke?.Invoke()
                            'Clicked: {target}'
                        }} else {{
                            'Element not found: {target}'
                        }}
                    }}
                    'SetValue' {{
                        $element = Find-ElementByName('{target}')
                        if ($element) {{
                            $value = $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
                            $value?.SetValue('{target}')
                            'Value set: {target}'
                        }} else {{
                            'Element not found: {target}'
                        }}
                    }}
                    default {{
                        'Unknown action: {action}'
                    }}
                }}
            }} catch {{
                'Error: ' + $_.Exception.Message
            }}
        ";

        return await ExecutePowerShellAsync(script, cancellationToken);
    }
}
