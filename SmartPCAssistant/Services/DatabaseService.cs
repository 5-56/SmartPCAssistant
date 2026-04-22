using Microsoft.Data.Sqlite;
using SmartPCAssistant.Models;
using Serilog;

namespace SmartPCAssistant.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private static DatabaseService? _instance;
    public static DatabaseService Instance => _instance ??= new DatabaseService();

    private DatabaseService()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPCAssistant",
            "data.db");

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Session (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Status TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT,
                Duration INTEGER,
                Result TEXT,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SessionMessage (
                Id TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (SessionId) REFERENCES Session(Id)
            );

            CREATE TABLE IF NOT EXISTS TaskStep (
                Id TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                StepNumber INTEGER NOT NULL,
                Description TEXT NOT NULL,
                Status TEXT NOT NULL,
                StartedAt TEXT,
                CompletedAt TEXT,
                Result TEXT,
                FOREIGN KEY (SessionId) REFERENCES Session(Id)
            );

            CREATE TABLE IF NOT EXISTS AiProvider (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ProviderType TEXT NOT NULL,
                ApiUrl TEXT,
                ApiKey TEXT,
                DefaultModel TEXT,
                IsDefault INTEGER DEFAULT 0,
                Priority INTEGER DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            );
            """;
        command.ExecuteNonQuery();
        Log.Information("Database initialized at {Path}", _connectionString);
    }

    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<List<Session>> GetAllSessionsAsync()
    {
        var sessions = new List<Session>();
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Session ORDER BY CreatedAt DESC";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new Session
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Status = Enum.Parse<SessionStatus>(reader.GetString(2)),
                StartedAt = DateTime.Parse(reader.GetString(3)),
                EndedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                Duration = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                Result = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = DateTime.Parse(reader.GetString(7))
            });
        }
        return sessions;
    }

    public async Task<Session?> GetSessionAsync(string id)
    {
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Session WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Session
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Status = Enum.Parse<SessionStatus>(reader.GetString(2)),
                StartedAt = DateTime.Parse(reader.GetString(3)),
                EndedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                Duration = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                Result = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = DateTime.Parse(reader.GetString(7))
            };
        }
        return null;
    }

    public async Task SaveSessionAsync(Session session)
    {
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO Session (Id, Title, Status, StartedAt, EndedAt, Duration, Result, CreatedAt)
            VALUES (@Id, @Title, @Status, @StartedAt, @EndedAt, @Duration, @Result, @CreatedAt)
            """;
        command.Parameters.AddWithValue("@Id", session.Id);
        command.Parameters.AddWithValue("@Title", session.Title);
        command.Parameters.AddWithValue("@Status", session.Status.ToString());
        command.Parameters.AddWithValue("@StartedAt", session.StartedAt.ToString("o"));
        command.Parameters.AddWithValue("@EndedAt", session.EndedAt?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Duration", session.Duration);
        command.Parameters.AddWithValue("@Result", session.Result ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", session.CreatedAt.ToString("o"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<SessionMessage>> GetSessionMessagesAsync(string sessionId)
    {
        var messages = new List<SessionMessage>();
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM SessionMessage WHERE SessionId = @SessionId ORDER BY CreatedAt";
        command.Parameters.AddWithValue("@SessionId", sessionId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new SessionMessage
            {
                Id = reader.GetString(0),
                SessionId = reader.GetString(1),
                Role = Enum.Parse<MessageRole>(reader.GetString(2)),
                Content = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4))
            });
        }
        return messages;
    }

    public async Task SaveSessionMessageAsync(SessionMessage message)
    {
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO SessionMessage (Id, SessionId, Role, Content, CreatedAt)
            VALUES (@Id, @SessionId, @Role, @Content, @CreatedAt)
            """;
        command.Parameters.AddWithValue("@Id", message.Id);
        command.Parameters.AddWithValue("@SessionId", message.SessionId);
        command.Parameters.AddWithValue("@Role", message.Role.ToString());
        command.Parameters.AddWithValue("@Content", message.Content);
        command.Parameters.AddWithValue("@CreatedAt", message.CreatedAt.ToString("o"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveTaskStepAsync(TaskStep step)
    {
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO TaskStep (Id, SessionId, StepNumber, Description, Status, StartedAt, CompletedAt, Result)
            VALUES (@Id, @SessionId, @StepNumber, @Description, @Status, @StartedAt, @CompletedAt, @Result)
            """;
        command.Parameters.AddWithValue("@Id", step.Id);
        command.Parameters.AddWithValue("@SessionId", step.SessionId);
        command.Parameters.AddWithValue("@StepNumber", step.StepNumber);
        command.Parameters.AddWithValue("@Description", step.Description);
        command.Parameters.AddWithValue("@Status", step.Status.ToString());
        command.Parameters.AddWithValue("@StartedAt", step.StartedAt?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CompletedAt", step.CompletedAt?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Result", step.Result ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<TaskStep>> GetTaskStepsAsync(string sessionId)
    {
        var steps = new List<TaskStep>();
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM TaskStep WHERE SessionId = @SessionId ORDER BY StepNumber";
        command.Parameters.AddWithValue("@SessionId", sessionId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            steps.Add(new TaskStep
            {
                Id = reader.GetString(0),
                SessionId = reader.GetString(1),
                StepNumber = reader.GetInt32(2),
                Description = reader.GetString(3),
                Status = Enum.Parse<TaskStepStatus>(reader.GetString(4)),
                StartedAt = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                CompletedAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                Result = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        return steps;
    }

    public async Task<List<AiProvider>> GetAiProvidersAsync()
    {
        var providers = new List<AiProvider>();
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AiProvider ORDER BY Priority DESC";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            providers.Add(new AiProvider
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                ProviderType = Enum.Parse<AiProviderType>(reader.GetString(2)),
                ApiUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                ApiKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                DefaultModel = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsDefault = reader.GetInt32(6) == 1,
                Priority = reader.GetInt32(7),
                CreatedAt = DateTime.Parse(reader.GetString(8)),
                UpdatedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9))
            });
        }
        return providers;
    }

    public async Task SaveAiProviderAsync(AiProvider provider)
    {
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO AiProvider (Id, Name, ProviderType, ApiUrl, ApiKey, DefaultModel, IsDefault, Priority, CreatedAt, UpdatedAt)
            VALUES (@Id, @Name, @ProviderType, @ApiUrl, @ApiKey, @DefaultModel, @IsDefault, @Priority, @CreatedAt, @UpdatedAt)
            """;
        command.Parameters.AddWithValue("@Id", provider.Id);
        command.Parameters.AddWithValue("@Name", provider.Name);
        command.Parameters.AddWithValue("@ProviderType", provider.ProviderType.ToString());
        command.Parameters.AddWithValue("@ApiUrl", provider.ApiUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ApiKey", provider.ApiKey ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@DefaultModel", provider.DefaultModel ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsDefault", provider.IsDefault ? 1 : 0);
        command.Parameters.AddWithValue("@Priority", provider.Priority);
        command.Parameters.AddWithValue("@CreatedAt", provider.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@UpdatedAt", provider.UpdatedAt?.ToString("o") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteSessionAsync(string id)
    {
        await using var connection = GetConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Session WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();

        await using var command2 = connection.CreateCommand();
        command2.CommandText = "DELETE FROM SessionMessage WHERE SessionId = @SessionId";
        command2.Parameters.AddWithValue("@SessionId", id);
        await command2.ExecuteNonQueryAsync();

        await using var command3 = connection.CreateCommand();
        command3.CommandText = "DELETE FROM TaskStep WHERE SessionId = @SessionId";
        command3.Parameters.AddWithValue("@SessionId", id);
        await command3.ExecuteNonQueryAsync();
    }
}
