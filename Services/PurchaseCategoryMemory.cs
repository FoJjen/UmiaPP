using System.IO;
using System.Text.Json;

namespace UmiaPP.Services;

/// <summary>Per-user category choices shared by all projects on this computer.</summary>
internal sealed class PurchaseCategoryMemory
{
    private static readonly object FileLock = new();
    private readonly string _filePath;

    public PurchaseCategoryMemory(string? filePath = null)
    {
        _filePath = Path.GetFullPath(filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UmiaPP", "purchase-category-memory.json"));
    }

    public Dictionary<string, string> Load()
    {
        lock (FileLock) return LoadCore();
    }

    public void SaveChoices(IReadOnlyDictionary<string, string> choices)
    {
        if (choices.Count == 0) return;
        lock (FileLock)
        {
            // Merge with the latest file, rather than overwriting other projects'
            // choices from an old in-memory snapshot. Do not overwrite bad JSON.
            var remembered = LoadCore();
            foreach (var choice in choices)
            {
                ValidateChoice(choice.Key, choice.Value);
                remembered[choice.Key] = choice.Value;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(remembered,
                    new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, _filePath, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private Dictionary<string, string> LoadCore()
    {
        if (!File.Exists(_filePath)) return new Dictionary<string, string>(StringComparer.Ordinal);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_filePath))
            ?? throw new InvalidDataException("Filen med sparade kategorival innehåller inga giltiga data.");
        foreach (var choice in loaded) ValidateChoice(choice.Key, choice.Value);
        return new Dictionary<string, string>(loaded, StringComparer.Ordinal);
    }

    private static void ValidateChoice(string? subject, string? category)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(category))
            throw new InvalidDataException("Ett sparat kategorival saknar ämne eller kategori.");
    }
}
