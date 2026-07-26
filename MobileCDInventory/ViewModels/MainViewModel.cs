using System;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Collections.ObjectModel;

namespace MobileCDInventory.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusMessage = "Ready to load database.";

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _selectedGenre = "All Genres";

    [ObservableProperty]
    private bool _sortByYear = false;

    [ObservableProperty]
    private string _sortButtonText = "Sort: Artist";

    [ObservableProperty]
    private bool _isShowingTracks = false;

    [ObservableProperty]
    private Album? _selectedAlbum;

    // These trigger automatically whenever the user types or selects a new dropdown item!
    partial void OnSearchQueryChanged(string value) => LoadDataFromDB();
    partial void OnSelectedGenreChanged(string value) => LoadDataFromDB();

    public ObservableCollection<Track> CurrentAlbumTracks { get; } = new();

    // Lists to hold the data for binding to the UI
    public ObservableCollection<Album> Albums { get; } = new();
    public ObservableCollection<string> Genres { get; } = new();

    public ObservableCollection<WishlistItem> Wishlist { get; } = new();

    [ObservableProperty] private string _wishArtist = "";
    [ObservableProperty] private string _wishTitle = "";
    [ObservableProperty] private string _wishFormat = "CD";
    [ObservableProperty] private string _wishNotes = "";

    private string _connectionString = string.Empty;

    public MainViewModel()
    {
        LoadWishlistFromDB();
    }

    public void ConnectToDatabase(string secureDbPath)
    {
        try
        {
            // set up connection string to use vaut path
            _connectionString = $"Data Source={secureDbPath}; Mode=ReadOnly;";

            // Load the data into lists
            LoadDataFromDB();
            LoadGenres();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void LoadDataFromDB()
    {
        if (string.IsNullOrEmpty(_connectionString)) return;

        try
        {
            Albums.Clear();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            // Base query
            string sql = "SELECT * FROM albums WHERE 1=1"; // Base query to allow for easy filtering

            // Add genre filter if a specific genre is selected
            if (!string.IsNullOrEmpty(SelectedGenre) && SelectedGenre != "All Genres")
            {
                sql += " AND Genre = @genre";
            }

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                sql += " AND (Artist LIKE @q OR Title LIKE @q)";
            }
            if (SortByYear)
            {
                sql += " ORDER BY Year DESC, COALESCE(SortArtist, Artist) ASC";
            }
            else
            {
                sql += " ORDER BY COALESCE(SortArtist, Artist) ASC, Year DESC";
            }

            using var command = new SqliteCommand(sql, connection);
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                command.Parameters.AddWithValue("@q", $"%{SearchQuery}%");
            }

            if (!string.IsNullOrEmpty(SelectedGenre) && SelectedGenre != "All Genres")
            {
                command.Parameters.AddWithValue("@genre", SelectedGenre);
            }

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Albums.Add(new Album
                    {
                        UPC = reader["UPC"].ToString() ?? "",
                        Artist = reader["Artist"].ToString() ?? "",
                        SortArtist = reader["SortArtist"]?.ToString() ?? reader["Artist"].ToString() ?? "",
                        Title = reader["Title"].ToString() ?? "",
                        Year = reader["Year"].ToString() ?? "",
                        Format = reader["Format"].ToString() ?? "",
                        Label = reader["Label"].ToString() ?? "",
                        Genre = reader["Genre"]?.ToString() ?? "",
                        Styles = reader["Styles"]?.ToString() ?? ""
                    });
                }
            }
            // Update the status message with the count of albums loaded
            StatusMessage = $"Showing {Albums.Count} Albums";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading data: {ex.Message}";
        }
    }

    // This triggers automatically when you tap an album in the list
    partial void OnSelectedAlbumChanged(Album? value)
    {
        if (value != null)
        {
            LoadTracksForSelectedAlbum(value.UPC);
            IsShowingTracks = true;
        }
    }

    public void CloseTracks()
    {
        IsShowingTracks = false;
        SelectedAlbum = null; // Reset selection so you can tap the same album again
    }

    private void LoadTracksForSelectedAlbum(string upc)
    {
        CurrentAlbumTracks.Clear();

        if(string.IsNullOrEmpty(_connectionString)) return;

        try
        {

            // Use a new connection for this query
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Query to get tracks for the selected album by UPC
            string sql = "SELECT Position, Track_Title, Duration FROM tracks WHERE UPC = @upc ORDER BY length(Position) ASC,Position ASC";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@upc", upc);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                CurrentAlbumTracks.Add(new Track
                {
                    TrackNumber = reader["Position"]?.ToString() ?? "",
                    Title = reader["Track_Title"]?.ToString() ?? "",
                    Duration = reader["Duration"]?.ToString() ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading tracks: {ex.Message}";
        }
    }

    private void LoadGenres()
    {
        // Check if we have a connection string yet
        if (string.IsNullOrEmpty(_connectionString)) return;

        try
        {
            // Start with the default "All" option
            var genreList = new System.Collections.Generic.List<string> { "All Genres" };

            // Use Microsoft.Data.Sqlite classes
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();

                string sql = "SELECT DISTINCT Genre FROM albums WHERE Genre IS NOT NULL AND Genre != '' ORDER BY Genre ASC";
                
                using var cmd = new SqliteCommand(sql, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    genreList.Add(reader.GetString(0));
                }
            }

            // Clear and update the ObservableCollection
            // This automatically updates the UI ComboBox because of the binding
            Genres.Clear();
            foreach (var g in genreList)
            {
                Genres.Add(g);
            }

            // Set the default selection
            SelectedGenre = "All Genres";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Genre Load Error: {ex.Message}";
        }
    }

    public void CloseConnection()
    {
        // Tells SQLite to let go of the database file so it can be replaced or deleted
        SqliteConnection.ClearAllPools();
        _connectionString = string.Empty;
    }
     
    public void ToggleSort()
    {
        SortByYear = !SortByYear;
        
        // Update the button label
        SortButtonText = SortByYear ? "Sort: Year" : "Sort: Artist";
        
        // Refresh the list with the new order
        LoadDataFromDB();
    }

    // WISHLIST METHODS
    public void LoadWishlistFromDB()
    {
        var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wishlistDbPath = Path.Combine(vaultFolder, "wishlist.db");

        if (!File.Exists(wishlistDbPath)) return;

        try
        {
            Wishlist.Clear();
            string connectionString = $"Data Source={wishlistDbPath}";

            using var conn = new SqliteConnection(connectionString);
            conn.Open();
            string sql = "SELECT WishID, Artist, Title, Format, Notes FROM wishlist ORDER BY DateAdded DESC";

            using var cmd = new SqliteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                Wishlist.Add(new WishlistItem
                {
                    wishID = Convert.ToInt32(reader["WishID"]),
                    Artist = reader["Artist"]?.ToString() ?? "",
                    Title = reader["Title"]?.ToString() ?? "",
                    Format = reader["Format"]?.ToString() ?? "",
                    Notes = reader["Notes"]?.ToString() ?? ""
                });
            }
        }
        catch(Exception ex)
        {
            StatusMessage = $"Error loading Wish List: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveWish()
    {
        if (string.IsNullOrWhiteSpace(WishTitle))
        {
            StatusMessage = "Wishlist: Title cannot be empty.";
            return;
        }
        try
        {
            var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var wishlistDbPath = Path.Combine(vaultFolder, "wishlist.db");
            string connectionString = $"Data Source={wishlistDbPath}";

            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            string CreateTableSql = @"CREATE TABLE IF NOT EXISTS ""wishlist"" (
                                        ""WishID"" INTEGER PRIMARY KEY AUTOINCREMENT,
                                        ""Artist"" TEXT,
                                        ""Title"" TEXT,
                                        ""Format"" TEXT,
                                        ""Notes"" TEXT,
                                        ""DateAdded"" TEXT
                                      );";
            using (var createCmd = new SqliteCommand(CreateTableSql, conn))
            {
                createCmd.ExecuteNonQuery();
            }
            
            string insertSql = @"INSERT INTO wishlist (Artist, Title, Format, Notes, DateAdded)
                                 VALUES (@Artist, @Title, @Format, @Notes, @DateAdded);";

            using (var insertCmd = new SqliteCommand(insertSql, conn))
            {
                insertCmd.Parameters.AddWithValue("@Artist", WishArtist ?? "");
                insertCmd.Parameters.AddWithValue("@Title", WishTitle);
                insertCmd.Parameters.AddWithValue("@Format", WishFormat ?? "");
                insertCmd.Parameters.AddWithValue("@Notes", WishNotes ?? "");
                insertCmd.Parameters.AddWithValue("@DateAdded", DateTime.Now.ToString("yyyy-MM-dd"));
                insertCmd.ExecuteNonQuery();
            }

            // Clear input fields upon successful save
            WishArtist = "";
            WishTitle = "";
            WishFormat = "";
            WishNotes = "";

            StatusMessage = "Wish List item saved successfully.";
            LoadWishlistFromDB(); // Refresh the wish list display            
        }
        catch(Exception ex)
        {
            StatusMessage = $"Error saving to Wish List: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DeleteWish(WishlistItem item)
    {
        if (item == null) return;

        try
        {
            var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var wishlistDbPath = Path.Combine(vaultFolder, "wishlist.db");
            string connectionString = $"Data Source={wishlistDbPath};";

            using var conn = new SqliteConnection(connectionString);
            conn.Open();            
            string deleteSql = "DELETE FROM wishlist WHERE WishID = @wishID;";
            using var deleteCmd = new SqliteCommand(deleteSql, conn);
            deleteCmd.Parameters.AddWithValue("@wishID", item.wishID);
            deleteCmd.ExecuteNonQuery();
            
            StatusMessage = "Wish List item deleted successfully.";
            LoadWishlistFromDB(); // Refresh the wish list display
        }
        catch(Exception ex)
        {
            StatusMessage = $"Error deleting from Wish List: {ex.Message}";
        }    
    }

    // --- DATA MODEL ---
    public class Album 
    {
        public string UPC { get; set; } = "";
        public string Artist { get; set; } = "";
        public string SortArtist { get; set; } = "";
        public string Title { get; set; } = "";
        public string Year { get; set; } = "";
        public string Format { get; set; } = "";
        public string Label { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Styles { get; set; } = "";
    }

    public class Track
    {
        public string TrackNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string Duration { get; set; } = "";
    }

    public class WishlistItem
    {
        public int wishID { get; set; }
        public string Artist { get; set; } = "";
        public string Title { get; set; } = "";
        public string Format { get; set; } = "";
        public string Notes { get; set; } = "";
        public string DateAdded { get; set; } = "";
    }
}
