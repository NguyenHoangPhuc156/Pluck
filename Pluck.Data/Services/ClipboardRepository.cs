using Microsoft.Data.Sqlite;
using Pluck.Data.Models;

namespace Pluck.Data.Services;

public sealed class ClipboardRepository : IDisposable
{
    private const int SchemaVersion = 2;

    private readonly SqliteConnection _connection;
    private readonly int _historyLimit;

    private static readonly (string Name, string Definition)[] CanonicalColumns =
    [
        ("type", "INTEGER NOT NULL DEFAULT 0"),
        ("preview", "TEXT NOT NULL DEFAULT ''"),
        ("text_content", "TEXT"),
        ("image_thumbnail", "BLOB"),
        ("image_full", "BLOB"),
        ("file_paths_json", "TEXT"),
        ("source_app_name", "TEXT NOT NULL DEFAULT ''"),
        ("source_app_icon", "BLOB"),
        ("source_window_handle", "INTEGER NOT NULL DEFAULT 0"),
        ("copied_at", "TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))"),
        ("is_pinned", "INTEGER NOT NULL DEFAULT 0"),
    ];

    private static readonly string[] LegacyTimestampColumns = ["captured_at", "created_at"];

    public ClipboardRepository(string? databasePath = null, int historyLimit = 200)
    {
        _historyLimit = historyLimit;
        var folder = databasePath is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pluck")
            : Path.GetDirectoryName(databasePath)!;
        Directory.CreateDirectory(folder);

        var dbFile = databasePath ?? Path.Combine(folder, "history.db");
        _connection = new SqliteConnection($"Data Source={dbFile}");
        _connection.Open();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS schema_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS clipboard_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type INTEGER NOT NULL DEFAULT 0,
                preview TEXT NOT NULL DEFAULT '',
                text_content TEXT,
                image_thumbnail BLOB,
                image_full BLOB,
                file_paths_json TEXT,
                source_app_name TEXT NOT NULL DEFAULT '',
                source_app_icon BLOB,
                source_window_handle INTEGER NOT NULL DEFAULT 0,
                copied_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                is_pinned INTEGER NOT NULL DEFAULT 0
            );
            """);

        AddMissingCanonicalColumns();

        if (HasLegacyTimestampColumns() || GetStoredSchemaVersion() < SchemaVersion)
            RebuildToCanonicalTable();

        Execute("""
            CREATE INDEX IF NOT EXISTS idx_clipboard_copied_at ON clipboard_items(copied_at DESC);
            """);

        SetStoredSchemaVersion(SchemaVersion);
    }

    private void AddMissingCanonicalColumns()
    {
        var existing = GetColumnNames();
        foreach (var (name, definition) in CanonicalColumns)
        {
            if (existing.Contains(name))
                continue;

            Execute($"ALTER TABLE clipboard_items ADD COLUMN {name} {definition};");
            existing.Add(name);
        }

        BackfillTimestampColumns(existing);
    }

    private void BackfillTimestampColumns(HashSet<string> columns)
    {
        if (!columns.Contains("copied_at"))
            return;

        foreach (var legacy in LegacyTimestampColumns)
        {
            if (!columns.Contains(legacy))
                continue;

            Execute($"""
                UPDATE clipboard_items
                SET copied_at = {legacy}
                WHERE (copied_at IS NULL OR copied_at = '')
                  AND {legacy} IS NOT NULL AND {legacy} != '';
                """);

            Execute($"""
                UPDATE clipboard_items
                SET {legacy} = copied_at
                WHERE ({legacy} IS NULL OR {legacy} = '')
                  AND copied_at IS NOT NULL AND copied_at != '';
                """);
        }
    }

    private bool HasLegacyTimestampColumns()
    {
        var columns = GetColumnNames();
        return LegacyTimestampColumns.Any(columns.Contains);
    }

    private void RebuildToCanonicalTable()
    {
        var columns = GetColumnNames();
        if (!columns.Contains("id"))
            return;

        var timestampExpr = BuildTimestampCoalesceExpr(columns);
        var selectParts = new List<string> { "id" };

        foreach (var (name, _) in CanonicalColumns)
        {
            if (name == "copied_at")
            {
                selectParts.Add($"{timestampExpr} AS copied_at");
                continue;
            }

            selectParts.Add(columns.Contains(name)
                ? name
                : $"NULL AS {name}");
        }

        Execute("DROP TABLE IF EXISTS clipboard_items__new;");
        Execute("""
            CREATE TABLE clipboard_items__new (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type INTEGER NOT NULL DEFAULT 0,
                preview TEXT NOT NULL DEFAULT '',
                text_content TEXT,
                image_thumbnail BLOB,
                image_full BLOB,
                file_paths_json TEXT,
                source_app_name TEXT NOT NULL DEFAULT '',
                source_app_icon BLOB,
                source_window_handle INTEGER NOT NULL DEFAULT 0,
                copied_at TEXT NOT NULL,
                is_pinned INTEGER NOT NULL DEFAULT 0
            );
            """);

        Execute($"""
            INSERT INTO clipboard_items__new (
                id, type, preview, text_content, image_thumbnail, image_full, file_paths_json,
                source_app_name, source_app_icon, source_window_handle, copied_at, is_pinned
            )
            SELECT {string.Join(", ", selectParts)}
            FROM clipboard_items;
            """);

        Execute("DROP TABLE clipboard_items;");
        Execute("ALTER TABLE clipboard_items__new RENAME TO clipboard_items;");
    }

    private static string BuildTimestampCoalesceExpr(HashSet<string> columns)
    {
        var parts = new List<string>();
        if (columns.Contains("copied_at"))
            parts.Add("NULLIF(copied_at, '')");
        foreach (var legacy in LegacyTimestampColumns)
        {
            if (columns.Contains(legacy))
                parts.Add($"NULLIF({legacy}, '')");
        }
        parts.Add("strftime('%Y-%m-%dT%H:%M:%fZ','now')");
        return $"COALESCE({string.Join(", ", parts)})";
    }

    private int GetStoredSchemaVersion()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM schema_meta WHERE key = 'schema_version';";
        var result = cmd.ExecuteScalar();
        return result is string s && int.TryParse(s, out var v) ? v : 0;
    }

    private void SetStoredSchemaVersion(int version)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO schema_meta (key, value) VALUES ('schema_version', @v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("@v", version.ToString());
        cmd.ExecuteNonQuery();
    }

    private HashSet<string> GetColumnNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(clipboard_items);";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            set.Add(reader.GetString(1));
        return set;
    }

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public long Insert(ClipboardItem item)
    {
        var at = item.CopiedAt.UtcDateTime.ToString("O");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO clipboard_items (
                type, preview, text_content, image_thumbnail, image_full, file_paths_json,
                source_app_name, source_app_icon, source_window_handle, copied_at, is_pinned
            ) VALUES (
                @type, @preview, @text, @thumb, @full, @files,
                @app, @icon, @hwnd, @at, @pinned
            );
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@type", (int)item.Type);
        cmd.Parameters.AddWithValue("@preview", item.Preview);
        cmd.Parameters.AddWithValue("@text", (object?)item.TextContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@thumb", (object?)item.ImageThumbnailPng ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@full", (object?)item.ImageFullPng ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@files", (object?)item.FilePathsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@app", item.SourceAppName);
        cmd.Parameters.AddWithValue("@icon", (object?)item.SourceAppIconPng ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@hwnd", item.SourceWindowHandle.ToInt64());
        cmd.Parameters.AddWithValue("@at", at);
        cmd.Parameters.AddWithValue("@pinned", item.IsPinned ? 1 : 0);

        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        item.Id = id;
        TrimHistory();
        return id;
    }

    public IReadOnlyList<ClipboardItem> GetRecent(int count = 100) =>
        Search(new HistorySearchCriteria { Limit = count });

    public IReadOnlyList<string> GetDistinctSourceApps(int limit = 200)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT source_app_name
            FROM clipboard_items
            WHERE source_app_name IS NOT NULL AND source_app_name != ''
            ORDER BY source_app_name COLLATE NOCASE
            LIMIT @limit;
            """;
        cmd.Parameters.AddWithValue("@limit", limit);

        var list = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(reader.GetString(0));
        return list;
    }

    public IReadOnlyList<ClipboardItem> Search(HistorySearchCriteria criteria)
    {
        criteria ??= new HistorySearchCriteria();

        var sql = new System.Text.StringBuilder("""
            SELECT id, type, preview, text_content, image_thumbnail, image_full, file_paths_json,
                   source_app_name, source_app_icon, source_window_handle, copied_at, is_pinned
            FROM clipboard_items
            WHERE 1=1
            """);

        if (criteria.Type.HasValue)
            sql.Append(" AND type = @type");

        if (!string.IsNullOrWhiteSpace(criteria.SourceAppName))
            sql.Append(" AND source_app_name = @app");

        if (criteria.TimeRange != HistoryTimeRange.All)
            sql.Append(" AND copied_at >= @since");

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            sql.Append(" AND (preview LIKE @q ESCAPE '\\' OR text_content LIKE @q ESCAPE '\\' OR file_paths_json LIKE @q ESCAPE '\\')");

        sql.Append(" ORDER BY copied_at DESC LIMIT @limit;");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql.ToString();
        cmd.Parameters.AddWithValue("@limit", Math.Max(1, criteria.Limit));

        if (criteria.Type.HasValue)
            cmd.Parameters.AddWithValue("@type", (int)criteria.Type.Value);

        if (!string.IsNullOrWhiteSpace(criteria.SourceAppName))
            cmd.Parameters.AddWithValue("@app", criteria.SourceAppName);

        if (criteria.TimeRange != HistoryTimeRange.All)
            cmd.Parameters.AddWithValue("@since", ResolveSinceUtc(criteria.TimeRange).UtcDateTime.ToString("O"));

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            cmd.Parameters.AddWithValue("@q", $"%{EscapeLike(criteria.SearchText.Trim())}%");

        var list = new List<ClipboardItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(ReadRow(reader));
        return list;
    }

    private static DateTimeOffset ResolveSinceUtc(HistoryTimeRange range)
    {
        var now = DateTimeOffset.Now;
        return range switch
        {
            HistoryTimeRange.Last24Hours => now.AddHours(-24).ToUniversalTime(),
            HistoryTimeRange.Today => new DateTimeOffset(now.Date, now.Offset).ToUniversalTime(),
            HistoryTimeRange.Last7Days => now.AddDays(-7).ToUniversalTime(),
            HistoryTimeRange.Last30Days => now.AddDays(-30).ToUniversalTime(),
            _ => DateTimeOffset.MinValue
        };
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public void Delete(long id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM clipboard_items WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetPinned(long id, bool pinned)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE clipboard_items SET is_pinned = @p WHERE id = @id;";
        cmd.Parameters.AddWithValue("@p", pinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void ClearAll()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM clipboard_items;";
        cmd.ExecuteNonQuery();
    }

    private void TrimHistory()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM clipboard_items
            WHERE is_pinned = 0
              AND id NOT IN (
                SELECT id FROM clipboard_items
                ORDER BY copied_at DESC
                LIMIT @limit
              );
            """;
        cmd.Parameters.AddWithValue("@limit", _historyLimit);
        cmd.ExecuteNonQuery();
    }

    private static ClipboardItem ReadRow(SqliteDataReader reader)
    {
        return new ClipboardItem
        {
            Id = reader.GetInt64(0),
            Type = (ClipboardItemType)reader.GetInt32(1),
            Preview = ReadStringOrEmpty(reader, 2),
            TextContent = reader.IsDBNull(3) ? null : reader.GetString(3),
            ImageThumbnailPng = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4),
            ImageFullPng = reader.IsDBNull(5) ? null : (byte[])reader.GetValue(5),
            FilePathsJson = reader.IsDBNull(6) ? null : reader.GetString(6),
            SourceAppName = ReadStringOrEmpty(reader, 7),
            SourceAppIconPng = reader.IsDBNull(8) ? null : (byte[])reader.GetValue(8),
            SourceWindowHandle = new IntPtr(reader.GetInt64(9)),
            CopiedAt = ReadDateTime(reader, 10),
            IsPinned = reader.GetInt32(11) != 0
        };
    }

    private static string ReadStringOrEmpty(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);

    private static DateTimeOffset ReadDateTime(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return DateTimeOffset.UtcNow;
        var text = reader.GetString(ordinal);
        return DateTimeOffset.TryParse(text, out var dt) ? dt : DateTimeOffset.UtcNow;
    }

    public void Dispose() => _connection.Dispose();
}
