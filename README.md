# Mobile CD Inventory
[cite_start]A high-performance .NET Avalonia application designed to catalog and browse a personal collection of over 800 CDs[cite: 2]. [cite_start]Optimized for the **Google Pixel 8 Pro** and cross-platform desktop use[cite: 4, 11], this app features professional-grade indexing and on-demand data retrieval.

## 🚀 Key Features

* [cite_start]**Professional Artist Indexing**: Implements a `SortArtist` system that follows record-store archival standards, ensuring "Johannes Brahms" sorts by last name while "Coldplay" remains under "C"[cite: 1].
* [cite_start]**On-Demand Track Listing**: Employs "Lazy Loading" to fetch album tracks via UPC only when selected, protecting data usage in low-connectivity environments[cite: 3].
* **Intelligent String Sorting**: Advanced SQL logic sorts track positions (e.g., "1-1", "1-10", "2-1") by string length and value to prevent common alphabetical sorting errors.
* [cite_start]**Dynamic Filtering**: Real-time search by Artist/Title and genre-based filtering that updates the UI reactively[cite: 3].
* [cite_start]**Seamless Sync**: A "Pick and Copy" database strategy allows for easy updates from a master SQLite source to the mobile device[cite: 5].

## 🛠️ Technical Stack
* **Framework**: Avalonia UI with CommunityToolkit.Mvvm.
* [cite_start]**Database**: SQLite (Microsoft.Data.Sqlite)[cite: 5].
* [cite_start]**Environment**: Developed on **SUSE Tumbleweed** with deployment targeting **Android (net10.0)**[cite: 8].
* **Design Pattern**: MVVM (Model-View-ViewModel) for clean separation of UI and business logic.

## 📂 Project Structure

* `MobileCDInventory/`: Core logic and ViewModels shared across platforms.
* [cite_start]`MobileCDInventory.Desktop/`: Windows desktop implementation for library management[cite: 11].
* [cite_start]`MobileCDInventory.Android/`: Mobile implementation optimized for the Google Pixel 8 Pro[cite: 4].

## 📝 Recent Version History

### [cite_start]v1.1.0 (March 15, 2026) [cite: 3]
* [cite_start]Added `SortArtist` database integration for professional alphabetization[cite: 1].
* Implemented `Track_Title` and `Position` display with length-aware sorting.
* Integrated an overlay UI for track listings with "Back to Collection" navigation.
* [cite_start]Optimized for mobile deployment on Google Pixel 8 Pro[cite: 4].