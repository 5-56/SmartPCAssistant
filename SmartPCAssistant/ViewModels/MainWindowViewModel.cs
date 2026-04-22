using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartPCAssistant.Models;
using SmartPCAssistant.Services;
using SmartPCAssistant.Views;
using Serilog;

namespace SmartPCAssistant.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _userInput = string.Empty;

    [ObservableProperty]
    private string _currentTaskTitle = string.Empty;

    [ObservableProperty]
    private int _taskProgress;

    [ObservableProperty]
    private string _taskStatus = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isListening;

    [ObservableProperty]
    private Session? _currentSession;

    [ObservableProperty]
    private bool _isHistoryPanelVisible = true;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private string _listeningStatus = "准备就绪";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<SessionMessage> Messages { get; } = new();
    public ObservableCollection<TaskStep> TaskSteps { get; } = new();
    public ObservableCollection<Session> SessionHistory { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();

    private readonly SessionService _sessionService;
    private readonly TaskEngine _taskEngine;
    private Window? _mainWindow;
    private Window? _floatBallWindow;
    private CancellationTokenSource? _listeningCts;

    public MainWindowViewModel()
    {
        _sessionService = SessionService.Instance;
        _taskEngine = TaskEngine.Instance;
        _sessionService.SessionChanged += OnSessionChanged;
        _sessionService.MessagesChanged += OnMessagesChanged;
        _sessionService.StepsChanged += OnStepsChanged;

        InitializeModels();
        _ = InitializeAsync();
    }

    private void InitializeModels()
    {
        AvailableModels.Clear();
        foreach (var model in AiProviderService.Instance.GetAvailableModels())
        {
            AvailableModels.Add(model);
        }
    }

    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
    }

    public void SetFloatBallWindow(Window window)
    {
        _floatBallWindow = window;
    }

    private async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "正在初始化...";
            await AiProviderService.Instance.InitializeAsync();
            await LoadSessionHistoryAsync();

            if (_sessionService.CurrentSession == null)
            {
                await _sessionService.StartNewSessionAsync();
            }
            else
            {
                UpdateFromCurrentSession();
            }

            StatusMessage = "就绪";
            Log.Information("MainWindowViewModel initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize MainWindowViewModel");
            StatusMessage = "初始化失败";
        }
    }

    private void OnSessionChanged(object? sender, Session? session)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentSession = session;
            CurrentTaskTitle = session?.Title ?? string.Empty;
        });
    }

    private void OnMessagesChanged(object? sender, List<SessionMessage> messages)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Messages.Clear();
            foreach (var msg in messages)
            {
                Messages.Add(msg);
            }
        });
    }

    private void OnStepsChanged(object? sender, List<TaskStep> steps)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            TaskSteps.Clear();
            foreach (var step in steps)
            {
                TaskSteps.Add(step);
            }
            UpdateTaskProgress();
        });
    }

    private void UpdateTaskProgress()
    {
        if (TaskSteps.Count == 0)
        {
            TaskProgress = 0;
            TaskStatus = string.Empty;
            return;
        }

        var completed = TaskSteps.Count(s => s.Status == TaskStepStatus.Completed);
        TaskProgress = (int)((double)completed / TaskSteps.Count * 100);
        TaskStatus = $"{completed}/{TaskSteps.Count} 步骤完成";
    }

    private void UpdateFromCurrentSession()
    {
        if (_sessionService.CurrentSession == null) return;

        CurrentSession = _sessionService.CurrentSession;
        CurrentTaskTitle = _sessionService.CurrentSession.Title;

        Messages.Clear();
        foreach (var msg in _sessionService.CurrentMessages)
        {
            Messages.Add(msg);
        }

        TaskSteps.Clear();
        foreach (var step in _sessionService.CurrentSteps)
        {
            TaskSteps.Add(step);
        }

        UpdateTaskProgress();
    }

    private async Task LoadSessionHistoryAsync()
    {
        try
        {
            var sessions = await _sessionService.GetAllSessionsAsync();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                SessionHistory.Clear();
                foreach (var session in sessions)
                {
                    SessionHistory.Add(session);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load session history");
        }
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsProcessing) return;

        var input = UserInput;
        UserInput = string.Empty;
        IsProcessing = true;
        StatusMessage = "处理中...";

        try
        {
            var request = _taskEngine.ParseRequest(input);
            Log.Information("Parsed task type: {Type}, target: {Target}", request.Type, request.Target);

            if (request.Type != TaskType.Unknown)
            {
                StatusMessage = $"执行任务: {request.Type}";
                var result = await _taskEngine.ExecuteAsync(request);

                if (result.Success)
                {
                    await _sessionService.AddAssistantMessageAsync(result.Message);
                    foreach (var step in result.Steps)
                    {
                        await _sessionService.AddTaskStepAsync(step);
                    }
                }
                else
                {
                    await _sessionService.AddAssistantMessageAsync($"执行失败: {result.Error}");
                }
            }
            else
            {
                await _sessionService.ProcessUserInputAsync(input);
            }

            await LoadSessionHistoryAsync();
            StatusMessage = "完成";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing message");
            StatusMessage = $"错误: {ex.Message}";
            await _sessionService.AddAssistantMessageAsync($"发生错误: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task NewSession()
    {
        try
        {
            StatusMessage = "创建新对话...";
            await _sessionService.StartNewSessionAsync();
            await LoadSessionHistoryAsync();
            StatusMessage = "就绪";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create new session");
            StatusMessage = "创建失败";
        }
    }

    [RelayCommand]
    private async Task LoadSession(Session? session)
    {
        if (session == null) return;
        try
        {
            StatusMessage = "加载对话...";
            await _sessionService.LoadSessionAsync(session.Id);
            StatusMessage = "就绪";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load session");
            StatusMessage = "加载失败";
        }
    }

    [RelayCommand]
    private async Task DeleteSession(Session? session)
    {
        if (session == null) return;
        try
        {
            await _sessionService.DeleteSessionAsync(session.Id);
            await LoadSessionHistoryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete session");
        }
    }

    [RelayCommand]
    private async Task EndSession()
    {
        try
        {
            StatusMessage = "结束对话...";
            await _sessionService.EndCurrentSessionAsync();
            await LoadSessionHistoryAsync();
            StatusMessage = "就绪";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to end session");
            StatusMessage = "结束失败";
        }
    }

    [RelayCommand]
    private void ToggleHistoryPanel()
    {
        IsHistoryPanelVisible = !IsHistoryPanelVisible;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        try
        {
            var settingsWindow = new SettingsWindow
            {
                DataContext = new SettingsViewModel()
            };
            settingsWindow.Show();
            IsSettingsOpen = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open settings");
        }
    }

    [RelayCommand]
    private async Task ToggleListening()
    {
        if (IsListening)
        {
            StopListening();
        }
        else
        {
            await StartListeningAsync();
        }
    }

    private async Task StartListeningAsync()
    {
        if (IsListening) return;

        try
        {
            _listeningCts = new CancellationTokenSource();
            IsListening = true;
            ListeningStatus = "正在聆听...";
            StatusMessage = "语音输入中...";
            Log.Information("Started listening");

            var recognizedText = await SpeechService.Instance.RecognizeFromMicrophoneAsync(_listeningCts.Token);

            if (!string.IsNullOrWhiteSpace(recognizedText))
            {
                UserInput = recognizedText;
                await SendMessage();
            }

            ListeningStatus = "准备就绪";
            StatusMessage = "就绪";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Listening failed");
            ListeningStatus = "聆听失败";
            StatusMessage = "语音输入失败";
        }
        finally
        {
            IsListening = false;
        }
    }

    private void StopListening()
    {
        _listeningCts?.Cancel();
        _listeningCts?.Dispose();
        _listeningCts = null;
        IsListening = false;
        ListeningStatus = "已停止";
        StatusMessage = "就绪";
        Log.Information("Stopped listening");
    }

    [RelayCommand]
    private async Task SpeakLastResponse()
    {
        if (Messages.Count == 0) return;

        var lastAssistantMessage = Messages.LastOrDefault(m => m.Role == MessageRole.Assistant);
        if (lastAssistantMessage == null) return;

        try
        {
            StatusMessage = "朗读中...";
            await SpeechService.Instance.SpeakAsync(lastAssistantMessage.Content);
            StatusMessage = "完成";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Speech failed");
            StatusMessage = "朗读失败";
        }
    }

    [RelayCommand]
    private void ShowFloatBall()
    {
        if (_floatBallWindow == null)
        {
            _floatBallWindow = new FloatBallWindow();
        }
        _floatBallWindow.Show();
        Log.Information("Float ball shown");
    }

    [RelayCommand]
    private void HideFloatBall()
    {
        _floatBallWindow?.Hide();
        Log.Information("Float ball hidden");
    }

    [RelayCommand]
    private async Task SearchAndLearn()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;

        try
        {
            IsProcessing = true;
            StatusMessage = "搜索和学习...";
            ListeningStatus = "正在搜索和学习...";

            var response = await LearningService.Instance.LearnAndExecuteAsync(UserInput);
            await _sessionService.AddAssistantMessageAsync(response);
            await LoadSessionHistoryAsync();

            ListeningStatus = "准备就绪";
            StatusMessage = "完成";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Search and learn failed");
            ListeningStatus = "学习失败";
            StatusMessage = "搜索失败";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task ExecuteTask()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;

        try
        {
            IsProcessing = true;
            StatusMessage = "执行任务...";
            ListeningStatus = "正在执行...";

            var steps = await LearningService.Instance.GetStepByStepGuideAsync(UserInput);

            foreach (var step in steps)
            {
                await _sessionService.AddTaskStepAsync(step);
                ListeningStatus = $"执行: {step}";
            }

            ListeningStatus = "执行完成";
            StatusMessage = "完成";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Execute task failed");
            ListeningStatus = "执行失败";
            StatusMessage = "执行失败";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void OpenLogViewer()
    {
        try
        {
            var logViewerWindow = new LogViewerWindow
            {
                DataContext = new LogViewerViewModel()
            };
            logViewerWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open log viewer");
        }
    }
}
