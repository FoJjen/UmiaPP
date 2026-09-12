namespace UmiaPP.Models;

public enum DisciplineType
{
    Ventilation = 0,
    Piping = 1,      // Rör / VS
    Electrical = 2,  // El
    Control = 3      // Styr / SÖE
}

public enum ProjectStatus
{
    Kalkyl = 0,      // Kalkylskede
    Anbud = 1,       // Anbud lämnat
    Pagaende = 2,    // Pågående entreprenad
    Slutbesiktigad = 3, // Slutbesiktigad
    Avslutad = 4     // Avslutad
}

public enum ContractType
{
    Totalentreprenad = 0,   // TE
    Utforandeentreprenad = 1, // UE
    Underentreprenad = 2,
    LopandeRakning = 3
}
