using Voicy.Models;

namespace Voicy.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
