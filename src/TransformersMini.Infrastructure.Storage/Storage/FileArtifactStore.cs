using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;

namespace TransformersMini.Infrastructure.Storage.Storage;

public sealed class FileArtifactStore : IArtifactStore
{
    private readonly string _root;
    private readonly ILogger<FileArtifactStore>? _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _runDirs = new(StringComparer.OrdinalIgnoreCase);

    public FileArtifactStore(ILogger<FileArtifactStore> logger) : this(Path.Combine(Environment.CurrentDirectory, "runs"), logger)
    {
    }

    public FileArtifactStore(string root, ILogger<FileArtifactStore>? logger = null)
    {
        _root = Path.GetFullPath(root);
        _logger = logger;
        Directory.CreateDirectory(_root);
    }

    public Task<string> PrepareRunDirectoryAsync(RunMetadata metadata, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var dir = Path.Combine(_root, metadata.StartedAt.ToString("yyyy"), metadata.StartedAt.ToString("MM"), metadata.RunId);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "artifacts"));
        Directory.CreateDirectory(Path.Combine(dir, "reports"));

        lock (_gate)
        {
            _runDirs[metadata.RunId] = dir;
        }

        return Task.FromResult(dir);
    }

    public Task WriteTextAsync(string runId, string relativePath, string content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(runId, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Encoding.UTF8);
        TryRegisterArtifact(runId, relativePath, path);
        return Task.CompletedTask;
    }

    public Task AppendLineAsync(string runId, string relativePath, string line, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(runId, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        TryRegisterArtifact(runId, relativePath, path);
        return Task.CompletedTask;
    }

    private void TryRegisterArtifact(string runId, string relativePath, string fullPath)
    {
        try
        {
            var dbPath = Path.Combine(_root, "runs.db");
            if (!File.Exists(dbPath))
            {
                return;
            }

            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO run_artifacts(run_id, artifact_path, artifact_kind, size_bytes, updated_at)
                VALUES($run_id, $artifact_path, $artifact_kind, $size_bytes, $updated_at)
                ON CONFLICT(run_id, artifact_path) DO UPDATE SET
                    artifact_kind = excluded.artifact_kind,
                    size_bytes = excluded.size_bytes,
                    updated_at = excluded.updated_at;
                """;
            cmd.Parameters.AddWithValue("$run_id", runId);
            cmd.Parameters.AddWithValue("$artifact_path", relativePath.Replace('\\', '/'));
            cmd.Parameters.AddWithValue("$artifact_kind", InferArtifactKind(relativePath));
            cmd.Parameters.AddWithValue("$size_bytes", new FileInfo(fullPath).Length);
            cmd.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // 中文说明：产物登记失败不应影响主流程，但必须保留可观测性。
            _logger?.LogWarning(ex, "Artifact register failed. RunId={RunId}, RelativePath={RelativePath}", runId, relativePath);
        }
    }

    private static string InferArtifactKind(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').ToLowerInvariant();
        if (normalized.StartsWith("reports/")) return "report";
        if (normalized.StartsWith("artifacts/")) return "artifact";
        if (normalized.EndsWith(".jsonl")) return "stream-log";
        if (normalized.EndsWith(".log")) return "log";
        if (normalized.EndsWith("resolved-config.json")) return "config";
        return "file";
    }

    private string ResolvePath(string runId, string relativePath)
    {
        lock (_gate)
        {
            if (_runDirs.TryGetValue(runId, out var runDir))
            {
                return Path.Combine(runDir, relativePath);
            }
        }

        var fallback = Directory.GetDirectories(_root, runId, SearchOption.AllDirectories).FirstOrDefault();
        if (fallback is null)
        {
            throw new InvalidOperationException($"Run directory not found for {runId}.");
        }

        lock (_gate)
        {
            _runDirs[runId] = fallback;
        }

        return Path.Combine(fallback, relativePath);
    }
}

