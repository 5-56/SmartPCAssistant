using SmartPCAssistant.Models;
using Serilog;

namespace SmartPCAssistant.Services;

public class SessionService
{
    private static SessionService? _instance;
    public static SessionService Instance => _instance ??= new SessionService();

    private Session? _currentSession;
    private List<SessionMessage> _currentMessages = new();
    private List<TaskStep> _currentSteps = new();

    public Session? CurrentSession => _currentSession;
    public IReadOnlyList<SessionMessage> CurrentMessages => _currentMessages;
    public IReadOnlyList<TaskStep> CurrentSteps => _currentSteps;

    public event EventHandler<Session?>? SessionChanged;
    public event EventHandler<List<SessionMessage>>? MessagesChanged;
    public event EventHandler<List<TaskStep>>? StepsChanged;

    private SessionService() { }

    public async Task<Session> StartNewSessionAsync(string? title = null)
    {
        if (_currentSession != null && _currentSession.Status == SessionStatus.Active)
        {
            await EndCurrentSessionAsync(SessionStatus.Paused);
        }

        _currentSession = new Session
        {
            Title = title ?? "新对话",
            Status = SessionStatus.Active,
            StartedAt = DateTime.Now
        };

        _currentMessages = new List<SessionMessage>();
        _currentSteps = new List<TaskStep>();

        await DatabaseService.Instance.SaveSessionAsync(_currentSession);
        SessionChanged?.Invoke(this, _currentSession);
        MessagesChanged?.Invoke(this, _currentMessages);
        StepsChanged?.Invoke(this, _currentSteps);

        Log.Information("Started new session: {SessionId}", _currentSession.Id);
        return _currentSession;
    }

    public async Task EndCurrentSessionAsync(SessionStatus status = SessionStatus.Completed)
    {
        if (_currentSession == null) return;

        _currentSession.Status = status;
        _currentSession.EndedAt = DateTime.Now;
        _currentSession.Duration = (int)(_currentSession.EndedAt.Value - _currentSession.StartedAt).TotalSeconds;

        await DatabaseService.Instance.SaveSessionAsync(_currentSession);
        Log.Information("Ended session {SessionId} with status {Status}", _currentSession.Id, status);
    }

    public async Task AddUserMessageAsync(string content)
    {
        if (_currentSession == null)
        {
            await StartNewSessionAsync();
        }

        var message = new SessionMessage
        {
            SessionId = _currentSession!.Id,
            Role = MessageRole.User,
            Content = content,
            CreatedAt = DateTime.Now
        };

        _currentMessages.Add(message);
        await DatabaseService.Instance.SaveSessionMessageAsync(message);

        if (string.IsNullOrEmpty(_currentSession.Title) || _currentSession.Title == "新对话")
        {
            _currentSession.Title = content.Length > 50 ? content[..50] + "..." : content;
            await DatabaseService.Instance.SaveSessionAsync(_currentSession);
        }

        MessagesChanged?.Invoke(this, _currentMessages);
        SessionChanged?.Invoke(this, _currentSession);
    }

    public async Task AddAssistantMessageAsync(string content)
    {
        if (_currentSession == null) return;

        var message = new SessionMessage
        {
            SessionId = _currentSession.Id,
            Role = MessageRole.Assistant,
            Content = content,
            CreatedAt = DateTime.Now
        };

        _currentMessages.Add(message);
        await DatabaseService.Instance.SaveSessionMessageAsync(message);
        MessagesChanged?.Invoke(this, _currentMessages);
    }

    public async Task<TaskStep> AddTaskStepAsync(string description)
    {
        if (_currentSession == null)
        {
            await StartNewSessionAsync();
        }

        var step = new TaskStep
        {
            SessionId = _currentSession!.Id,
            StepNumber = _currentSteps.Count + 1,
            Description = description,
            Status = TaskStepStatus.Pending,
            StartedAt = DateTime.Now
        };

        _currentSteps.Add(step);
        await DatabaseService.Instance.SaveTaskStepAsync(step);
        StepsChanged?.Invoke(this, _currentSteps);
        return step;
    }

    public async Task UpdateTaskStepAsync(TaskStep step)
    {
        await DatabaseService.Instance.SaveTaskStepAsync(step);
        var index = _currentSteps.FindIndex(s => s.Id == step.Id);
        if (index >= 0)
        {
            _currentSteps[index] = step;
        }
        StepsChanged?.Invoke(this, _currentSteps);
    }

    public async Task<List<Session>> GetAllSessionsAsync()
    {
        return await DatabaseService.Instance.GetAllSessionsAsync();
    }

    public async Task LoadSessionAsync(string sessionId)
    {
        if (_currentSession != null && _currentSession.Status == SessionStatus.Active)
        {
            await EndCurrentSessionAsync(SessionStatus.Paused);
        }

        _currentSession = await DatabaseService.Instance.GetSessionAsync(sessionId);
        if (_currentSession != null)
        {
            _currentMessages = await DatabaseService.Instance.GetSessionMessagesAsync(sessionId);
            _currentSteps = await DatabaseService.Instance.GetTaskStepsAsync(sessionId);
        }
        else
        {
            _currentMessages = new List<SessionMessage>();
            _currentSteps = new List<TaskStep>();
        }

        SessionChanged?.Invoke(this, _currentSession);
        MessagesChanged?.Invoke(this, _currentMessages);
        StepsChanged?.Invoke(this, _currentSteps);
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        await DatabaseService.Instance.DeleteSessionAsync(sessionId);
        if (_currentSession?.Id == sessionId)
        {
            _currentSession = null;
            _currentMessages = new List<SessionMessage>();
            _currentSteps = new List<TaskStep>();
            SessionChanged?.Invoke(this, _currentSession);
            MessagesChanged?.Invoke(this, _currentMessages);
            StepsChanged?.Invoke(this, _currentSteps);
        }
    }

    public async Task<string> ProcessUserInputAsync(string userInput)
    {
        await AddUserMessageAsync(userInput);

        try
        {
            var response = await AiProviderService.Instance.SendMessageAsync(userInput, _currentMessages.ToList());
            await AddAssistantMessageAsync(response);
            return response;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing user input");
            var errorMessage = $"抱歉，发生了错误：{ex.Message}";
            await AddAssistantMessageAsync(errorMessage);
            return errorMessage;
        }
    }
}
