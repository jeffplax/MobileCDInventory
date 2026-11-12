# Mobile CD Inventory
A high-performance .NET Avalonia application designed to catalog and browse a personal collection of over 500 CDs. Optimized for the **Google Pixel 8 Pro**, this app features professional-grade indexing and on-demand data retrieval.

## 🚀 Key Features

* **Professional Artist Indexing**: Implements a `SortArtist` system that follows record-store archival standards, ensuring "Johannes Brahms" sorts by last name while "Coldplay" remains under "C".
* **On-Demand Track Listing**: Employs "Lazy Loading" to fetch album tracks via UPC only when selected, protecting data usage in low-connectivity environments.
* **Correct Track Order**: Tracks are listed in the order Discogs gives them, so multi-disc sets read disc 1 then disc 2 (e.g., "1-1" … "1-10", then "2-1").
* **Dynamic Filtering**: Real-time search by Artist/Title and genre-based filtering that updates the UI reactively.
* **Seamless Sync**: A "Pick and Copy" database strategy allows for easy updates from a master SQLite source to the mobile device.

## 🛠️ Technical Stack
* **Framework**: Avalonia UI with CommunityToolkit.Mvvm.
* **Database**: SQLite (Microsoft.Data.Sqlite).
* **Environment**: Developed on **SUSE Tumbleweed** with deployment targeting **Android (net10.0)**.
* **Design Pattern**: MVVM (Model-View-ViewModel) for clean separation of UI and business logic.

## 📂 Project Structure

* `MobileCDInventory/`: Core logic and ViewModels shared across platforms.
* ~~`MobileCDInventory.Desktop/`: Windows desktop implementation for library management.~~
* ~~`MobileCDInventory.Browser/`: Browser implementation.~~
* ~~`MobileCDInventory.iOS/`: Apple iOS implementation.~~
* `MobileCDInventory.Android/`: Mobile implementation optimized for the Google Pixel 8 Pro.

## 📝 Recent Version History

### v1.1.0 (March 15, 2026)
* Added `SortArtist` database integration for professional alphabetization.
* Implemented `Track_Title` and `Position` display with length-aware sorting.
* Integrated an overlay UI for track listings with "Back to Collection" navigation.
* Optimized for mobile deployment on Google Pixel 8 Pro.

### v1.2.0 (May 20, 2026)
* Added an 'About' tab
* Added a 'Wishlist' tab to identify CDs for future purchase
* Removed the desktop, browser and iOS build versions from the project

### v1.3.0 (October 15, 2026)
* Cleaned up code 
* Fixed some synching issues with wishlist

### v1.5.0 (November 2026)
* New app icon matching the desktop app, and the splash screen now shows it
* The library opens at launch from the last synced copy, so it works offline in a store
* Multi-disc tracklists come out in the correct order
* Genre filter lists each genre once and matches albums with several genres
* Signed release builds published on GitHub; new app ID `com.jeffplax.cdinventory`
* Updated the bundled SQLite engine (security fix)

## 📲 Installing and Updating
1. On the phone, open the repo's **Releases** page and tap the latest `CDInventory-<version>.apk`.
2. If Android asks, allow your browser to **install unknown apps**, then tap **Install** (or **Update**).

Updates install over the top and keep your data, because every release is signed with the same key.

### One-time switch from the old app (versions before 1.5.0)
Version 1.5.0 has a new app ID, so it installs *next to* the old app instead of replacing it. Phone data doesn't carry over by itself:
1. In the **old** app, run the **Wish List sync** so your wishlist is saved to OneDrive.
2. Install 1.5.0 from Releases.
3. In the **new** app, tap **Sync DB** to load the library, then run the Wish List sync to bring the wishlist back.
4. Once everything's there, uninstall the old app.

## 🔨 Building
Requires the .NET 10 SDK with the Android workload (`dotnet workload install android`) and JDK 21.

Connect the phone by USB with **USB debugging** on (*Settings → System → Developer options*) and approve the computer when the phone asks. Then:
```powershell
cd MobileCDInventory.Android
dotnet build -t:Run -c Release
```
This builds, installs and opens the app. To make an APK file instead, use `dotnet publish -c Release`; the APK ending in `-Signed.apk` is in `bin\Release\net10.0-android\publish\`.

**Signing local builds with the release key.** Without this, local builds are signed with your computer's debug key and won't install over the released app (Android reports a signature conflict). Set these once, as user environment variables, then open a new terminal:
```powershell
setx CDINV_KEYSTORE "$HOME\Documents\cdinventory.keystore"
setx CDINV_KEYSTORE_PASSWORD "<keystore password>"
setx CDINV_KEY_PASSWORD "<key password>"
```
Keep the keystore file out of the repo folder, and back it up with its password: without them, future versions can't install as updates.

**On Linux,** the same commands work; set the variables with `export` in your shell profile instead of `setx`.

## 🚢 Releasing a New Version
1. Set `<Version>` in `Directory.Build.props` (e.g. `1.6.0`) and merge that change to `main`.
2. Tag that commit and push the tag:
   ```powershell
   git tag v1.6.0
   git push origin v1.6.0
   ```
3. GitHub Actions builds the APK, signs it with the release key, and publishes it on the Releases page. The build stops if the tag doesn't match `<Version>`.
