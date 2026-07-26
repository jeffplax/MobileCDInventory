# Mobile CD Inventory
A high-performance .NET Avalonia application designed to catalog and browse a personal collection of over 500 CDs. Optimized for the **Google Pixel 8 Pro**, this app features professional-grade indexing and on-demand data retrieval.

## 🚀 Key Features

* **Professional Artist Indexing**: Implements a `SortArtist` system that follows record-store archival standards, ensuring "Johannes Brahms" sorts by last name while "Coldplay" remains under "C".
* **On-Demand Track Listing**: Employs "Lazy Loading" to fetch album tracks via UPC only when selected, protecting data usage in low-connectivity environments.
* **Intelligent String Sorting**: Advanced SQL logic sorts track positions (e.g., "1-1", "1-10", "2-1") by string length and value to prevent common alphabetical sorting errors.
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
