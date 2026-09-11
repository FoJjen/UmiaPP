using System.Collections.Generic;

namespace UmiaPP.Services;

public interface IThemeService
{
    IEnumerable<string> GetAvailableThemes();
    void ApplyTheme(string themeName);
    void SaveCustomTheme(string themeName, string xamlContent);
}
