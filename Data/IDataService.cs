using UmiaPP.Models;

namespace UmiaPP.Data;

/// <summary>
/// IDataService tillhandahåller abstraktion ovanpå databasen och förbereder
/// för framtida synkronisering mot extern molndatabas (t.ex. Azure SQL / REST API).
/// </summary>
public interface IDataService
{
    Task InitializeAsync();
    IProjectRepository Projects { get; }
    Task SyncWithCloudAsync();
    bool IsCloudConnected { get; }
}
