using TradeDashboard.Models;

namespace TradeDashboard.Services;

public interface IConfigurationService
{
    string GetProjectRootPath();
    string GetDatabasePath();
    AppConfiguration Load();
    void Save(AppConfiguration config);
}
