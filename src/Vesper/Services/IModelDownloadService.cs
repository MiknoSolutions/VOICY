using Vesper.Models;

namespace Vesper.Services;

public interface IModelDownloadService
{
    Task DownloadModelAsync(ModelDefinition model, IProgress<double>? progress = null, CancellationToken ct = default);
    bool IsModelDownloaded(ModelDefinition model);
}
