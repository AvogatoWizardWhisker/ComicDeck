using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using ComicDeck.Core.Models;
using System.Runtime.InteropServices.Marshalling;

namespace ComicDeck.Core.Metadata

/// <summary>Parses ComicRack-style ComicInfo.xml. Returns null if XML is unusuable.</summary>
{
    public static class ComicInfoParser
    {
        private static readonly XmlReaderSettings Settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit, // blocks XXE and entity expansion attacks
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            XmlResolver = null,
            MaxCharactersInDocument = 5_000_000,    // 5 MB
            CheckCharacters = false,                // be lenient with sloppy exporters
        };

        public static ComicMetadata? Parse(Stream stream)
        {
            using var reader = XmlReader.Create(stream, Settings);
            return Load(reader);
        }

        public static ComicMetadata? Parse(string xml)
        {
            using var reader = XmlReader.Create(new StringReader(xml), Settings);
            return Load(reader);
        }

        private static ComicMetadata? Load(XmlReader reader)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(reader);
            }
            catch (XmlException)
            {
                return null;
            }

            var root = doc.Root;
            if (root is null || !root.Name.LocalName.Equals("ComicInfo", StringComparison.OrdinalIgnoreCase))
                return null;

            return new ComicMetadata
            {
                Title = Text(root, "Title"),
                Series = Text(root, "Series"),
                Number = Text(root, "Number"),
                Count = PositiveInt(Text(root, "Count")),
                Volume = PositiveInt(Text(root, "Volume")),
                Summary = Text(root, "Summary"),

                Year = PositiveInt(Text(root, "Year")),
                Month = InRange(PositiveInt(Text(root, "Month")), 1, 12),
                Day = InRange(PositiveInt(Text(root, "Day")), 1, 31),

                Writer = Text(root, "Writer"),
                Penciller = Text(root, "Penciller"),
                Inker = Text(root, "Inker"),
                Colorist = Text(root, "Colorist"),
                Letterer = Text(root, "Letterer"),
                CoverArtist = Text(root, "CoverArtist"),
                Editor = Text(root, "Editor"),

                Publisher = Text(root, "Publisher"),
                Imprint = Text(root, "Imprint"),
                Genre = Text(root, "Genre"),
                Tags = Text(root, "Tags"),
                LanguageIsoCode = Text(root, "LanguageISOCode"),
                AgeRating = Text(root, "AgeRating"),
                Web = Text(root, "Web"),

                PageCount = PositiveInt(Text(root, "PageCount")),
                ReadingDirection = ParseDirection(Text(root, "ReadingDirection")),
            };
        }
        // element names are matched case-insensitively because exporters are inconsistent about capitalization
        private static string? Text(XElement root, string name)
        {
            var element = root.Elements()
                .FirstOrDefault(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));

            var value = element?.Value.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static int? PositiveInt(string? text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;

        private static int? InRange(int? value, int min, int max) => value is >= 0 && value >= min && value <= max ? value : null;

        private static ReadingDirection? ParseDirection(string? manga) => manga?.Trim().ToLowerInvariant() switch
        {
            "yesandrighttoleft" => ReadingDirection.RightToLeft,
            "no" => ReadingDirection.LeftToRight,
            _ => null,
        };
    }
}
