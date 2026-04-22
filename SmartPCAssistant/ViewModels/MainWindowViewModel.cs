using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartPCAssistant.Models;
using SmartPCAssistant.Services;
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
    private Session? _currentSession;

    [ObservableProperty]
    private bool _isHistoryPanelVisible = true;

    public ObservableCollection<SessionMessage> Messages { get; } = new();
    public ObservableCollection<TaskStep> TaskSteps { get; } = new();
    public ObservableCollection<Session> SessionHistory { get; } = new();

    private readonly SessionService _sessionService;

    public MainWindowViewModel()
    {
        _sessionService = SessionService.Instance;
        _sessionService.SessionChanged += OnSessionChanged;
        _sessionService.MessagesChanged += OnMessagesChanged;
        _sessionService.StepsChanged += OnStepsChanged;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
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
        Log.Information("Opening settings...");
    }
}
