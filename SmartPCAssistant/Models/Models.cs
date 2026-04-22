namespace SmartPCAssistant.Models;

public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? EndedAt { get; set; }
    public int Duration { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public enum SessionStatus
{
    Active,
    Completed,
    Paused
}

public class SessionMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public enum MessageRole
{
    User,
    Assistant,
    System
}

public class TaskStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public TaskStepStatus Status { get; set; } = TaskStepStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
}

public enum TaskStepStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public class AiProvider
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public AiProviderType ProviderType { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultModel { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}

public enum AiProviderType
{
    OpenAI,
    Claude,
    Gemini,
    DeepSeek,
    Custom
}
