using System.Text;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;

namespace TransformersMini.Infrastructure.Storage.Storage;

public sealed class FileArtifactStore : IArtifactStore
{
    private readonly string _root;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _runDirs = new(StringComparer.OrdinalIgnoreCase);

    public FileArtifactStore() : this(Path.Combine(Environment.CurrentDirectory, "runs"))
    {
    }

    public FileArtifactStore(string root)
    {
        _root = Path.GetFullPath(root);
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
        return Task.CompletedTask;
    }

    public Task AppendLineAsync(string runId, string relativePath, string line, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(runId, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        return Task.CompletedTask;
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
