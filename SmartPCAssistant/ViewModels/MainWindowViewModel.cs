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

    public ObservableCollection<SessionMessage> Messages { get; } = new();
    public ObservableCollection<TaskStep> TaskSteps { get; } = new();
    public ObservableCollection<Session> SessionHistory { get; } = new();

    private readonly SessionService _sessionService;
    private Window? _mainWindow;
    private Window? _floatBallWindow;
    private CancellationTokenSource? _listeningCts;

    public MainWindowViewModel()
    {
        _sessionService = SessionService.Instance;
        _sessionService.SessionChanged += OnSessionChanged;
        _sessionService.MessagesChanged += OnMessagesChanged;
        _sessionService.StepsChanged += OnStepsChanged;

        _ = InitializeAsync();
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

            Log.Information("MainWindowViewModel initialized");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize MainWindowViewModel");
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

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsProcessing) return;

        var input = UserInput;
        UserInput = string.Empty;
        IsProcessing = true;

        try
        {
            await _sessionService.ProcessUserInputAsync(input);
            await LoadSessionHistoryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending message");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task NewSession()
    {
        await _sessionService.StartNewSessionAsync();
        await LoadSessionHistoryAsync();
    }

    [RelayCommand]
    private async Task LoadSession(Session? session)
    {
        if (session == null) return;
        await _sessionService.LoadSessionAsync(session.Id);
    }

    [RelayCommand]
    private async Task DeleteSession(Session? session)
    {
        if (session == null) return;
        await _sessionService.DeleteSessionAsync(session.Id);
        await LoadSessionHistoryAsync();
    }

    [RelayCommand]
    private async Task EndSession()
    {
        await _sessionService.EndCurrentSessionAsync();
        await LoadSessionHistoryAsync();
    }

    [RelayCommand]
    private void ToggleHistoryPanel()
    {
        IsHistoryPanelVisible = !IsHistoryPanelVisible;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow
        {
            DataContext = new SettingsViewModel()
        };
        settingsWindow.Show();
        IsSettingsOpen = true;
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
            Log.Information("Started listening");

            var speechService = SpeechService.Instance;
            var recognizedText = await speechService.RecognizeFromMicrophoneAsync(_listeningCts.Token);

            if (!string.IsNullOrWhiteSpace(recognizedText))
            {
                UserInput = recognizedText;
                await SendMessage();
            }

            ListeningStatus = "准备就绪";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Listening failed");
            ListeningStatus = "聆听失败";
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
            await SpeechService.Instance.SpeakAsync(lastAssistantMessage.Content);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Speech failed");
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
            ListeningStatus = "正在搜索和学习...";

            var response = await LearningService.Instance.LearnAndExecuteAsync(UserInput);
            await _sessionService.AddAssistantMessageAsync(response);
            await LoadSessionHistoryAsync();

            ListeningStatus = "准备就绪";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Search and learn failed");
            ListeningStatus = "学习失败";
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
            ListeningStatus = "正在执行...";

            var steps = await LearningService.Instance.GetStepByStepGuideAsync(UserInput);

            foreach (var step in steps)
            {
                await _sessionService.AddTaskStepAsync(step);
                ListeningStatus = $"执行: {step}";
            }

            ListeningStatus = "执行完成";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Execute task failed");
            ListeningStatus = "执行失败";
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
