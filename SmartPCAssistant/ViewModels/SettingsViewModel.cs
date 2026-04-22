using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartPCAssistant.Models;
using SmartPCAssistant.Services;
using Serilog;

namespace SmartPCAssistant.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _apiUrl = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _selectedProvider = "OpenAI";

    [ObservableProperty]
    private string _selectedModel = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private int _priority;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isVoiceEnabled;

    [ObservableProperty]
    private bool _isSandboxEnabled;

    [ObservableProperty]
    private string _selectedBrowser = "Chrome";

    public ObservableCollection<string> AvailableProviders { get; } = new()
    {
        "OpenAI",
        "Claude",
        "DeepSeek",
        "Gemini",
        "Custom"
    };

    public ObservableCollection<string> AvailableModels { get; } = new()
    {
        "gpt-4",
        "gpt-4-turbo",
        "gpt-3.5-turbo"
    };

    public ObservableCollection<string> AvailableBrowsers { get; } = new()
    {
        "Chrome",
        "Edge",
        "Firefox",
        "Brave"
    };

    public SettingsViewModel()
    {
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var providers = await DatabaseService.Instance.GetAiProvidersAsync();
            var defaultProvider = providers.FirstOrDefault(p => p.IsDefault) ?? providers.FirstOrDefault();

            if (defaultProvider != null)
            {
                SelectedProvider = defaultProvider.ProviderType.ToString();
                ApiUrl = defaultProvider.ApiUrl ?? string.Empty;
                ApiKey = defaultProvider.ApiKey ?? string.Empty;
                SelectedModel = defaultProvider.DefaultModel ?? string.Empty;
                IsDefault = defaultProvider.IsDefault;
                Priority = defaultProvider.Priority;
            }

            IsVoiceEnabled = true;
            IsSandboxEnabled = ExecutorService.Instance.IsSandboxEnabled;
            SelectedBrowser = LearningService.Instance.GetDefaultBrowser();

            UpdateAvailableModels();
            Log.Information("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load settings");
            StatusMessage = "加载设置失败";
        }
    }

    partial void OnSelectedProviderChanged(string value)
    {
        UpdateAvailableModels();
    }

    private void UpdateAvailableModels()
    {
        AvailableModels.Clear();

        var models = SelectedProvider switch
        {
            "OpenAI" => new[] { "gpt-4", "gpt-4-turbo", "gpt-4o", "gpt-3.5-turbo" },
            "Claude" => new[] { "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307" },
            "DeepSeek" => new[] { "deepseek-chat", "deepseek-coder" },
            "Gemini" => new[] { "gemini-pro", "gemini-pro-vision", "gemini-1.5-pro" },
            "Custom" => new[] { "custom-model" },
            _ => new[] { "gpt-4" }
        };

        foreach (var model in models)
        {
            AvailableModels.Add(model);
        }

        if (SelectedModel == string.Empty || !models.Contains(SelectedModel))
        {
            SelectedModel = models.FirstOrDefault() ?? string.Empty;
        }
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "API Key 不能为空";
            return;
        }

        IsSaving = true;
        StatusMessage = "正在保存...";

        try
        {
            var providerType = Enum.Parse<AiProviderType>(SelectedProvider);
            var apiBaseUrl = ApiUrl;

            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                apiBaseUrl = providerType switch
                {
                    AiProviderType.OpenAI => "https://api.openai.com/v1",
                    AiProviderType.Claude => "https://api.anthropic.com",
                    AiProviderType.DeepSeek => "https://api.deepseek.com",
                    AiProviderType.Gemini => "https://generativelanguage.googleapis.com",
                    _ => "https://api.openai.com/v1"
                };
            }

            var providers = await DatabaseService.Instance.GetAiProvidersAsync();
            var existingProvider = providers.FirstOrDefault(p =>
                p.ProviderType == providerType ||
                (providerType == AiProviderType.Custom && p.ApiUrl == apiBaseUrl));

            if (existingProvider != null)
            {
                existingProvider.ApiUrl = apiBaseUrl;
                existingProvider.ApiKey = ApiKey;
                existingProvider.DefaultModel = SelectedModel;
                existingProvider.IsDefault = IsDefault;
                existingProvider.Priority = Priority;
                existingProvider.UpdatedAt = DateTime.Now;
                await DatabaseService.Instance.SaveAiProviderAsync(existingProvider);
                AiProviderService.Instance.SetProvider(existingProvider);
            }
            else
            {
                var newProvider = new AiProvider
                {
                    Name = SelectedProvider,
                    ProviderType = providerType,
                    ApiUrl = apiBaseUrl,
                    ApiKey = ApiKey,
                    DefaultModel = SelectedModel,
                    IsDefault = IsDefault,
                    Priority = Priority
                };
                await DatabaseService.Instance.SaveAiProviderAsync(newProvider);
                AiProviderService.Instance.SetProvider(newProvider);
            }

            LearningService.Instance.SetBrowser(SelectedBrowser);

            if (IsSandboxEnabled)
            {
                await ExecutorService.Instance.EnableSandboxAsync();
            }
            else
            {
                await ExecutorService.Instance.DisableSandboxAsync();
            }

            StatusMessage = "设置已保存";
            Log.Information("Settings saved successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
            StatusMessage = $"保存失败: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "请先输入 API Key";
            return;
        }

        StatusMessage = "正在测试连接...";

        try
        {
            var testMessage = new SessionMessage
            {
                Role = MessageRole.User,
                Content = "Hello, respond with 'OK' if you can read this."
            };

            var response = await AiProviderService.Instance.SendMessageAsync(testMessage.Content, new System.Collections.Generic.List<SessionMessage> { testMessage });

            if (!string.IsNullOrEmpty(response) && !response.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "连接成功!";
            }
            else
            {
                StatusMessage = "连接失败，请检查配置";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Connection test failed");
            StatusMessage = $"连接失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        SelectedProvider = "OpenAI";
        ApiUrl = "https://api.openai.com/v1";
        ApiKey = string.Empty;
        SelectedModel = "gpt-4";
        IsDefault = true;
        Priority = 1;
        IsVoiceEnabled = true;
        IsSandboxEnabled = false;
        SelectedBrowser = "Chrome";
        StatusMessage = "已重置为默认值";
    }
}
