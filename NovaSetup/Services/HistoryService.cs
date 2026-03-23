using System.Globalization;
using Microsoft.Data.Sqlite;
using NovaSetup.Models;

namespace NovaSetup.Services;

public sealed class HistoryService
{
    private readonly LoggingService? _loggingService;

    public HistoryService(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            InitializeDatabase();
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to initialize install history database: {ex.Message}");
        }
    }

    private static string DatabasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaSetup", "history.db");

    private static string ConnectionString => $"Data Source={DatabasePath}";

    public async Task RecordInstallAsync(InstallRecord record)
    {
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO InstallHistory (
                    AppId,
                    AppName,
                    Version,
                    Platform,
                    InstallMethod,
                    Success,
                    ErrorMessage,
                    InstalledAt,
                    ElapsedMs
                )
                VALUES (
                    $appId,
                    $appName,
                    $version,
                    $platform,
                    $installMethod,
                    $success,
                    $errorMessage,
                    $installedAt,
                    $elapsedMs
                );
                """;
            command.Parameters.AddWithValue("$appId", record.AppId ?? string.Empty);
            command.Parameters.AddWithValue("$appName", record.AppName ?? string.Empty);
            command.Parameters.AddWithValue("$version", record.Version ?? string.Empty);
            command.Parameters.AddWithValue("$platform", record.Platform ?? string.Empty);
            command.Parameters.AddWithValue("$installMethod", record.InstallMethod ?? string.Empty);
            command.Parameters.AddWithValue("$success", record.Success ? 1 : 0);
            command.Parameters.AddWithValue("$errorMessage", record.ErrorMessage ?? string.Empty);
            command.Parameters.AddWithValue("$installedAt", record.InstalledAt.ToUniversalTime().ToString("O"));
            command.Parameters.AddWithValue("$elapsedMs", record.ElapsedMs);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to record install history for '{record.AppName}': {ex.Message}");
        }
    }

    public async Task<List<InstallRecord>> GetHistoryAsync(int limit = 100)
    {
        var records = new List<InstallRecord>();

        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, AppId, AppName, Version, Platform, InstallMethod, Success, ErrorMessage, InstalledAt, ElapsedMs
                FROM InstallHistory
                ORDER BY InstalledAt DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", limit);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(MapRecord(reader));
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to read install history: {ex.Message}");
        }

        return records;
    }

    public async Task<List<InstallRecord>> GetHistoryForAppAsync(string appId)
    {
        var records = new List<InstallRecord>();

        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, AppId, AppName, Version, Platform, InstallMethod, Success, ErrorMessage, InstalledAt, ElapsedMs
                FROM InstallHistory
                WHERE AppId = $appId
                ORDER BY InstalledAt DESC;
                """;
            command.Parameters.AddWithValue("$appId", appId ?? string.Empty);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(MapRecord(reader));
            }
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to read install history for '{appId}': {ex.Message}");
        }

        return records;
    }

    public async Task ClearHistoryAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM InstallHistory;";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to clear install history: {ex.Message}");
        }
    }

    public async Task<int> GetTotalInstallCountAsync()
    {
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM InstallHistory WHERE Success = 1;";
            var scalar = await command.ExecuteScalarAsync();
            return scalar is long longValue ? (int)longValue : Convert.ToInt32(scalar ?? 0, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Failed to count successful installs: {ex.Message}");
            return 0;
        }
    }

    private static InstallRecord MapRecord(SqliteDataReader reader)
    {
        var installedAtText = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
        var installedAt = DateTime.TryParse(
            installedAtText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedInstalledAt)
            ? parsedInstalledAt
            : DateTime.UtcNow;

        return new InstallRecord
        {
            Id = reader.GetInt32(0),
            AppId = reader.GetString(1),
            AppName = reader.GetString(2),
            Version = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Platform = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            InstallMethod = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            Success = reader.GetInt64(6) == 1,
            ErrorMessage = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            InstalledAt = installedAt,
            ElapsedMs = reader.IsDBNull(9) ? 0L : reader.GetInt64(9)
        };
    }

    private static void InitializeDatabase()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS InstallHistory (
              Id INTEGER PRIMARY KEY AUTOINCREMENT,
              AppId TEXT NOT NULL,
              AppName TEXT NOT NULL,
              Version TEXT,
              Platform TEXT,
              InstallMethod TEXT,
              Success INTEGER NOT NULL,
              ErrorMessage TEXT,
              InstalledAt TEXT NOT NULL,
              ElapsedMs INTEGER
            );
            """;
        command.ExecuteNonQuery();
    }
}
