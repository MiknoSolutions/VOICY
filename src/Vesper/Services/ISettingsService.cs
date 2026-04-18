using Vesper.Models;

namespace Vesper.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
