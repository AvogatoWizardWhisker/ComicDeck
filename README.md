# ComicDeck

ComicDeck is a modern Windows comic book reader built with WinUI. It aims to provide a clean, fast, and native reading experience for comic archives while automatically extracting and organizing comic metadata.

> [!Note]
> **Work in Progress**
> 
> ComicDeck is currently under active development. Features, architecture, file formats, and user experience may change significantly between releases. Expect incomplete functionality, bugs, and breaking changes while development continues.

## Features

### Reading Experience
- Native WinUI interface
- Support for comic archive formats
- Fast page loading and navigation
- Archive-based comic browsing

### Metadata Support
- Reads metadata from `ComicInfo.xml`
- Automatic filename-based metadata detection
- Metadata merging from multiple sources
- Automatic page count detection

### Filename Parsing
ComicDeck can extract metadata from common comic naming conventions, including:

```text
Batman 001 (2016)
Saga Vol. 3
Amazing Spider-Man 025 - Hunted Part 1
Invincible 144 (of 144)
```

Detected information includes:

- Series name
- Issue number
- Issue title
- Volume number
- Publication year
- Issue count

### Reliability
- Gracefully handles missing metadata
- Falls back to filename parsing when `ComicInfo.xml` is unavailable
- Continues loading comics even if metadata files are corrupted

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
- Page information

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

1. ComicInfo.xml
2. Filename parser fallback
3. Archive-derived page count

---

## Project Structure

```text
ComicDeck.Core
├── Archives
├── Metadata
│   ├── FileNameParser
│   ├── ComicInfoParser
│   ├── MetadataService
│   └── IMetadataService
└── Models
```

---

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

Please note that the project is still evolving, so APIs and internal structures may change frequently.

---

## License

License information will be added once the project reaches a more mature stage.

---

**ComicDeck is an active work-in-progress project focused on building a fast, modern, and metadata-aware comic reading experience for Windows.**
