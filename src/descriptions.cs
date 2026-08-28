/**
 * @authors @katcw, @gintoki000 (for chinese simplified translations)
 * @file    descriptions.cs
 * @brief   json-backed store of item descriptions and type colours, with hot reload and embedded defaults.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PeakItemTooltip
{
    /**
     * @brief the authored type(s) and description for a single item.
     */
    public class ItemInfo
    {
        // an item may have one or more types; the json "type" field accepts either a single
        // string ("MYSTICAL") or an array (["MYSTICAL", "DEPLOYABLE"])
        [JsonProperty("type")]
        [JsonConverter(typeof(StringOrStringArrayConverter))]
        public List<string> Types = new List<string>();

        [JsonProperty("description")]
        public string Description = "";
    }

    /**
     * @brief reads a json value that is either a single string or an array of strings into a string list,
     *        so the "type" field can carry one or many types
     */
    public class StringOrStringArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(List<string>);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var list = new List<string>();
            if (reader.TokenType == JsonToken.Null) return list;

            if (reader.TokenType == JsonToken.StartArray)
            {
                foreach (JToken token in JArray.Load(reader))
                {
                    string s = token?.ToString();
                    if (!string.IsNullOrEmpty(s)) list.Add(s);
                }
            }
            else
            {
                string s = reader.Value?.ToString();
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }

            return list;
        }

        // write a lone type back as a plain string, multiple types as an array, to keep hand-edited files tidy
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var list = value as List<string>;
            if (list != null && list.Count == 1)
            {
                writer.WriteValue(list[0]);
                return;
            }

            writer.WriteStartArray();
            if (list != null) foreach (string s in list) writer.WriteValue(s);
            writer.WriteEndArray();
        }
    }

    /**
     * @brief the json schema of the descriptions file: the shared type colour map and the per-item entries.
     */
    public class DescriptionFile
    {
        // item type -> colour
        [JsonProperty("types")]
        public Dictionary<string, string> Types = new Dictionary<string, string>();
        // item key -> item info (type and desc)
        [JsonProperty("items")]
        public Dictionary<string, ItemInfo> Items = new Dictionary<string, ItemInfo>();
    }

    /**
     * @brief loads, hot-reloads, and serves item descriptions and type colours from the json file, with its embedded defaults
     */
    public class Descriptions
    {
        // filenames for the description json files that appear in bepinex/config
        public const string FileNameEn = "peak-item-tooltip.descriptions.json";
        public const string FileNameZh = "peak-item-tooltip.descriptions.zh-CN.json";

        private readonly string _path;
        private Dictionary<string, ItemInfo> _items = new Dictionary<string, ItemInfo>(StringComparer.OrdinalIgnoreCase);
        // raw type -> hex and its parsed colour for lookups
        private Dictionary<string, string> _types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Color> _typeColours = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastWriteUtc;

        /**
         * @brief picks the description filename for the current tooltip language (zh-CN vs default English)
         */
        public static string FileNameForCurrentLanguage()
        {
            return Zh.IsActive() ? FileNameZh : FileNameEn;
        }

        /**
         * @brief resolves the description file path inside the bepinex config folder
         */
        public Descriptions()
        {
            _path = Path.Combine(Paths.ConfigPath, FileNameForCurrentLanguage());
        }

        public string Path_ => _path;

        /**
         * @brief creates the description file from the embedded default if it does not exist;
         *        updates an existing description file with new entries (if there are), then load it
         *        one up with the entries added since it was written, then loads it
         */
        public void Initialize()
        {
            try
            {
                string defaults = LoadEmbeddedDefault();

                // if the file doesn't exist, write new description file with embedded default
                if (!File.Exists(_path))
                {
                    // if the embedded default description file cannot be read, then fallback to DefaultFileContents
                    File.WriteAllText(_path, defaults ?? DefaultFileContents);
                    Plugin.Log.LogInfo($"Created description file at {_path}");
                }
                // if the description file already exists
                else if (defaults != null)
                {
                    // read user's existing description file
                    JObject file = JObject.Parse(File.ReadAllText(_path));
                    // read the new embedded default description file
                    JObject baseline = JObject.Parse(defaults);
                    // accumulator
                    var added = new List<string>();

                    foreach (string section in new[] { "types", "items" })
                    {
                        var source = baseline[section] as JObject;
                        if (source == null) continue;

                        // user existing description file lookup
                        var target = file[section] as JObject;
                        // if a section does not exist, copy the new section over from baseline
                        if (target == null)
                        {
                            file[section] = source.DeepClone();
                            added.Add(section);
                            continue;
                        }

                        var present = new HashSet<string>(target.Properties().Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                        foreach (JProperty prop in source.Properties())
                        {
                            if (!present.Add(prop.Name)) continue;

                            target[prop.Name] = prop.Value.DeepClone();
                            added.Add(prop.Name);
                        }
                    }

                    // rewrite the description file if any change is detected
                    if (added.Count > 0)
                    {
                        File.WriteAllText(_path, file.ToString(Formatting.Indented));
                        Plugin.Log.LogInfo($"Added {added.Count} new entries to {Path.GetFileName(_path)}: {string.Join(", ", added)}");
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not create description file: {e.Message}");
            }

            Load();
        }

        /**
         * @brief reloads the file when it has changed on disk since the last load
         */
        public void MaybeReload()
        {
            try
            {
                if (!File.Exists(_path)) return;

                DateTime writeUtc = File.GetLastWriteTimeUtc(_path);
                if (writeUtc != _lastWriteUtc) Load();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Description reload check failed: {e.Message}");
            }
        }

        /**
         * @brief looks up an item's info by its key, returning false when the key is empty or missing
         * @param key the item key to look up
         * @param info the found item info, or null when not found
         * @return true if an entry was found
         */
        public bool TryGet(string key, out ItemInfo info)
        {
            if (!string.IsNullOrEmpty(key)) return _items.TryGetValue(key, out info);

            info = null;
            return false;
        }

        /**
         * @brief looks up a type's colour by name from the shared types map
         * @param typeName the type name to look up
         * @param colour the parsed colour, or default when not found
         * @return true if the type had a valid colour
         */
        public bool TryGetTypeColour(string typeName, out Color colour)
        {
            if (!string.IsNullOrEmpty(typeName)) return _typeColours.TryGetValue(typeName, out colour);

            colour = default;
            return false;
        }

        /**
         * @brief reads and parses the file into the item and type colour maps, keeping the previous data if parsing fails
         */
        private void Load()
        {
            try
            {
                string json = File.ReadAllText(_path);
                DescriptionFile parsed = JsonConvert.DeserializeObject<DescriptionFile>(json) ?? new DescriptionFile();

                var next = new Dictionary<string, ItemInfo>(StringComparer.OrdinalIgnoreCase);
                if (parsed.Items != null)
                {
                    foreach (var kvp in parsed.Items)
                    {
                        if (kvp.Value != null) next[kvp.Key] = kvp.Value;
                    }
                }

                // type -> hex map + a parsed colour cache; invalid hex is skipped
                var nextTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var nextTypeColours = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
                if (parsed.Types != null)
                {
                    foreach (var kvp in parsed.Types)
                    {
                        if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null) continue;
                        nextTypes[kvp.Key] = kvp.Value;
                        if (ColorUtility.TryParseHtmlString(kvp.Value, out Color c)) nextTypeColours[kvp.Key] = c;
                    }
                }

                _items = next;
                _types = nextTypes;
                _typeColours = nextTypeColours;
                _lastWriteUtc = File.GetLastWriteTimeUtc(_path);
                Plugin.Log.LogInfo($"Loaded {_items.Count} item description(s) and {_types.Count} type(s) from {Path.GetFileName(_path)}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Failed to parse {Path.GetFileName(_path)}: {e.Message} (keeping previous data)");
            }
        }

        /**
         * @brief returns the curated defaults baked into the dll, matched by filename suffix, or null if the resource is missing
         */
        private static string LoadEmbeddedDefault()
        {
            try
            {
                var asm = typeof(Descriptions).Assembly;
                string suffix = Zh.IsActive() ? "default-descriptions-zh-CN.json" : "default-descriptions.json";
                string name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;

                using (var stream = asm.GetManifestResourceStream(name))
                using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read embedded default descriptions: {e.Message}");
                return null;
            }
        }

        // fallback for if default-descriptions.json cannot be found or read
        private const string DefaultFileContents = @"{
          ""types"": {
        },
          ""items"": {
            ""EXAMPLE_KEY"": {
              ""type"": """",
              ""description"": """"
                }
            }
        }
        ";
    }
}
