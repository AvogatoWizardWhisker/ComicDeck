# ComicDeck

**ComicDeck** is a modern, fast, and native Windows comic book reader built with **WinUI 3** and **.NET 10**. Designed for comic enthusiasts with large digital collections, it combines a clean Mica-backdrop interface with smart archive handling, automatic metadata extraction, and a responsive SQLite-backed library.

> \[!NOTE\]
> ComicDeck is currently in **active development**. Features, database schemas, and user interfaces may evolve between releases.

## ✨ Features

### 📖 Modern Reader Experience

* **Multiple Reading Modes:** Switch seamlessly between **Single-Page** and **Double-Page Spread** views (automatically keeping covers and wide landscape spreads standalone).

* **Flexible Fit & Zoom:** Choose between *Fit Page*, *Fit Width*, *Fit Height*, or *Original Size*, with smooth mouse-wheel/touchpad zoom and double-tap zoom.

* **Manga & RTL Support:** Native Right-to-Left reading direction that automatically activates based on `ComicInfo.xml` metadata or can be toggled manually.

* **Smart Memory & Caching:** Asynchronous page preloading (`PageCache`) with viewport-aware decode sizing keeps page turns instant while minimizing memory usage.

* **Distraction-Free UI:** Auto-hiding top/bottom HUD overlays, full-screen mode (`F11`), drag-and-drop file opening, and Windows file-type associations (`.cbz`, `.cbr`, `.cb7`, `.cbt`).

### 📚 Library & Series Management

* **Multi-Folder Watching:** Add multiple root directories and recursively scan them for comic archives without blocking the UI.

* **Automatic Series Grouping:** Intelligently groups issues into series (normalizing punctuation and casing) with a dedicated Series view and "Continue Reading" next-issue tracking.

* **Search, Filter & Sort:** Instant debounced search (`Ctrl+F`) across titles, series, issue numbers, and filenames. Filter by *Unread*, *In Progress*, *Completed*, or *Favorites*, and sort by *Series*, *Title*, *Recently Added*, or *Recently Read* with an A–Z SemanticZoom jump list.

* **Progress Persistence:** Automatically saves your current page, completion status, and favorites in a local SQLite database.

* **On-Demand Cover Thumbnails:** Generates and caches resized JPEG cover thumbnails locally for smooth virtualized grid scrolling.

### 🏷️ Metadata & Archive Engine

* **Magic-Byte Format Detection:** Identifies `.cbz` (ZIP), `.cbr` (RAR4/RAR5), `.cb7` (7z), and `.cbt` (TAR) by file header bytes rather than trusting file extensions alone.

* **Solid Archive Optimization:** Automatically extracts solid RAR/7z archives to a temporary background cache on first read so jumping to later pages is instantaneous.

* **Natural Page Sorting:** Sorts pages naturally (`page2.jpg` before `page10.jpg`) while filtering out OS junk files (`__MACOSX`, `.DS_Store`, `Thumbs.db`).

* **Hybrid Metadata Pipeline:** Parses ComicRack-standard `ComicInfo.xml` files (with XXE protection) and falls back to smart regex filename parsing for missing fields.

## ⌨️ Reader Keyboard Shortcuts

| Key | Action | 
| ----- | ----- | 
| `Left` / `Right` | Step page left or right (respects LTR/RTL direction) | 
| `Up` / `Down` | Scroll vertically; turns page at top/bottom edge | 
| `Space` / `Page Down` | Advance viewport or turn to next page | 
| `Page Up` | Scroll up or turn to previous page | 
| `Home` / `End` | Jump to first or last page | 
| `D` | Toggle Double-Page Spread mode | 
| `R` | Toggle Left-to-Right / Right-to-Left reading direction | 
| `F` | Cycle through Fit modes | 
| `F11` | Toggle Full-Screen mode | 
| `Esc` | Exit Full-Screen or return to library | 

## 🏗️ Architecture & Project Structure

ComicDeck separates pure domain logic from the Windows UI layer so that archive parsing, metadata extraction, and database queries remain fast and thoroughly unit-tested:

```
ComicDeck/
├── ComicDeck/                  # WinUI 3 Desktop Application (net10.0-windows)
│   ├── Assets/                 # App icons, splash screens, and tile logos
│   ├── Helpers/                # Win32 interop helpers (FileOpenPicker, FolderPicker)
│   ├── Pages/                  # XAML views (Home, Library, Series, SeriesDetail, Reader, Settings)
│   ├── Services/               # UI-layer services (PageCache, PageImageLoader, CoverThumbnailService, ThemeService)
│   └── ViewModels/             # MVVM ViewModels powered by CommunityToolkit.Mvvm
│
├── ComicDeck.Core/             # Core Class Library (net10.0, zero UI dependencies)
│   ├── Archives/               # IComicArchive, ZipComicArchive, SharpCompressComicArchive, FormatDetector
│   ├── Data/                   # SQLite connection setup and repository implementations
│   ├── Library/                # LibraryScanner, SeriesGrouping, LibraryQuery, LibraryGrouping
│   ├── Metadata/               # ComicInfoParser, FileNameParser, MetadataService
│   ├── Models/                 # Domain entities (Comic, Series, ReadingProgress, FitMode, etc.)
│   └── Utilities/              # NaturalStringComparer, SpreadPlanner, DecodeSizing
│
└── ComicDeck.Core.Tests/       # xUnit v3 Test Suite

```

## 🚀 Getting Started

### Prerequisites

* **OS:** Windows 10 (version 1809 / build 17763) or Windows 11

* **SDK:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

* **IDE:** [Visual Studio 2022 / 2026](https://visualstudio.microsoft.com/) with the **Windows application development** workload (Windows App SDK) installed.

### Building and Running

1. **Clone the repository:**

   ```bash
   git clone https://github.com/AvogatoWizardWhisker/ComicDeck.git
   cd ComicDeck
   ```

2. **Restore and build the solution:**

   ```bash
   dotnet build ComicDeck.slnx
   ```

3. **Run the application:**
   Open `ComicDeck.slnx` in Visual Studio, select the `ComicDeck (Package)` launch profile, and press `F5` (or run via CLI with `dotnet run --project ComicDeck/ComicDeck.csproj`).

4. **Run the unit tests:**

   ```bash
   dotnet test ComicDeck.Core.Tests/ComicDeck.Core.Tests.csproj
   ```

## 🛠️ Built With

* [**Windows App SDK & WinUI 3**](https://github.com/microsoft/WindowsAppSDK) — Native Fluent Design UI, Mica backdrop, and hardware-accelerated image decoding.

* [**CommunityToolkit.Mvvm**](https://github.com/CommunityToolkit/dotnet) — Source-generated observable properties and MVVM architecture.

* [**SharpCompress**](https://github.com/adamhathcock/sharpcompress) — Multi-format archive extraction for `.cbr`, `.cb7`, and `.cbt`.

* [**sqlite-net-pcl**](https://github.com/praeclarum/sqlite-net) — Lightweight local SQLite database for library indexing and reading progress.

* [**xUnit v3**](https://xunit.net/) — Unit testing framework for `ComicDeck.Core`.

## 🤝 Contributing

Contributions, bug reports, and feature suggestions are welcome! Feel free to open an issue or submit a pull request on GitHub.

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE.txt) file for details.
