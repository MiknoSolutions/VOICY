using System.IO;
using System.Net.Http;
using Voicy.Models;

namespace Voicy.Services;

public sealed class ModelDownloadService : IModelDownloadService
{
    private readonly HttpClient _httpClient = new();

    public bool IsModelDownloaded(ModelDefinition model) => model.IsDownloaded();

    public async Task DownloadModelAsync(ModelDefinition model, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var dir = model.ModelDirectory;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Filter to files not yet downloaded
        var filesToDownload = model.Files
            .Where(f => !File.Exists(model.GetFilePath(f.RelativePath)))
            .ToArray();

        if (filesToDownload.Length == 0)
        {
            progress?.Report(100);
            return;
        }

        for (int i = 0; i < filesToDownload.Length; i++)
        {
            var file = filesToDownload[i];
            var filePath = model.GetFilePath(file.RelativePath);

            using var response = await _httpClient.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            long receivedBytes = 0;

            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                receivedBytes += bytesRead;

                double fileProgress = totalBytes > 0 ? (double)receivedBytes / totalBytes : 0.5;
                double overallProgress = ((i + fileProgress) / filesToDownload.Length) * 100;
                progress?.Report(overallProgress);
            }
        }

        progress?.Report(100);
    }
}
