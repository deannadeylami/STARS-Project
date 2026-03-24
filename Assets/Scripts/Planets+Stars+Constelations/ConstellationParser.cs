using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads constellations from StreamingAssets in a whitespace format:
///   Aql "Aquila" 8  98036 97649 97649 97278 ...
/// Or (legacy/no-name):
///   Aql 8 98036 97649 ...
///
/// The HIP list after the count is interpreted as pairs (hip1 hip2), each pair is a line segment.
/// Keys are stored case-insensitive via ToUpperInvariant().
/// </summary>
public static class ConstellationCatalog
{
    public struct Segment
    {
        public int hip1;
        public int hip2;
    }

    public class Constellation
    {
        public string Abbrev;        // original casing from file (for display if needed)
        public string AbbrevKey;     // normalized key: upper-invariant
        public string Name;          // may be empty if file lacked it
        public List<Segment> Segments = new List<Segment>(32);

        // Useful for label centroid calculations
        public HashSet<int> UniqueHipIds = new HashSet<int>();
    }

    public class Catalog
    {
        public readonly Dictionary<string, Constellation> ByAbbrevKey = new Dictionary<string, Constellation>(88);
        public readonly List<Constellation> All = new List<Constellation>(88);
    }

    // Simple cache so 2D exporter + 3D viewer don’t re-parse every time.
    private static readonly Dictionary<string, Catalog> _cache = new Dictionary<string, Catalog>();

    public static Catalog LoadFromStreamingAssets(string fileName, bool forceReload = false)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Constellation fileName is null/empty.");

        string key = fileName.Trim().ToLowerInvariant();
        if (!forceReload && _cache.TryGetValue(key, out var cached))
            return cached;

        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[ConstellationCatalog] File not found: {path}");
            return new Catalog();
        }

        var catalog = new Catalog();

        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            string line = raw.Trim();
            if (line.StartsWith("#")) continue;

            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash).Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Tokenize with quote support (so "Ursa Major" becomes one token)
            List<string> tokens = TokenizeWhitespaceWithQuotes(line);
            if (tokens.Count < 3) continue;

            // tokens[0] = abbrev
            string abbrev = tokens[0].Trim();
            if (string.IsNullOrWhiteSpace(abbrev)) continue;

            string abbrevKey = NormalizeKey(abbrev);

            int index = 1;
            string name = "";

            // If tokens[1] is quoted name, accept it.
            // Our tokenizer strips quotes, so we detect name by whether token[1] can parse as int (count).
            // If it cannot parse as int => treat as name.
            if (!int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                name = tokens[index].Trim();
                index++;
            }

            // Now tokens[index] should be the count (N). We don’t strictly rely on it.
            if (index >= tokens.Count) continue;

            // Try parse count but ignore value
            int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            index++;

            // Remaining tokens are HIP ids (as ints), interpreted in pairs
            if (index >= tokens.Count) continue;

            var ids = new List<int>(tokens.Count - index);
            for (int t = index; t < tokens.Count; t++)
            {
                if (int.TryParse(tokens[t], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hip) && hip > 0)
                    ids.Add(hip);
            }

            if (ids.Count < 2) continue;

            if (!catalog.ByAbbrevKey.TryGetValue(abbrevKey, out var con))
            {
                con = new Constellation
                {
                    Abbrev = abbrev,
                    AbbrevKey = abbrevKey,
                    Name = name
                };
                catalog.ByAbbrevKey[abbrevKey] = con;
                catalog.All.Add(con);
            }
            else
            {
                // If we previously loaded without a name, allow the name to fill in.
                if (string.IsNullOrWhiteSpace(con.Name) && !string.IsNullOrWhiteSpace(name))
                    con.Name = name;
            }

            // Build segments: (ids[0],ids[1]), (ids[2],ids[3])...
            for (int k = 0; k + 1 < ids.Count; k += 2)
            {
                int a = ids[k];
                int b = ids[k + 1];
                if (a <= 0 || b <= 0) continue;

                con.Segments.Add(new Segment { hip1 = a, hip2 = b });
                con.UniqueHipIds.Add(a);
                con.UniqueHipIds.Add(b);
            }
        }

        _cache[key] = catalog;
        Debug.Log($"[ConstellationCatalog] Loaded {catalog.All.Count} constellations from {fileName}");
        return catalog;
    }

    private static string NormalizeKey(string abbrev) => abbrev.Trim().ToUpperInvariant();

    /// <summary>
    /// Splits by whitespace but preserves quoted substrings as one token.
    /// Example: Aql "Ursa Major" 8  123 456  -> tokens: [Aql, Ursa Major, 8, 123, 456]
    /// </summary>
    private static List<string> TokenizeWhitespaceWithQuotes(string line)
    {
        var tokens = new List<string>(64);
        bool inQuotes = false;
        var cur = new System.Text.StringBuilder(32);

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue; // drop quote char
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (cur.Length > 0)
                {
                    tokens.Add(cur.ToString());
                    cur.Clear();
                }
                continue;
            }

            cur.Append(c);
        }

        if (cur.Length > 0)
            tokens.Add(cur.ToString());

        return tokens;
    }
}