using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace MobileCDInventory.ViewModels;

// Reads, writes and merges wishlist.db files.
//
// The desktop app (AvaloniaCDInventory) writes the same schema. Rows are identified across devices by
// Artist + Title (WishID is local to each file), deletes are soft (Deleted = 1) so they can propagate,
// and every change stamps Modified (UTC ISO-8601, which sorts as text) so the newest version wins a merge.
public static class WishlistStore
{
    public static string LocalPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wishlist.db");

    public static string UtcStamp() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

    // Pooling is off so no connection keeps the file open while it is being copied or replaced
    public static SqliteConnection Open(string path)
    {
        var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        conn.Open();
        return conn;
    }

    public static bool HasWishlistTable(SqliteConnection conn)
    {
        using var cmd = new SqliteCommand("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'wishlist'", conn);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public static void EnsureSchema(SqliteConnection conn)
    {
        Execute(conn, @"CREATE TABLE IF NOT EXISTS wishlist (
                            WishID INTEGER PRIMARY KEY AUTOINCREMENT,
                            Artist TEXT,
                            Title TEXT NOT NULL,
                            Format TEXT,
                            Notes TEXT,
                            DateAdded TEXT,
                            Modified TEXT,
                            Deleted INTEGER NOT NULL DEFAULT 0
                        )");

        // Upgrade wishlists created before sync support existed
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = new SqliteCommand("PRAGMA table_info(wishlist)", conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read()) columns.Add(reader["name"].ToString() ?? "");
        }

        if (!columns.Contains("Modified")) Execute(conn, "ALTER TABLE wishlist ADD COLUMN Modified TEXT");
        if (!columns.Contains("Deleted")) Execute(conn, "ALTER TABLE wishlist ADD COLUMN Deleted INTEGER NOT NULL DEFAULT 0");

        Execute(conn, "UPDATE wishlist SET Modified = COALESCE(DateAdded, '2000-01-01') || 'T00:00:00.000Z' WHERE Modified IS NULL");
    }

    // Brings both files to the same contents: for each Artist + Title, the most recently modified row wins.
    // No VACUUM is run, so neither file ever gets smaller; see MainView.BtnSyncWish_Click for why that matters.
    // Returns the number of live targets after the merge.
    public static int Merge(string localPath, string remotePath)
    {
        using var local = Open(localPath);
        using var remote = Open(remotePath);
        EnsureSchema(local);
        EnsureSchema(remote);

        var localRows = ReadAll(local);
        var remoteRows = ReadAll(remote);

        var winners = new Dictionary<string, Row>();
        foreach (var row in localRows) Consider(winners, row);
        foreach (var row in remoteRows) Consider(winners, row);

        Apply(local, localRows, winners);
        Apply(remote, remoteRows, winners);

        int live = 0;
        foreach (var row in winners.Values) if (!row.Deleted) live++;
        return live;
    }

    private static void Consider(Dictionary<string, Row> winners, Row row)
    {
        // On a timestamp tie, prefer the delete so removals stick
        if (!winners.TryGetValue(row.Key, out var current)
            || string.CompareOrdinal(row.Modified, current.Modified) > 0
            || (row.Modified == current.Modified && row.Deleted && !current.Deleted))
        {
            winners[row.Key] = row;
        }
    }

    private static void Apply(SqliteConnection conn, List<Row> existing, Dictionary<string, Row> winners)
    {
        using var tx = conn.BeginTransaction();

        var seen = new HashSet<string>();
        foreach (var row in existing)
        {
            if (!seen.Add(row.Key))
            {
                // A duplicate of a target already handled in this file
                using var del = new SqliteCommand("DELETE FROM wishlist WHERE WishID = @id", conn, tx);
                del.Parameters.AddWithValue("@id", row.WishID);
                del.ExecuteNonQuery();
                continue;
            }

            var w = winners[row.Key];
            if (row.SameAs(w)) continue;

            using var cmd = new SqliteCommand(@"UPDATE wishlist SET Artist = @artist, Title = @title, Format = @format, Notes = @notes,
                                                DateAdded = @dateAdded, Modified = @modified, Deleted = @deleted
                                                WHERE WishID = @id", conn, tx);
            AddParameters(cmd, w);
            cmd.Parameters.AddWithValue("@id", row.WishID);
            cmd.ExecuteNonQuery();
        }

        foreach (var w in winners.Values)
        {
            if (seen.Contains(w.Key)) continue;

            using var cmd = new SqliteCommand(@"INSERT INTO wishlist (Artist, Title, Format, Notes, DateAdded, Modified, Deleted)
                                                VALUES (@artist, @title, @format, @notes, @dateAdded, @modified, @deleted)", conn, tx);
            AddParameters(cmd, w);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static void AddParameters(SqliteCommand cmd, Row r)
    {
        cmd.Parameters.AddWithValue("@artist", r.Artist);
        cmd.Parameters.AddWithValue("@title", r.Title);
        cmd.Parameters.AddWithValue("@format", r.Format);
        cmd.Parameters.AddWithValue("@notes", r.Notes);
        cmd.Parameters.AddWithValue("@dateAdded", r.DateAdded);
        cmd.Parameters.AddWithValue("@modified", r.Modified);
        cmd.Parameters.AddWithValue("@deleted", r.Deleted ? 1 : 0);
    }

    private static List<Row> ReadAll(SqliteConnection conn)
    {
        var rows = new List<Row>();
        using var cmd = new SqliteCommand("SELECT WishID, Artist, Title, Format, Notes, DateAdded, Modified, Deleted FROM wishlist", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new Row
            {
                WishID = reader.GetInt64(0),
                Artist = reader["Artist"]?.ToString() ?? "",
                Title = reader["Title"]?.ToString() ?? "",
                Format = reader["Format"]?.ToString() ?? "",
                Notes = reader["Notes"]?.ToString() ?? "",
                DateAdded = reader["DateAdded"]?.ToString() ?? "",
                Modified = reader["Modified"]?.ToString() ?? "",
                Deleted = Convert.ToInt64(reader["Deleted"]) != 0
            });
        }
        return rows;
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = new SqliteCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private class Row
    {
        public long WishID;
        public string Artist = "", Title = "", Format = "", Notes = "", DateAdded = "", Modified = "";
        public bool Deleted;

        public string Key => $"{Artist.Trim().ToLowerInvariant()}\u001F{Title.Trim().ToLowerInvariant()}";

        public bool SameAs(Row o) => Artist == o.Artist && Title == o.Title && Format == o.Format && Notes == o.Notes
                                     && DateAdded == o.DateAdded && Modified == o.Modified && Deleted == o.Deleted;
    }
}
