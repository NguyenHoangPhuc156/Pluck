using Microsoft.Data.Sqlite;
using Pluck.Data.Models;

namespace Pluck.Data.Services;

/// <summary>
/// SQLite-backed persistence for clipboard history, including schema migration and search.
/// </summary>
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

    /// <summary>
    /// Opens (or creates) the clipboard history database and ensures the schema is up to date.
    /// </summary>
    /// <param name="databasePath">
    /// Optional full path to the SQLite database file. When <see langword="null"/>, uses
    /// <see cref="Environment.SpecialFolder.LocalApplicationData"/>/<c>Pluck/history.db</c>.
    /// </param>
    /// <param name="historyLimit">
    /// Maximum number of unpinned entries to retain after inserts; older unpinned rows are trimmed.
    /// </param>
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

    /// <summary>
    /// Creates required tables, migrates legacy schemas, and records the current schema version.
    /// </summary>
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

    /// <summary>
    /// Adds any canonical columns missing from an older <c>clipboard_items</c> table via <c>ALTER TABLE</c>.
    /// </summary>
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

    /// <summary>
    /// Copies timestamp values between <c>copied_at</c> and legacy column names when one side is empty.
    /// </summary>
    /// <param name="columns">The set of column names currently present on <c>clipboard_items</c>.</param>
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

    /// <summary>
    /// Determines whether the table still contains pre-canonical timestamp column names.
    /// </summary>
    /// <returns><see langword="true"/> if any legacy timestamp column exists; otherwise <see langword="false"/>.</returns>
    private bool HasLegacyTimestampColumns()
    {
        var columns = GetColumnNames();
        return LegacyTimestampColumns.Any(columns.Contains);
    }

    /// <summary>
    /// Rebuilds <c>clipboard_items</c> into the canonical column layout, coalescing legacy timestamps.
    /// </summary>
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

    /// <summary>
    /// Builds a SQL <c>COALESCE</c> expression that picks the first non-empty timestamp among known columns.
    /// </summary>
    /// <param name="columns">The set of column names currently present on <c>clipboard_items</c>.</param>
    /// <returns>A SQL expression suitable for use in a <c>SELECT</c> list.</returns>
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

    /// <summary>
    /// Reads the persisted schema version from <c>schema_meta</c>, or returns 0 when unset.
    /// </summary>
    /// <returns>The stored schema version, or 0 if missing or invalid.</returns>
    private int GetStoredSchemaVersion()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM schema_meta WHERE key = 'schema_version';";
        var result = cmd.ExecuteScalar();
        return result is string s && int.TryParse(s, out var v) ? v : 0;
    }

    /// <summary>
    /// Upserts the schema version marker in <c>schema_meta</c>.
    /// </summary>
    /// <param name="version">The schema version number to persist.</param>
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

    /// <summary>
    /// Returns the column names defined on <c>clipboard_items</c>, compared case-insensitively.
    /// </summary>
    /// <returns>A set of column names from <c>PRAGMA table_info</c>.</returns>
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

    /// <summary>
    /// Executes a non-query SQL statement on the open connection.
    /// </summary>
    /// <param name="sql">The SQL to execute.</param>
    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts a clipboard item, assigns its generated <see cref="ClipboardItem.Id"/>, and trims excess history.
    /// </summary>
    /// <param name="item">The item to persist; its <see cref="ClipboardItem.Id"/> is updated on success.</param>
    /// <returns>The database identifier assigned to the new row.</returns>
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

    /// <summary>
    /// Returns the most recently copied items, ordered by <see cref="ClipboardItem.CopiedAt"/> descending.
    /// </summary>
    /// <param name="count">Maximum number of items to return.</param>
    /// <returns>A read-only list of matching clipboard items.</returns>
    public IReadOnlyList<ClipboardItem> GetRecent(int count = 100) =>
        Search(new HistorySearchCriteria { Limit = count });

    /// <summary>
    /// Returns distinct non-empty source application names seen in history, sorted alphabetically.
    /// </summary>
    /// <param name="limit">Maximum number of names to return.</param>
    /// <returns>A read-only list of application display names.</returns>
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

    /// <summary>
    /// Searches clipboard history using the supplied filter criteria.
    /// </summary>
    /// <param name="criteria">
    /// Search filters and result limit. When <see langword="null"/>, defaults are used.
    /// </param>
    /// <returns>A read-only list of matching items, most recent first.</returns>
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

    /// <summary>
    /// Converts a <see cref="HistoryTimeRange"/> value to the UTC cutoff used in SQL filters.
    /// </summary>
    /// <param name="range">The selected time window.</param>
    /// <returns>The earliest <see cref="DateTimeOffset"/> included by the range, in UTC.</returns>
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

    /// <summary>
    /// Escapes wildcard characters for use in SQLite <c>LIKE</c> patterns with an escape character.
    /// </summary>
    /// <param name="value">The raw user search text.</param>
    /// <returns>A literal-safe fragment for embedding in a <c>LIKE</c> pattern.</returns>
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// Deletes a single history entry by identifier.
    /// </summary>
    /// <param name="id">The database row identifier to remove.</param>
    public void Delete(long id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM clipboard_items WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Sets whether a history entry is pinned.
    /// </summary>
    /// <param name="id">The database row identifier to update.</param>
    /// <param name="pinned"><see langword="true"/> to pin the item; <see langword="false"/> to unpin it.</param>
    public void SetPinned(long id, bool pinned)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE clipboard_items SET is_pinned = @p WHERE id = @id;";
        cmd.Parameters.AddWithValue("@p", pinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Removes all rows from clipboard history.
    /// </summary>
    public void ClearAll()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM clipboard_items;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes unpinned items beyond the configured history limit, keeping the newest entries.
    /// </summary>
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

    /// <summary>
    /// Maps a data reader row to a <see cref="ClipboardItem"/> instance.
    /// </summary>
    /// <param name="reader">An open reader positioned on a result row.</param>
    /// <returns>A populated clipboard item.</returns>
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

    /// <summary>
    /// Reads a string column, returning an empty string when the value is SQL NULL.
    /// </summary>
    /// <param name="reader">The active data reader.</param>
    /// <param name="ordinal">Zero-based column ordinal.</param>
    /// <returns>The column value, or <see cref="string.Empty"/> if NULL.</returns>
    private static string ReadStringOrEmpty(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);

    /// <summary>
    /// Parses an ISO-8601 timestamp column, falling back to UTC now when missing or invalid.
    /// </summary>
    /// <param name="reader">The active data reader.</param>
    /// <param name="ordinal">Zero-based column ordinal.</param>
    /// <returns>The parsed timestamp, or <see cref="DateTimeOffset.UtcNow"/> on failure.</returns>
    private static DateTimeOffset ReadDateTime(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return DateTimeOffset.UtcNow;
        var text = reader.GetString(ordinal);
        return DateTimeOffset.TryParse(text, out var dt) ? dt : DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Releases the underlying SQLite connection.
    /// </summary>
    public void Dispose() => _connection.Dispose();
}
