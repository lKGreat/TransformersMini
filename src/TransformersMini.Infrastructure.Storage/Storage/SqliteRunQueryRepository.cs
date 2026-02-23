using System.Text;
using Microsoft.Data.Sqlite;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;

namespace TransformersMini.Infrastructure.Storage.Storage;

/// <summary>
/// 基于 SQLite 的运行查询仓库实现，支持多维度过滤、分页、排序。
/// 替代 WinForms 列表页逐条 GetAsync 扫描。
/// </summary>
public sealed class SqliteRunQueryRepository : IRunQueryRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public SqliteRunQueryRepository(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<RunQueryResult> QueryAsync(RunQueryFilter filter, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);

            var (whereClause, parameters) = BuildWhereClause(filter);
            var orderClause = BuildOrderClause(filter.OrderBy);

            // 查询总数
            var countSql = $"SELECT COUNT(DISTINCT r.run_id) FROM runs r {(NeedsTagJoin(filter) ? "LEFT JOIN run_tags rt ON r.run_id = rt.run_id" : string.Empty)} {whereClause}";
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = countSql;
            BindParameters(countCmd, parameters);
            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

            // 分页查询
            var limitClause = filter.Limit > 0 ? $"LIMIT {filter.Limit} OFFSET {filter.Offset}" : $"OFFSET {filter.Offset}";
            var dataSql = $"""
                SELECT DISTINCT r.run_id, r.run_name, r.task, r.backend, r.mode, r.device, r.status,
                       r.started_at, r.ended_at, r.run_directory, r.config_path
                FROM runs r
                {(NeedsTagJoin(filter) ? "LEFT JOIN run_tags rt ON r.run_id = rt.run_id" : string.Empty)}
                {whereClause}
                {orderClause}
                {limitClause}
                """;

            await using var dataCmd = conn.CreateCommand();
            dataCmd.CommandText = dataSql;
            BindParameters(dataCmd, parameters);

            await using var reader = await dataCmd.ExecuteReaderAsync(ct);
            var items = new List<RunSummaryDto>();
            while (await reader.ReadAsync(ct))
            {
                items.Add(new RunSummaryDto
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

            return new RunQueryResult
            {
                Items = items,
                TotalCount = totalCount,
                Offset = filter.Offset,
                Limit = filter.Limit
            };
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static bool NeedsTagJoin(RunQueryFilter filter) =>
        !string.IsNullOrWhiteSpace(filter.TagKey) || !string.IsNullOrWhiteSpace(filter.TagValue);

    private static (string Clause, List<(string Name, object Value)> Parameters) BuildWhereClause(RunQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new List<(string, object)>();

        if (!string.IsNullOrWhiteSpace(filter.Task))
        {
            conditions.Add("r.task = $task");
            parameters.Add(("$task", filter.Task));
        }

        if (!string.IsNullOrWhiteSpace(filter.Mode))
        {
            conditions.Add("r.mode = $mode");
            parameters.Add(("$mode", filter.Mode));
        }

        if (!string.IsNullOrWhiteSpace(filter.Backend))
        {
            conditions.Add("r.backend = $backend");
            parameters.Add(("$backend", filter.Backend));
        }

        if (!string.IsNullOrWhiteSpace(filter.Device))
        {
            conditions.Add("r.device = $device");
            parameters.Add(("$device", filter.Device));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("r.status = $status");
            parameters.Add(("$status", filter.Status));
        }

        if (!string.IsNullOrWhiteSpace(filter.TagKey))
        {
            conditions.Add("rt.tag_key LIKE $tag_key");
            parameters.Add(("$tag_key", $"%{filter.TagKey}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.TagValue))
        {
            conditions.Add("rt.tag_value LIKE $tag_value");
            parameters.Add(("$tag_value", $"%{filter.TagValue}%"));
        }

        if (filter.StartedAfter.HasValue)
        {
            conditions.Add("r.started_at >= $started_after");
            parameters.Add(("$started_after", filter.StartedAfter.Value.ToString("O")));
        }

        if (filter.StartedBefore.HasValue)
        {
            conditions.Add("r.started_at <= $started_before");
            parameters.Add(("$started_before", filter.StartedBefore.Value.ToString("O")));
        }

        var clause = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;

        return (clause, parameters);
    }

    private static string BuildOrderClause(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return "ORDER BY r.started_at DESC";
        }

        return orderBy.Trim().ToLowerInvariant() switch
        {
            "started_at asc" => "ORDER BY r.started_at ASC",
            "started_at desc" => "ORDER BY r.started_at DESC",
            "task" => "ORDER BY r.task ASC, r.started_at DESC",
            "status" => "ORDER BY r.status ASC, r.started_at DESC",
            _ => "ORDER BY r.started_at DESC"
        };
    }

    private static void BindParameters(SqliteCommand cmd, List<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
    }

    private static DateTimeOffset ParseTime(string value) =>
        DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
}
