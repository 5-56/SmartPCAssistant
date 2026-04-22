using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SmartPCAssistant.Models;
using Serilog;

namespace SmartPCAssistant.Services;

public class AiProviderService
{
    private static AiProviderService? _instance;
    public static AiProviderService Instance => _instance ??= new AiProviderService();

    private readonly HttpClient _httpClient;
    private AiProvider? _currentProvider;

    private AiProviderService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task InitializeAsync()
    {
        var providers = await DatabaseService.Instance.GetAiProvidersAsync();
        _currentProvider = providers.FirstOrDefault(p => p.IsDefault) ?? providers.FirstOrDefault();

        if (_currentProvider == null)
        {
            _currentProvider = new AiProvider
            {
                Name = "OpenAI",
                ProviderType = AiProviderType.OpenAI,
                ApiUrl = "https://api.openai.com/v1",
                DefaultModel = "gpt-4",
                IsDefault = true,
                Priority = 1
            };
            await DatabaseService.Instance.SaveAiProviderAsync(_currentProvider);
        }
        Log.Information("AI Provider initialized: {Provider}", _currentProvider.Name);
    }

    public void SetProvider(AiProvider provider)
    {
        _currentProvider = provider;
        Log.Information("Switched to AI Provider: {Provider}", provider.Name);
    }

    public List<AiProvider> GetAvailableProviders()
    {
        return Enum.GetValues<AiProviderType>()
            .Where(t => t != AiProviderType.Custom)
            .Select(t => new AiProvider { Name = t.ToString(), ProviderType = t })
            .ToList();
    }

    public async Task<string> SendMessageAsync(string userMessage, List<SessionMessage> conversationHistory)
    {
        if (_currentProvider == null)
        {
            await InitializeAsync();
        }

        return _currentProvider!.ProviderType switch
        {
            AiProviderType.OpenAI => await SendToOpenAIAsync(userMessage, conversationHistory),
            AiProviderType.Claude => await SendToClaudeAsync(userMessage, conversationHistory),
            AiProviderType.DeepSeek => await SendToDeepSeekAsync(userMessage, conversationHistory),
            AiProviderType.Gemini => await SendToGeminiAsync(userMessage, conversationHistory),
            AiProviderType.Custom => await SendToCustomAsync(userMessage, conversationHistory),
            _ => throw new NotSupportedException($"Provider {_currentProvider.ProviderType} not supported")
        };
    }

    private async Task<string> SendToOpenAIAsync(string userMessage, List<SessionMessage> history)
    {
        var messages = history.Select(m => new
        {
            role = m.Role == MessageRole.User ? "user" : "assistant",
            content = m.Content
        }).ToList();

        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = _currentProvider!.DefaultModel ?? "gpt-4",
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_currentProvider.ApiUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_currentProvider.ApiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task<string> SendToClaudeAsync(string userMessage, List<SessionMessage> history)
    {
        var messages = history.Select(m => new
        {
            role = m.Role == MessageRole.User ? "user" : "assistant",
            content = m.Content
        }).ToList();

        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = _currentProvider!.DefaultModel ?? "claude-3-opus-20240229",
            max_tokens = 4096,
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_currentProvider.ApiUrl}/v1/messages");
        request.Headers.Add("x-api-key", _currentProvider.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    private async Task<string> SendToDeepSeekAsync(string userMessage, List<SessionMessage> history)
    {
        var messages = history.Select(m => new
        {
            role = m.Role == MessageRole.User ? "user" : "assistant",
            content = m.Content
        }).ToList();

        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = _currentProvider!.DefaultModel ?? "deepseek-chat",
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_currentProvider.ApiUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_currentProvider.ApiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task<string> SendToGeminiAsync(string userMessage, List<SessionMessage> history)
    {
        var contents = history.Select(m => new
        {
            role = m.Role == MessageRole.User ? "user" : "model",
            parts = new[] { new { text = m.Content } }
        }).ToList();

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = userMessage } }
        });

        var requestBody = new { contents = contents };

        var apiKey = _currentProvider!.ApiKey;
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_currentProvider.ApiUrl}/v1beta/models/{_currentProvider.DefaultModel ?? "gemini-pro"}:generateContent?key={apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
    }

    private async Task<string> SendToCustomAsync(string userMessage, List<SessionMessage> history)
    {
        var messages = history.Select(m => new
        {
            role = m.Role == MessageRole.User ? "user" : "assistant",
            content = m.Content
        }).ToList();

        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = _currentProvider!.DefaultModel ?? "gpt-4",
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_currentProvider.ApiUrl}/chat/completions");
        if (!string.IsNullOrEmpty(_currentProvider.ApiKey))
        {
            request.Headers.Add("Authorization", $"Bearer {_currentProvider.ApiKey}");
        }
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    public List<string> GetAvailableModels()
    {
        if (_currentProvider == null) return new List<string>();

        return _currentProvider.ProviderType switch
        {
            AiProviderType.OpenAI => new List<string> { "gpt-4", "gpt-4-turbo", "gpt-3.5-turbo" },
            AiProviderType.Claude => new List<string> { "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307" },
            AiProviderType.DeepSeek => new List<string> { "deepseek-chat", "deepseek-coder" },
            AiProviderType.Gemini => new List<string> { "gemini-pro", "gemini-pro-vision" },
            _ => new List<string>()
        };
    }
}
