// ============================================================================================================
//  Roster data for the launcher's top two tiers: character cards, map tiles and game mode tiles.
//
//  The lists live in launcher/roster.json. A copy is built into the exe (so the launcher works offline), and the
//  launcher also fetches the newest roster.json from the repo on every start, so adding a character to the JSON
//  shows up in everyone's launcher without them downloading a new exe.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BrokenCharactersLauncher
{
    /// <summary>One character card in the roster.</summary>
    public sealed class CharacterEntry
    {
        public string Name { get; init; } = "";
        public string Tagline { get; init; } = "";
        public DateTimeOffset Added { get; init; }
        public ImageSource? Portrait { get; init; }
        public Color AccentColor { get; init; } = Color.FromRgb(0xFF, 0xC9, 0x28);
        public Brush AccentBrush => new SolidColorBrush(AccentColor);
        public bool IsNew { get; set; }
        public Visibility NewBadgeVisibility => IsNew ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>One map or game mode thumbnail.</summary>
    public sealed class TileEntry
    {
        public string Title { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public string Badge { get; init; } = "";
        public ImageSource? Thumbnail { get; init; }
        public Visibility BadgeVisibility => string.IsNullOrWhiteSpace(Badge) ? Visibility.Collapsed : Visibility.Visible;
    }

    public sealed class RosterData
    {
        public List<CharacterEntry> Characters { get; } = new List<CharacterEntry>();
        public List<TileEntry> Maps { get; } = new List<TileEntry>();
        public List<TileEntry> Modes { get; } = new List<TileEntry>();

        /// <summary>
        /// Parses roster.json. <paramref name="imageBase"/> turns a relative image path ("Assets/Roster/X.png") into a
        /// full URI: a pack:// URI for the built-in copy, or a raw.githubusercontent.com URL for the downloaded one.
        /// </summary>
        public static RosterData Parse(string json, Func<string, Uri> imageBase)
        {
            var data = new RosterData();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("characters", out JsonElement chars))
                foreach (JsonElement c in chars.EnumerateArray())
                {
                    data.Characters.Add(new CharacterEntry
                    {
                        Name = Str(c, "name"),
                        Tagline = Str(c, "tagline"),
                        Added = DateTimeOffset.TryParse(Str(c, "added"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset d) ? d : DateTimeOffset.MinValue,
                        Portrait = Image(Str(c, "image"), imageBase),
                        AccentColor = ParseColor(Str(c, "accent")),
                        IsNew = c.TryGetProperty("new", out JsonElement n) && n.ValueKind == JsonValueKind.True
                    });
                }

            ReadTiles(root, "maps", data.Maps, imageBase);
            ReadTiles(root, "modes", data.Modes, imageBase);

            // Newest characters are pinned to the far left.
            data.Characters.Sort((a, b) => b.Added.CompareTo(a.Added));

            // NEW badge: the 3 most recent additions, if they were added in the last 14 days
            // (a character can also force it with "new": true in the JSON).
            DateTimeOffset cutoff = DateTimeOffset.Now.AddDays(-14);
            foreach (CharacterEntry c in data.Characters.Take(3))
                if (c.Added >= cutoff) c.IsNew = true;

            return data;
        }

        private static void ReadTiles(JsonElement root, string key, List<TileEntry> into, Func<string, Uri> imageBase)
        {
            if (!root.TryGetProperty(key, out JsonElement list)) return;
            foreach (JsonElement t in list.EnumerateArray())
                into.Add(new TileEntry
                {
                    Title = Str(t, "title"),
                    Subtitle = Str(t, "subtitle"),
                    Badge = Str(t, "badge"),
                    Thumbnail = Image(Str(t, "image"), imageBase)
                });
        }

        private static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        private static Color ParseColor(string hex)
        {
            try { if (!string.IsNullOrWhiteSpace(hex)) return (Color)ColorConverter.ConvertFromString(hex); }
            catch (FormatException) { }
            return Color.FromRgb(0xFF, 0xC9, 0x28);
        }

        /// <summary>Loads an image; a missing or broken image just leaves the card's dark background showing.</summary>
        private static ImageSource? Image(string path, Func<string, Uri> imageBase)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new Uri(path) : imageBase(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;   // read it once, don't keep the file/stream open
                bmp.DecodePixelWidth = 400;                   // cards are small; no need to keep full-size images
                bmp.EndInit();
                return bmp;
            }
            catch (Exception ex) when (ex is UriFormatException || ex is System.IO.IOException || ex is NotSupportedException)
            {
                return null;
            }
        }
    }
}
