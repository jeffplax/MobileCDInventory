using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq; 
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MobileCDInventory.ViewModels;

namespace MobileCDInventory.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            // Manually ensure the DataContext is set if it isn't already
            if (DataContext == null)
            {
                DataContext = new MainViewModel();
            }            

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            
            var lblVersion = this.FindControl<TextBlock>("LblVersion");
            var lblBuildDate = this.FindControl<TextBlock>("LblBuildDate");

            if (lblVersion != null && version != null)
            {
                lblVersion.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }

            // Read the exact date string we injected via the .csproj file
            var buildDateAttr = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                                        .Cast<System.Reflection.AssemblyMetadataAttribute>()
                                        .FirstOrDefault(attr => attr.Key == "BuildDate");

            if (lblBuildDate != null && buildDateAttr != null)
            {
                lblBuildDate.Text = $"Build Date: {buildDateAttr.Value}";
            }

        }

        private void CloseAbout_Click(object? sender, RoutedEventArgs e)
        {
            // Switch back to the first tab (Master Library). The About tab stays available.
            MainTabs.SelectedIndex = 0;
        }
        // Opens the file picker at the folder of the last file picked for this purpose (e.g. the OneDrive
        // WindowsCode folder) instead of wherever Android defaults to, such as Downloads. Only the location is
        // remembered: the user still chooses the file each time, and no lasting access to it is kept.
        private static async Task<IStorageFile?> PickFileAsync(TopLevel topLevel, string title, string locationKey)
        {
            var locationFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"last_location_{locationKey}.txt");

            IStorageFolder? startLocation = null;
            try
            {
                // Android's picker accepts a file's content:// URI as its start location and opens that file's
                // folder. If the provider can't, it just opens at its default.
                if (File.Exists(locationFile))
                    startLocation = await topLevel.StorageProvider.OpenFolderBookmarkAsync(File.ReadAllText(locationFile).Trim());
            }
            catch (Exception)
            {
                startLocation = null; // A stale location isn't worth failing the pick over
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                SuggestedStartLocation = startLocation
            });
            if (files.Count == 0) return null;

            try { File.WriteAllText(locationFile, files[0].Path.ToString()); } catch (Exception) { }
            return files[0];
        }

        private async void BtnSync_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            try
            {
                var inventoryFile = await PickFileAsync(topLevel, "Select inventory.db (Check your OneDrive folder)", "inventory");

                if (inventoryFile != null)
                {
                    // Use a single 'if' check to get our ViewModel
                    if (DataContext is MainViewModel vm)
                    {
                        // 1. Close connection so we can overwrite the file
                        vm.CloseConnection();

                        // 2. Define the path
                        var vaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        var dbPath = Path.Combine(vaultFolder, "inventory.db");   
                        
                        // 3. Copy the file
                        await using var sourceStream = await inventoryFile.OpenReadAsync();
                        using (var destinationStream = File.Create(dbPath))
                        {
                            await sourceStream.CopyToAsync(destinationStream);
                        }

                        // 4. Update the UI and Connect
                        vm.StatusMessage = "File picked. Connecting...";
                        vm.ConnectToDatabase(dbPath);
                    }
                    else
                    {
                        Console.WriteLine("DEBUG: DataContext is null or wrong type!");
                    }
                }
            }
            catch (Exception ex)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.StatusMessage = $"Sync Error: {ex.Message}";
                }
            }
        }
        

        // ====================================================================
        // Wish List Sync
        // ====================================================================
        // Pick the shared wishlist.db in OneDrive, merge it with the phone's copy (see WishlistStore.Merge),
        // then write the merged result back to both. Adds and deletes made on either device survive, instead of
        // whichever copy was pushed or pulled last overwriting the other.
        private async void BtnSyncWish_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || DataContext is not MainViewModel vm) return;

            try
            {
                var remoteFile = await PickFileAsync(topLevel, "Select wishlist.db (Check your OneDrive folder)", "wishlist");
                if (remoteFile == null) return;

                vm.StatusMessage = "Syncing Wish List...";

                var remoteCopyPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wishlist_remote.db");
                await using (var source = await remoteFile.OpenReadAsync())
                await using (var destination = File.Create(remoteCopyPath))
                {
                    await source.CopyToAsync(destination);
                }

                using (var conn = WishlistStore.Open(remoteCopyPath))
                {
                    if (!WishlistStore.HasWishlistTable(conn))
                    {
                        vm.StatusMessage = "That file isn't a Wish List (no wishlist table). Nothing changed.";
                        return;
                    }
                }

                int count = WishlistStore.Merge(WishlistStore.LocalPath, remoteCopyPath);

                // Write back into the file that was picked. A Save dialog would make Android create
                // "wishlist (1).db" alongside the original, which the desktop never reads. Some Android storage
                // providers don't truncate on write; that's safe here because the merged copy is never smaller.
                await using (var source = File.OpenRead(remoteCopyPath))
                await using (var destination = await remoteFile.OpenWriteAsync())
                {
                    await source.CopyToAsync(destination);
                }

                vm.LoadWishlistFromDB();
                vm.StatusMessage = $"Wish List synced: {count} targets.";
            }
            catch (Exception ex)
            {
                vm.StatusMessage = $"Wish List Sync Error: {ex.Message}";
            }
        }
    }
}