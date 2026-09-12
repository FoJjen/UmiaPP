using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data;

namespace UmiaPP.Data;

/// <summary>
/// SQLite EnsureCreated skapar bara databasen första gången.
/// Synkroniseraren lägger till saknade tabeller/kolumner när modellen utökas.
/// </summary>
public static class SqliteSchemaSynchronizer
{
    public static async Task SynchronizeAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();
        await context.Database.OpenConnectionAsync();

        try
        {
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                if (entityType.IsOwned())
                    continue;

                var tableName = entityType.GetTableName();
                if (string.IsNullOrWhiteSpace(tableName))
                    continue;

                var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
                var existingColumns = GetExistingColumns(context, tableName);

                if (existingColumns.Count == 0)
                {
                    await CreateTableAsync(context, entityType, tableName, storeObject);
                    continue;
                }

                foreach (var property in entityType.GetProperties())
                {
                    var columnName = property.GetColumnName(storeObject);
                    if (string.IsNullOrWhiteSpace(columnName))
                        continue;

                    if (existingColumns.Contains(columnName))
                        continue;

                    var columnType = property.GetColumnType() ?? GetSqliteType(property.ClrType);
                    var defaultSql = GetSqliteDefault(property.ClrType);
                    var sql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnType} DEFAULT {defaultSql}";
                    await context.Database.ExecuteSqlRawAsync(sql);
                    existingColumns.Add(columnName);
                }
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static HashSet<string> GetExistingColumns(AppDbContext context, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = context.Database.GetDbConnection();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (!string.IsNullOrWhiteSpace(name))
                columns.Add(name);
        }

        return columns;
    }

    private static async Task CreateTableAsync(
        AppDbContext context,
        IEntityType entityType,
        string tableName,
        StoreObjectIdentifier storeObject)
    {
        var columnDefinitions = new List<string>();

        foreach (var property in entityType.GetProperties())
        {
            var columnName = property.GetColumnName(storeObject);
            if (string.IsNullOrWhiteSpace(columnName))
                continue;

            var columnType = property.GetColumnType() ?? GetSqliteType(property.ClrType);
            var definition = $"\"{columnName}\" {columnType}";

            if (property.IsPrimaryKey())
            {
                definition += " PRIMARY KEY";
                if (property.ValueGenerated == ValueGenerated.OnAdd && IsIntegerType(property.ClrType))
                    definition += " AUTOINCREMENT";
            }

            columnDefinitions.Add(definition);
        }

        if (columnDefinitions.Count == 0)
            return;

        var sql = $"CREATE TABLE IF NOT EXISTS \"{tableName}\" ({string.Join(", ", columnDefinitions)})";
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static string GetSqliteType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string) || type == typeof(DateTime) || type == typeof(Guid) || type == typeof(decimal))
            return "TEXT";
        if (type == typeof(double) || type == typeof(float))
            return "REAL";
        if (type == typeof(bool) || type == typeof(int) || type == typeof(long) || type == typeof(short) || type.IsEnum)
            return "INTEGER";

        return "TEXT";
    }

    private static string GetSqliteDefault(Type type)
    {
        if (Nullable.GetUnderlyingType(type) != null)
            return "NULL";

        if (type == typeof(string))
            return "''";
        if (type == typeof(DateTime))
            return "'0001-01-01 00:00:00'";
        if (type == typeof(bool) || type == typeof(int) || type == typeof(long) || type == typeof(short) ||
            type == typeof(double) || type == typeof(float) || type == typeof(decimal) || type.IsEnum)
            return "0";

        return "NULL";
    }

    private static bool IsIntegerType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(int) || type == typeof(long) || type == typeof(short);
    }
}
