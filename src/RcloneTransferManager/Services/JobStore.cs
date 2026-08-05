using System.Text.Json;
using System.Text.Json.Serialization;
using RcloneTransferManager.Models;

namespace RcloneTransferManager.Services;

public sealed class JobStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public async Task<List<TransferJob>> LoadAsync()
    {
        if (!File.Exists(AppPaths.JobsFile)) return new();
        await using var stream = File.OpenRead(AppPaths.JobsFile);
        return await JsonSerializer.DeserializeAsync<List<TransferJob>>(stream, Options) ?? new();
    }

    public async Task SaveAsync(IEnumerable<TransferJob> jobs)
    {
        var temp = AppPaths.JobsFile + ".tmp";
        await using (var stream = File.Create(temp)) await JsonSerializer.SerializeAsync(stream, jobs.ToList(), Options);
        File.Move(temp, AppPaths.JobsFile, true);
    }
}
