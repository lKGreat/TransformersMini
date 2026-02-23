using Microsoft.Data.Sqlite;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Infrastructure.Storage.Storage;

public sealed class SqliteRunRepository : IRunRepository
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public SqliteRunRepository() : this(Path.Combine(Environment.CurrentDirectory, "runs", "runs.db"))
    {
    }

    public SqliteRunRepository(string dbPath)
    {
        _dbPath = Path.GetFullPath(dbPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        Initialize();
    }

    public async Task<string> CreateRunAsync(RunMetadata metadata, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO runs(run_id, run_name, config_path, run_directory, mode, task, backend, device, status, started_at, ended_at, message)
                VALUES($run_id, $run_name, $config_path, $run_directory, $mode, $task, $backend, $device, $status, $started_at, $ended_at, $message)
                ON CONFLICT(run_id) DO UPDATE SET
                    run_name=excluded.run_name,
                    config_path=excluded.config_path,
                    run_directory=excluded.run_directory,
                    mode=excluded.mode,
                    task=excluded.task,
                    backend=excluded.backend,
                    device=excluded.device,
                    status=excluded.status,
                    started_at=excluded.started_at,
                    ended_at=excluded.ended_at,
                    message=excluded.message;
                """;
            BindRun(cmd, metadata);
            await cmd.ExecuteNonQueryAsync(ct);
            await UpsertTagAsync(conn, metadata.RunId, "system.run_name", metadata.RunName, ct);
            await UpsertTagAsync(conn, metadata.RunId, "system.mode", metadata.Mode.ToString(), ct);
            await UpsertTagAsync(conn, metadata.RunId, "system.task", metadata.Task.ToString(), ct);
            await UpsertTagAsync(conn, metadata.RunId, "system.backend", metadata.Backend.ToString(), ct);
            await UpsertTagAsync(conn, metadata.RunId, "system.device", metadata.Device.ToString(), ct);
            return metadata.RunId;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpdateStatusAsync(string runId, RunStatus status, string? message, DateTimeOffset? endedAt, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                UPDATE runs
                SET status = $status, message = $message, ended_at = $ended_at
                WHERE run_id = $run_id;
                """;
            cmd.Parameters.AddWithValue("$status", status.ToString());
            cmd.Parameters.AddWithValue("$message", (object?)message ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ended_at", endedAt?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$run_id", runId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpsertTagAsync(string runId, string key, string value, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await UpsertTagAsync(conn, runId, key, value, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task AppendMetricAsync(string runId, MetricPoint metric, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO run_metrics(run_id, metric_name, step, metric_value, timestamp)
                VALUES($run_id, $metric_name, $step, $metric_value, $timestamp);
                """;
            cmd.Parameters.AddWithValue("$run_id", runId);
            cmd.Parameters.AddWithValue("$metric_name", metric.Name);
            cmd.Parameters.AddWithValue("$step", metric.Step);
            cmd.Parameters.AddWithValue("$metric_value", metric.Value);
            cmd.Parameters.AddWithValue("$timestamp", metric.Timestamp.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct);
            await UpsertLatestMetricAsync(conn, runId, metric, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task AppendEventAsync(string runId, RunEvent evt, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO run_events(run_id, level, event_type, message, timestamp)
                VALUES($run_id, $level, $event_type, $message, $timestamp);
                """;
            cmd.Parameters.AddWithValue("$run_id", runId);
            cmd.Parameters.AddWithValue("$level", evt.Level);
            cmd.Parameters.AddWithValue("$event_type", evt.EventType);
            cmd.Parameters.AddWithValue("$message", evt.Message);
            cmd.Parameters.AddWithValue("$timestamp", evt.Timestamp.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<RunSummaryDto>> ListAsync(CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                SELECT run_id, run_name, task, backend, mode, device, status, started_at, ended_at, run_directory, config_path
                FROM runs
                ORDER BY started_at DESC;
                """;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<RunSummaryDto>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new RunSummaryDto
                {
                    RunId = reader.GetString(0),
                    RunName = reader.GetString(1),
                    Task = reader.GetString(2),
                    Backend = reader.GetString(3),
                    Mode = reader.GetString(4),
                    Device = reader.GetString(5),
                    Status = reader.GetString(6),
                    StartedAt = ParseTime(reader.GetString(7)),
                    EndedAt = reader.IsDBNull(8) ? null : ParseTime(reader.GetString(8)),
                    RunDirectory = reader.GetString(9),
                    ConfigPath = reader.GetString(10)
                });
            }

            return list;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<RunDetailDto?> GetAsync(string runId, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await using var conn = await OpenAsync(ct);

            RunDetailDto? detail;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    """
                    SELECT run_id, run_name, task, backend, mode, device, status, started_at, ended_at, run_directory, config_path, message
                    FROM runs
                    WHERE run_id = $run_id;
                    """;
                cmd.Parameters.AddWithValue("$run_id", runId);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                detail = new RunDetailDto
                {
                    RunId = reader.GetString(0),
                    RunName = reader.GetString(1),
                    Task = reader.GetString(2),
                    Backend = reader.GetString(3),
                    Mode = reader.GetString(4),
                    Device = reader.GetString(5),
                    Status = reader.GetString(6),
                    StartedAt = ParseTime(reader.GetString(7)),
                    EndedAt = reader.IsDBNull(8) ? null : ParseTime(reader.GetString(8)),
                    RunDirectory = reader.GetString(9),
                    ConfigPath = reader.GetString(10),
                    Message = reader.IsDBNull(11) ? null : reader.GetString(11)
                };
            }

            detail.Metrics = await LoadMetricsAsync(conn, runId, ct);
            detail.Events = await LoadEventsAsync(conn, runId, ct);
            detail.Tags = await LoadTagsAsync(conn, runId, ct);
            detail.Artifacts = await LoadArtifactsAsync(conn, runId, ct);
            detail.LatestMetrics = await LoadLatestMetricsAsync(conn, runId, ct);
            return detail;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private void Initialize()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        pragma.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS runs (
                run_id TEXT PRIMARY KEY,
                run_name TEXT NOT NULL,
                config_path TEXT NOT NULL,
                run_directory TEXT NOT NULL,
                mode TEXT NOT NULL,
                task TEXT NOT NULL,
                backend TEXT NOT NULL,
                device TEXT NOT NULL,
                status TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT NULL,
                message TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS run_metrics (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id TEXT NOT NULL,
                metric_name TEXT NOT NULL,
                step INTEGER NOT NULL,
                metric_value REAL NOT NULL,
                timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS run_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id TEXT NOT NULL,
                level TEXT NOT NULL,
                event_type TEXT NOT NULL,
                message TEXT NOT NULL,
                timestamp TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS run_tags (
                run_id TEXT NOT NULL,
                tag_key TEXT NOT NULL,
                tag_value TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                PRIMARY KEY (run_id, tag_key)
            );

            CREATE TABLE IF NOT EXISTS run_artifacts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id TEXT NOT NULL,
                artifact_path TEXT NOT NULL,
                artifact_kind TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS run_metrics_latest (
                run_id TEXT NOT NULL,
                metric_name TEXT NOT NULL,
                step INTEGER NOT NULL,
                metric_value REAL NOT NULL,
                timestamp TEXT NOT NULL,
                PRIMARY KEY (run_id, metric_name)
            );

            CREATE INDEX IF NOT EXISTS idx_run_metrics_run_id ON run_metrics(run_id);
            CREATE INDEX IF NOT EXISTS idx_run_events_run_id ON run_events(run_id);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_run_artifacts_unique ON run_artifacts(run_id, artifact_path);
            CREATE INDEX IF NOT EXISTS idx_run_artifacts_run_id ON run_artifacts(run_id);
            CREATE INDEX IF NOT EXISTS idx_run_tags_run_id ON run_tags(run_id);
            """;
        cmd.ExecuteNonQuery();
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    private static void BindRun(SqliteCommand cmd, RunMetadata metadata)
    {
        cmd.Parameters.AddWithValue("$run_id", metadata.RunId);
        cmd.Parameters.AddWithValue("$run_name", metadata.RunName);
        cmd.Parameters.AddWithValue("$config_path", metadata.ConfigPath);
        cmd.Parameters.AddWithValue("$run_directory", metadata.RunDirectory);
        cmd.Parameters.AddWithValue("$mode", metadata.Mode.ToString());
        cmd.Parameters.AddWithValue("$task", metadata.Task.ToString());
        cmd.Parameters.AddWithValue("$backend", metadata.Backend.ToString());
        cmd.Parameters.AddWithValue("$device", metadata.Device.ToString());
        cmd.Parameters.AddWithValue("$status", metadata.Status.ToString());
        cmd.Parameters.AddWithValue("$started_at", metadata.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$ended_at", metadata.EndedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$message", (object?)metadata.Message ?? DBNull.Value);
    }

    private static DateTimeOffset ParseTime(string value) => DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static async Task UpsertTagAsync(SqliteConnection conn, string runId, string key, string value, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO run_tags(run_id, tag_key, tag_value, updated_at)
            VALUES($run_id, $tag_key, $tag_value, $updated_at)
            ON CONFLICT(run_id, tag_key) DO UPDATE SET
                tag_value = excluded.tag_value,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("$run_id", runId);
        cmd.Parameters.AddWithValue("$tag_key", key);
        cmd.Parameters.AddWithValue("$tag_value", value);
        cmd.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertLatestMetricAsync(SqliteConnection conn, string runId, MetricPoint metric, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO run_metrics_latest(run_id, metric_name, step, metric_value, timestamp)
            VALUES($run_id, $metric_name, $step, $metric_value, $timestamp)
            ON CONFLICT(run_id, metric_name) DO UPDATE SET
                step = excluded.step,
                metric_value = excluded.metric_value,
                timestamp = excluded.timestamp;
            """;
        cmd.Parameters.AddWithValue("$run_id", runId);
        cmd.Parameters.AddWithValue("$metric_name", metric.Name);
        cmd.Parameters.AddWithValue("$step", metric.Step);
        cmd.Parameters.AddWithValue("$metric_value", metric.Value);
        cmd.Parameters.AddWithValue("$timestamp", metric.Timestamp.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<MetricPoint>> LoadMetricsAsync(SqliteConnection conn, string runId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT metric_name, step, metric_value, timestamp
            FROM run_metrics
            WHERE run_id = $run_id
            ORDER BY id ASC;
            """;
        cmd.Parameters.AddWithValue("$run_id", runId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<MetricPoint>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new MetricPoint(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetDouble(2),
                ParseTime(reader.GetString(3))));
        }

        return list;
    }

    private static async Task<List<RunEvent>> LoadEventsAsync(SqliteConnection conn, string runId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT level, event_type, message, timestamp
            FROM run_events
            WHERE run_id = $run_id
            ORDER BY id ASC;
            """;
        cmd.Parameters.AddWithValue("$run_id", runId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RunEvent>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RunEvent(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ParseTime(reader.GetString(3))));
        }

        return list;
    }

    private static async Task<List<RunTagDto>> LoadTagsAsync(SqliteConnection conn, string runId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT tag_key, tag_value, updated_at
            FROM run_tags
            WHERE run_id = $run_id
            ORDER BY tag_key ASC;
            """;
        cmd.Parameters.AddWithValue("$run_id", runId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RunTagDto>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RunTagDto(
                reader.GetString(0),
                reader.GetString(1),
                ParseTime(reader.GetString(2))));
        }

        return list;
    }

    private static async Task<List<RunArtifactDto>> LoadArtifactsAsync(SqliteConnection conn, string runId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT artifact_path, artifact_kind, size_bytes, updated_at
            FROM run_artifacts
            WHERE run_id = $run_id
            ORDER BY updated_at DESC, artifact_path ASC;
            """;
        cmd.Parameters.AddWithValue("$run_id", runId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<RunArtifactDto>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RunArtifactDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                ParseTime(reader.GetString(3))));
        }

        return list;
    }

    private static async Task<List<LatestMetricDto>> LoadLatestMetricsAsync(SqliteConnection conn, string runId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT metric_name, step, metric_value, timestamp
            FROM run_metrics_latest
            WHERE run_id = $run_id
            ORDER BY metric_name ASC;
            """;
        cmd.Parameters.AddWithValue("$run_id", runId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LatestMetricDto>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new LatestMetricDto(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetDouble(2),
                ParseTime(reader.GetString(3))));
        }

        return list;
    }
}
