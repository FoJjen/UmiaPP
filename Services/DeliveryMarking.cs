using UmiaPP.Models;

namespace UmiaPP.Services;

public static class DeliveryMarking
{
    public static string Code(DisciplineType discipline) => discipline switch
    {
        DisciplineType.Piping => "70",
        DisciplineType.Electrical => "30",
        DisciplineType.Control => "90",
        _ => "50"
    };

    public static string ShortName(DisciplineType discipline) => discipline switch
    {
        DisciplineType.Piping => "VS",
        DisciplineType.Electrical => "EL",
        DisciplineType.Control => "SÖE",
        _ => "VE"
    };

    public static string Format(string? projectNumber, DisciplineType discipline)
    {
        var number = string.IsNullOrWhiteSpace(projectNumber) ? "-" : projectNumber.Trim();
        return $"{number}-{Code(discipline)}";
    }
}
