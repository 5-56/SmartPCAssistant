using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace SmartPCAssistant.Services;

public class AppConfig
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "zh-CN";
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowFloatBall { get; set; } = true;
    public string FloatBallPosition { get; set; } = "RightBottom";
    public string DefaultBrowser { get; set; } = "Chrome";
    public bool VoiceEnabled { get; set; } = true;
    public bool SandboxEnabled { get; set; } = false;
    public string GlobalHotkey { get; set; } = "Alt+Space";
    public int MaxHistorySessions { get; set; } = 100;
    public bool AutoSpeakResponse { get; set; } = false;
}

public interface IConfigService
{
    AppConfig Config { get; }
    void Load();
    void Save();
    void Update(Action<AppConfig> updateAction);
    string GetConfigPath();
}

public class ConfigService : IConfigService
{
    private static ConfigService? _instance;
    public static ConfigService Instance => _instance ??= new ConfigService();

    private readonly string _configPath;
    private AppConfig _config;

    public AppConfig Config => _config;

    private ConfigService()
    {
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPCAssistant",
            "config.json");

        _config = new AppConfig();
        Load();
    }

    public string GetConfigPath() => _configPath;

    public void Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                Log.Information("Configuration loaded from {Path}", _configPath);
            }
            else
            {
                _config = new AppConfig();
                Save();
                Log.Information("Created default configuration at {Path}", _configPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load configuration, using defaults");
            _config = new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_configPath, json);
            Log.Information("Configuration saved to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save configuration");
        }
    }

    public void Update(Action<AppConfig> updateAction)
    {
        updateAction(_config);
        Save();
    }
}
