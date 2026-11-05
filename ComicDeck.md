# ComicDeck: what you'll need to build

The NavigationView template gives you the shell. Everything below is what goes inside it.

## 1. Archive handling (the core engine)

- **CBZ** is just a ZIP. `System.IO.Compression.ZipArchive` handles it with no extra dependencies.
- **CBR** is a RAR, and .NET has no built-in RAR support. **SharpCompress** (NuGet) is the usual choice and reads RAR4/RAR5, 7z, tar and zip, so it can also cover **CB7** and **CBT**. The alternative is the native UnRAR library, but its licensing is awkward.
- Build an `IComicArchive` abstraction with implementations per format and a factory that picks one by extension or magic bytes. Many files are misnamed (a `.cbr` that is really a ZIP is common), so sniffing the header is worth it.
- You'll need to:
  - List entries, filter to image files, and skip junk like `__MACOSX/`, `Thumbs.db` and `.DS_Store`.
  - Sort pages with natural sort (`page2` before `page10`). Plain alphabetical sorting breaks here.
  - Open a single page as a stream without extracting the whole archive.
  - Handle corrupt, empty or password-protected archives gracefully.
- **Solid RAR caveat:** in solid archives, jumping to page 80 means decompressing everything before it. For those, extract to a temp cache folder in the background, or read sequentially with read-ahead.

## 2. Metadata

- Parse **ComicInfo.xml** (the ComicRack standard) if present: series, number, title, writer, publisher, year, page count, manga/RTL flag, and so on.
- Fall back to parsing the filename and folder name (e.g. `Batman 012 (2016).cbz`).
- Optional later: online lookup via ComicVine or Metron.

## 3. Image pipeline

- Load pages into `BitmapImage` via `SetSourceAsync(IRandomAccessStream)` (convert from a `Stream` with `AsRandomAccessStream()`).
- Set `DecodePixelWidth` or `DecodePixelHeight` to match the display size. Comic pages are often 3000+ px tall, and decoding at full size will burn memory.
- Keep a small LRU cache of decoded pages (current page ±2) and preload neighbors in the background. Dispose old ones aggressively.
- Supported formats are JPG and PNG out of the box. WebP and AVIF depend on the user having the Microsoft codec extensions installed, so detect failures and show a helpful message.
- Generate and cache cover thumbnails (page 1 resized to about 300px) to disk.

## 4. The reader page

- **Reading modes:** single page, double-page spread, and continuous vertical scroll (webtoon style).
- **Fit modes:** fit width, fit height, fit page, original size.
- **Direction:** left-to-right, right-to-left (manga), and auto-detection from ComicInfo's manga flag.
- **Zoom and pan:** WinUI 3's `ScrollView` or `ScrollViewer` with `ZoomMode`, plus double-click or double-tap to zoom.
- **Input:** keyboard (arrows, space, Home/End), mouse wheel, click zones on the left and right edges, touch swipe, and gamepad if you want to be fancy.
- **UI:** an auto-hiding overlay with a page slider, a thumbnail strip, a page counter, and a fullscreen toggle (`AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen)`).
- **Double-page logic:** detect already-wide pages (spreads) and show them alone, and let the user shift the pairing offset.
- Save the current page on exit and resume from it.

I'd write a custom paging control rather than rely on `FlipView`. `FlipView` struggles with zoom, memory, and RTL, and you'll get more control with your own.

## 5. The library

- **Sources:** let users add folders to watch, using `FolderPicker`. Scan recursively and optionally use `FileSystemWatcher` for live updates.
- **Database:** SQLite, through either **EF Core** or **sqlite-net-pcl**. Tables include Comics, Series, Collections, ReadingProgress and Bookmarks, with fields like path, file hash or size or modified time (for change detection), page count, cover path, metadata, last read page, and read status.
- **UI:**
  - A grid of covers (`GridView` or `ItemsRepeater`, both virtualized) and a list or detail view.
  - Sort, filter, and search by title, series, publisher, unread or in progress, and so on.
  - Series grouping, with a series detail page listing issues.
  - Context menus: mark read or unread, add to a collection, open file location, remove from library.
- A "Continue Reading" shelf is a nice touch.
- Run scanning and thumbnailing as background jobs with progress reporting, so the UI never blocks.

## 6. Navigation structure

A suggested layout for the NavigationView:

- Home / Continue Reading
- Library (all comics)
- Series
- Collections
- Favorites
- Settings (as the built-in footer item)

The reader should be its own full-window page or a separate view that hides the nav pane, rather than an item in the menu.

## 7. File access and OS integration

- `FileOpenPicker` and `FolderPicker` need `WinRT.Interop.InitializeWithWindow` in WinUI 3. This trips up almost everyone.
- Persist folder access using `StorageApplicationPermissions.FutureAccessList` (packaged apps), or just store paths if you go unpackaged.
- Register **file type associations** (`.cbz`, `.cbr`, `.cb7`) in `Package.appxmanifest`. Handle file activation in `App.xaml.cs` using `AppInstance.GetCurrent().GetActivatedEventArgs()` so double-clicking a comic opens it in the reader.
- Consider drag-and-drop of files onto the window, and jump lists or recent files.

## 8. Architecture

- **MVVM** with **CommunityToolkit.Mvvm** (source-generated `[ObservableProperty]` and `[RelayCommand]`).
- **Dependency injection** via `Microsoft.Extensions.DependencyInjection` (or the Generic Host). Typical services: `IArchiveService`, `IComicRepository`, `IThumbnailService`, `IMetadataService`, `ISettingsService`, `INavigationService`, `ILibraryScanner`.
- Logging with Serilog or `Microsoft.Extensions.Logging`, writing to a file. You'll want it when users report corrupt-file bugs.
- Also check out **Template Studio for WinUI**. It scaffolds MVVM, DI, navigation and settings if you want to skip the boilerplate.

## 9. Settings and polish

- Theme (light, dark, system), Mica or Acrylic backdrop, and custom title bar.
- Reader defaults: mode, fit, direction, background color, animations, preload count.
- Cache management: size limit and a clear-cache button.
- Keyboard shortcut help, accessibility (AutomationProperties, high contrast), and localization (`.resw` files) if you plan to go international.
- A first-run experience: "Add your comics folder."

## 10. Quality, packaging and distribution

- **Tests:** unit-test the archive services, natural sort, filename parser and ComicInfo parser with a set of sample archives (including nasty ones: corrupt, nested folders, huge pages, odd encodings and unicode filenames).
- **Performance testing:** try a 2 GB archive and a 10,000-comic library.
- **Packaging:** MSIX for the Microsoft Store or sideloading. Unpackaged is simpler for development, but some APIs (like `LocalSettings` and `FutureAccessList`) behave differently, so decide early.

## Suggested build order

1. **Archive service plus a basic reader:** open a CBZ and CBR, show pages, and navigate. This validates the riskiest parts first.
2. **Reader features:** zoom, fit modes, RTL, double-page, and fullscreen.
3. **Library:** folder scan, SQLite, cover thumbnails, and the grid view.
4. **Progress tracking:** resume, read status, and continue reading.
5. **Metadata:** ComicInfo.xml, series grouping, and search.
6. **Polish:** settings, file associations, themes, and error handling.
7. **Extras:** collections, online metadata, OPDS server support, and cloud sync.

Pitfalls to watch: memory with large images, solid RAR performance, and the `InitializeWithWindow` picker issue. I can sketch out the archive service interface or the project folder structure next, if you tell me whether you plan to ship packaged (MSIX) or unpackaged.

# Project structure for ComicDeck

I'd split it into **three projects**. The rule is that anything that doesn't need WinUI goes in a plain .NET class library. That keeps the UI thin and lets you unit-test the hard parts (archives, parsing, database) without launching an app.

```
ComicDeck/                          (repo root)
├── ComicDeck.sln
├── README.md
├── LICENSE
├── .gitignore
├── TestData/                       (gitignored sample comics)
│
├── src/
│   ├── ComicDeck/                  WinUI 3 app (packaged)
│   ├── ComicDeck.Core/             class library, net8.0 (no UI references)
│   └── ComicDeck.Infrastructure/   optional, see note below
│
└── tests/
    └── ComicDeck.Core.Tests/       xUnit or MSTest
```

**Dependency direction:** `ComicDeck` → `ComicDeck.Core`, and `Tests` → `Core`. Core never references the app. If you add Infrastructure, it references Core, and the app references both.

Template Studio often generates a `.Core` project already. If yours did, use it. Otherwise add one via *Class Library* targeting the same .NET version as the app (without the `-windows` suffix in the target framework).

## ComicDeck.Core

Domain models, interfaces and the pure logic.

```
ComicDeck.Core/
├── Models/
│   ├── Comic.cs                    path, title, page count, cover path, etc.
│   ├── Series.cs
│   ├── ComicMetadata.cs            parsed ComicInfo.xml fields
│   ├── ReadingProgress.cs
│   └── ReadingDirection.cs         enum: LeftToRight, RightToLeft
│
├── Archives/
│   ├── IComicArchive.cs            list pages, open page stream, dispose
│   ├── ComicArchiveFactory.cs      picks implementation by magic bytes/extension
│   ├── ZipComicArchive.cs          CBZ
│   ├── SharpCompressComicArchive.cs  CBR, CB7, CBT
│   ├── ArchiveException.cs         corrupt, password-protected, unsupported
│   └── PageFilter.cs               image extensions, junk-file filtering
│
├── Metadata/
│   ├── IMetadataService.cs
│   ├── ComicInfoParser.cs          ComicInfo.xml
│   └── FileNameParser.cs           "Batman 012 (2016).cbz" fallback
│
├── Library/
│   ├── IComicRepository.cs
│   ├── ILibraryScanner.cs
│   └── LibraryScanner.cs
│
├── Utilities/
│   └── NaturalStringComparer.cs    page2 before page10
│
└── Contracts/                      small shared interfaces if you need them
```

## ComicDeck.Infrastructure (optional)

If you'd rather keep Core free of NuGet database packages, put the SQLite code here: `ComicDbContext` (or sqlite-net setup), `ComicRepository`, and migrations. For a first version it's fine to skip this project and put the repository in Core. You can split it out later without much pain.

## ComicDeck (the WinUI app)

This follows the layout Template Studio generates, plus a few folders of your own.

```
ComicDeck/
├── App.xaml / App.xaml.cs          DI container setup lives here
├── Package.appxmanifest            file associations (.cbz, .cbr, .cb7)
│
├── Activation/                     file-activation handler (open comic by double-click)
├── Contracts/Services/             INavigationService, IThemeSelectorService,
│                                   IPageService, IImageCacheService, etc.
├── Services/                       implementations of the above
│   ├── NavigationService.cs
│   ├── PageService.cs              maps ViewModel → Page
│   ├── ThemeSelectorService.cs
│   ├── LocalSettingsService.cs
│   ├── PageImageLoader.cs          archive stream → BitmapImage, with decode sizing
│   ├── PageCache.cs                LRU cache of decoded pages
│   └── ThumbnailService.cs
│
├── ViewModels/
│   ├── ShellViewModel.cs
│   ├── HomeViewModel.cs
│   ├── LibraryViewModel.cs
│   ├── SeriesViewModel.cs
│   ├── ReaderViewModel.cs
│   └── SettingsViewModel.cs
│
├── Views/
│   ├── ShellPage.xaml              the NavigationView frame
│   ├── HomePage.xaml
│   ├── LibraryPage.xaml
│   ├── SeriesPage.xaml
│   ├── ReaderPage.xaml
│   └── SettingsPage.xaml
│
├── Controls/                       reusable pieces
│   ├── ComicCoverCard.xaml
│   ├── PageViewer.xaml             your custom zoom/pan paging control
│   └── ReaderOverlay.xaml          slider, page counter, toolbar
│
├── Converters/                     value converters
├── Helpers/                        window handle helper for pickers, extensions
├── Styles/                         resource dictionaries (colors, text styles)
├── Strings/en-us/Resources.resw    localizable text
└── Assets/
```

## Why this split

- **Core vs. app boundary:** `BitmapImage`, `StorageFile` and anything else from `Microsoft.UI` or `Windows.*` stays in the app. Core deals in `Stream`, `string` and plain models. That means `IComicArchive.OpenPageAsync(int index)` returns a `Stream`, and the app converts it to a bitmap.
- **ViewModels never touch controls.** They call services through interfaces. This is what makes MVVM and testing worthwhile.
- **One ViewModel per page**, registered in DI along with its page. Template Studio's `PageService` handles the mapping.

## DI registration

All wiring goes in one place, `App.xaml.cs` (or a `Host` builder), roughly:

```csharp
services.AddSingleton<IComicArchiveFactory, ComicArchiveFactory>();
services.AddSingleton<IMetadataService, MetadataService>();
services.AddSingleton<IComicRepository, ComicRepository>();
services.AddSingleton<ILibraryScanner, LibraryScanner>();
services.AddSingleton<IThumbnailService, ThumbnailService>();

services.AddTransient<LibraryViewModel>();
services.AddTransient<ReaderViewModel>();
// ...and so on for each page
```

Archives themselves are **not** registered, because each open comic is a short-lived, disposable object created by the factory.

## Conventions worth settling now

- **Namespaces match folders** (`ComicDeck.Core.Archives`).
- **Async everywhere** for I/O, with `CancellationToken` parameters on archive and scanner methods. You'll need cancellation when the user flips pages quickly.
- **Nullable reference types on** in all projects.
- Put shared build settings (`LangVersion`, `Nullable`, analyzers) in a **`Directory.Build.props`** at the repo root so all projects stay consistent.

## What to do first

1. Create the solution layout above (move the app under `src/` if the wizard put it at the root).
2. Add `ComicDeck.Core` and the test project, and add the project reference from the app to Core.
3. Make sure it still builds and runs, then commit: `Set up solution structure`.
4. Add the **first real code**: `IComicArchive`, `ZipComicArchive`, `NaturalStringComparer`, and tests for them.

I'd start on step 4 next, beginning with the `IComicArchive` interface and the CBZ implementation. Want me to write those out?