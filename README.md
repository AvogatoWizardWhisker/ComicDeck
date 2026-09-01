# ComicDeck
A simple comic book reader app (.cbz, .cbr). built with WinUI 3

## Project Structure
```
ComicDeck/
├── ComicDeck.sln
├── ComicDeck/                          # Main WinUI 3 app project
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs
│   │
│   ├── Views/
│   │   ├── LibraryPage.xaml
│   │   ├── ReaderPage.xaml
│   │   ├── SettingsPage.xaml
│   │   └── ComicDetailsPage.xaml
│   │
│   ├── ViewModels/
│   │   ├── LibraryViewModel.cs
│   │   ├── ReaderViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   └── ComicDetailsViewModel.cs
│   │
│   ├── Controls/                       # Custom XAML controls
│   │   ├── PageFlipView.xaml
│   │   └── ThumbnailGridItem.xaml
│   │
│   ├── Converters/
│   ├── Helpers/
│   ├── Assets/
│   └── Package.appxmanifest
│
├── ComicDeck.Core/                     # Class library — models, interfaces, DTOs
│   ├── Models/
│   │   ├── ComicArchive.cs
│   │   ├── ComicPage.cs
│   │   ├── LibraryEntry.cs
│   │   ├── ReadingProgress.cs
│   │   └── ArchiveFormat.cs            # enum: Cbz, Cbr, Cb7, PdfComic...
│   │
│   ├── Services/                       # interfaces only
│   │   ├── IArchiveReaderService.cs
│   │   ├── IArchiveReaderFactory.cs
│   │   ├── IImageDecodingService.cs
│   │   ├── IThumbnailCacheService.cs
│   │   ├── ILibraryService.cs
│   │   ├── IReadingProgressService.cs
│   │   ├── IMetadataService.cs
│   │   └── ISettingsService.cs
│   │
│   └── Exceptions/
│       ├── UnsupportedArchiveException.cs
│       └── CorruptArchiveException.cs
│
├── ComicDeck.Services/                 # Class library — concrete implementations
│   ├── Archives/
│   │   ├── CbzArchiveReaderService.cs
│   │   ├── CbrArchiveReaderService.cs
│   │   ├── Cb7ArchiveReaderService.cs
│   │   └── ArchiveReaderFactory.cs
│   ├── Caching/
│   │   └── ThumbnailCacheService.cs
│   ├── Library/
│   │   ├── LibraryService.cs
│   │   └── ReadingProgressService.cs
│   ├── Imaging/
│   │   └── ImageDecodingService.cs
│   └── Persistence/
│       ├── ComicDeckDbContext.cs       # SQLite via EFCore or sqlite-net-pcl
│       └── SettingsService.cs
│
└── ComicDeck.Tests/
    ├── ArchiveReaderTests.cs
    ├── LibraryServiceTests.cs
    └── ThumbnailCacheTests.cs
```
