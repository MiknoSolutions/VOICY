using System.IO;
using System.Net.Http;

namespace Voicy.Services;

public sealed class ModelDownloadService : IModelDownloadService
{
    private readonly HttpClient _httpClient = new();

    private static readonly Dictionary<string, string> ModelUrls = new()
    {
        ["tiny"] = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
        ["base"] = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
        ["small"] = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
        ["medium"] = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin",
        ["large"] = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin",
    };

    public List<string> AvailableModelSizes => [.. ModelUrls.Keys];

    public bool IsModelDownloaded(string modelSize)
    {
        var path = GetModelPath(modelSize);
        return File.Exists(path);
    }

    public async Task DownloadModelAsync(string modelSize, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (!ModelUrls.TryGetValue(modelSize, out var url))
            throw new ArgumentException($"Unknown model size: {modelSize}");

        var path = GetModelPath(modelSize);
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        long receivedBytes = 0;

        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            receivedBytes += bytesRead;

            if (totalBytes > 0)
                progress?.Report((double)receivedBytes / totalBytes * 100);
        }

        progress?.Report(100);
    }

    private static string GetModelPath(string modelSize)
    {
        return Path.Combine(AppContext.BaseDirectory, "models", $"ggml-{modelSize}.bin");
    }
}
