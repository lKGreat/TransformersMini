using System.Text.Json;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Infrastructure.Storage.Storage;

public sealed class FileIndexedRunRepository : IRunRepository
{
    private readonly string _indexRoot;
    private readonly string _indexPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileIndexedRunRepository() : this(Path.Combine(Environment.CurrentDirectory, "runs"))
    {
    }

    public FileIndexedRunRepository(string runsRoot)
    {
        _indexRoot = Path.GetFullPath(runsRoot);
        Directory.CreateDirectory(_indexRoot);
        _indexPath = Path.Combine(_indexRoot, ".index.json");
        if (!File.Exists(_indexPath))
        {
            File.WriteAllText(_indexPath, "{\"runs\":[]}");
        }
    }

    public async Task<string> CreateRunAsync(RunMetadata metadata, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var db = await LoadAsync(ct);
            db.Runs.RemoveAll(x => x.RunId == metadata.RunId);
            db.Runs.Add(ToDetail(metadata));
            await SaveAsync(db, ct);
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
            var db = await LoadAsync(ct);
            var run = db.Runs.FirstOrDefault(x => x.RunId == runId);
            if (run is null) return;
            run.Status = status.ToString();
            run.Message = message;
            run.EndedAt = endedAt;
            await SaveAsync(db, ct);
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
            var db = await LoadAsync(ct);
            var run = db.Runs.FirstOrDefault(x => x.RunId == runId);
            run?.Metrics.Add(metric);
            await SaveAsync(db, ct);
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
            var db = await LoadAsync(ct);
            var run = db.Runs.FirstOrDefault(x => x.RunId == runId);
            run?.Events.Add(evt);
            await SaveAsync(db, ct);
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
            var db = await LoadAsync(ct);
            return db.Runs
                .OrderByDescending(x => x.StartedAt)
                .Select(x => new RunSummaryDto
                {
                    RunId = x.RunId,
                    RunName = x.RunName,
                    Task = x.Task,
                    Backend = x.Backend,
                    Mode = x.Mode,
                    Device = x.Device,
                    Status = x.Status,
                    StartedAt = x.StartedAt,
                    EndedAt = x.EndedAt,
                    RunDirectory = x.RunDirectory,
                    ConfigPath = x.ConfigPath
                })
                .ToList();
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
            var db = await LoadAsync(ct);
            return db.Runs.FirstOrDefault(x => x.RunId == runId);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<RunIndexDocument> LoadAsync(CancellationToken ct)
    {
        await using var stream = File.OpenRead(_indexPath);
        return await JsonSerializer.DeserializeAsync<RunIndexDocument>(stream, JsonOptions, ct) ?? new RunIndexDocument();
    }

    private async Task SaveAsync(RunIndexDocument db, CancellationToken ct)
    {
        await using var stream = File.Create(_indexPath);
        await JsonSerializer.SerializeAsync(stream, db, JsonOptions, ct);
    }

    private static RunDetailDto ToDetail(RunMetadata metadata)
    {
        return new RunDetailDto
        {
            RunId = metadata.RunId,
            RunName = metadata.RunName,
            Task = metadata.Task.ToString(),
            Backend = metadata.Backend.ToString(),
            Mode = metadata.Mode.ToString(),
            Device = metadata.Device.ToString(),
            Status = metadata.Status.ToString(),
            StartedAt = metadata.StartedAt,
            EndedAt = metadata.EndedAt,
            RunDirectory = metadata.RunDirectory,
            ConfigPath = metadata.ConfigPath,
            Message = metadata.Message
        };
    }

    private sealed class RunIndexDocument
    {
        public List<RunDetailDto> Runs { get; set; } = new();
    }
}
