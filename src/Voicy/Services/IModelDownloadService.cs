namespace Voicy.Services;

public interface IModelDownloadService
{
    Task DownloadModelAsync(string modelSize, IProgress<double>? progress = null, CancellationToken ct = default);
    bool IsModelDownloaded(string modelSize);
    List<string> AvailableModelSizes { get; }
}
