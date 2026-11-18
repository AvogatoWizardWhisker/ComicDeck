# ComicDeck

ComicDeck is a modern Windows comic book reader built with WinUI. It is designed to give users a clean, fast, and native reading experience for comic archives while organizing their library, tracking reading progress, and extracting metadata automatically.

> [!Note]
> **Work in Progress**
>
> ComicDeck is currently under active development. Features, architecture, file formats, and user experience may change significantly between releases. Expect incomplete functionality, bugs, and breaking changes while development continues.

## Core Features

### Reading Experience

- Native WinUI interface for Windows
- Reads comic archive files directly from disk
- Fast page navigation and layout handling
- Support for single-page and double-page reading modes
- Fit modes for page sizing and zooming
- Right-to-left reading support for applicable titles

### Library Management

- Scan folders for comic archives
- Track comics in a local SQLite database
- Keep reading progress, favorites, and completion state
- Search, filter, and sort comics in the library
- Support for adding and rescanning library folders

### Metadata Support

- Reads metadata from `ComicInfo.xml`
- Falls back to filename-based detection when metadata is missing
- Merges metadata from multiple sources
- Automatically detects page counts from archive contents
- Handles missing or corrupted metadata gracefully

### Filename Parsing

ComicDeck can extract metadata from common comic naming conventions, including:

```text
Batman 001 (2016)
Saga Vol. 3
Amazing Spider-Man 025 - Hunted Part 1
Invincible 144 (of 144)
```

### Detected information may include

- Series name
- Issue number
- Issue title
- Volume number
- Publication year
- Total issue count

### Supported File Formats

ComicDeck supports the following comic archive formats:

- ZIP (.cbz)
- RAR (.cbr)
- 7z (.cb7)
- TAR (.cbt)

The archive type is detected automatically, and the app reads pages from supported comic files.

---

## Supported Metadata Sources

### ComicInfo.xml

When available, ComicDeck reads metadata from:

```text
ComicInfo.xml
```

Common fields include:

- Title
- Series
- Number
- Volume
- Year
- Page count
- Publisher and other metadata values

### Filename Metadata

If embedded metadata is missing or incomplete, ComicDeck automatically extracts information from the comic filename.

Example:

```text
Saga 054 (2018) (Digital) (Zone-Empire).cbz
```

Produces:

```text
Series: Saga
Number: 54
Year: 2018
```

---

## Architecture

Current metadata flow:

```text
Archive
 ├── ComicInfo.xml
 └── Filename

        │
        ▼

 MetadataService
        │
        ▼

 ComicMetadata
```

Metadata priority:

1. `ComicInfo.xml`
2. Filename parser fallback
3. Archive-derived page count

---

## Project Structure

```text
ComicDeck/
├── Assets
├── Helpers
├── Pages
├── Services
└── ViewModels

ComicDeck.Core/
├── Archives
├── Data
├── Library
├── Metadata
├── Models
└── Utilities

ComicDeck.Core.Tests/

```

---

## Getting started

You first need to install an IDE (Integrated Development Environment). Preferably Visual Studio 2026 or Visual Studio Code. You can download them [in the following link](https://visualstudio.microsoft.com/downloads/). You also need to download and install the latest version of [.NET SDK](https://dotnet.microsoft.com/en-us/download), as well as [git](https://git-scm.com/downloads).

To run the develop this project, follow these steps

- Clone the repository to your prefered directory: `git clone https://github.com/AvogatoWizardWhisker/ComicDeck.git` then open it with `cd ComicDeck`.
- Build and run the project with the dotnet command `dotnet build && dotnet run`.

## Ressources

This project has been built with:

- [SharpCompress](https://github.com/adamhathcock/sharpcompress)
- [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net)
- [Communitytoolkit.MVVM](https://github.com/CommunityToolkit/dotnet)

## Development Status

Current areas of focus:

- Archive handling improvements
- Metadata extraction and validation
- Reader UI and navigation
- Library management
- Performance optimization
- Automated testing

Planned features may include:

- Library organization
- Search and filtering
- Reading progress tracking
- Thumbnail generation
- Additional metadata providers
- Enhanced reader customization

---

## Contributing

Contributions, bug reports, and feature suggestions are welcome.

Please note that the project is still evolving, so it is subject to change.

---

## License

This project is licensed under the [MIT license](https://github.com/AvogatoWizardWhisker/ComicDeck?tab=MIT-1-ov-file).

---

**ComicDeck is an active work-in-progress project focused on building a fast, modern, and metadata-aware comic reading experience for Windows.**
