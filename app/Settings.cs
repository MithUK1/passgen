using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Passgen
{
    /// <summary>
    /// Every option for one preset. The names and defaults match the browser
    /// extension. All fields are plain values so a preset can be saved to
    /// and read from the ini file by name.
    /// </summary>
    public class Settings
    {
        public string Mode = "passphrase";

        public int PwLength = 20;
        public bool PwLower = true;
        public bool PwUpper = true;
        public bool PwDigits = true;
        public bool PwSymbols = true;
        public string PwSymbolSet = "!@#$%^&*-_=+?.";
        public bool PackMusic;
        public bool PackChess;
        public bool PackEmoji;
        public bool AvoidAmbiguous;
        public int Group;

        public int SiteMinLength;
        public int SiteMaxLength;
        public int SiteMinDigits;
        public int SiteMaxDigits;
        public int SiteMinSymbols;
        public int SiteMaxSymbols;
        public string LetterCase = "any";
        public bool StartLetter;
        public int MaxRepeat;
        public bool NoSequences;
        public bool NoDates;
        public string Exclude = "";

        public int Words = 7;
        public string Separator = "-";
        public bool Capitalize = true;
        public bool AddNumber = true;
        public int NumberDigits = 2;
        public int WordMin = 6;
        public int WordMax = 6;
        public bool RandomLength;
        public string WordList = "common";
        public bool FilterProfanity = true;
        public string Blocked = "";

        public int PinLength = 6;
        public string PinType = "digits";

        public int HashBits = 256;
        public string HashFormat = "hex";

        public int Count = 1;
        public int ClearSeconds = 90;

        public static readonly string[] Modes = { "password", "passphrase", "pin", "hash" };
        public static readonly string[] LetterCases = { "any", "both", "upper", "lower" };
        public static readonly string[] Separators = { "-", ".", "_", " ", "", "random" };
        public static readonly string[] WordLists = { "common", "big", "eff", "custom" };
        public static readonly string[] PinTypes = { "digits", "alnum" };
                public static readonly int[] HashSizes = { 128, 160, 256, 384, 512 };
        public static readonly string[] HashFormats = { "hex", "base64", "base64url" };

        private static readonly FieldInfo[] Fields = typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance);

        // A copy, so editing one preset never changes another.
        public Settings Clone()
        {
            var copy = new Settings();
            foreach (FieldInfo field in Fields) field.SetValue(copy, field.GetValue(this));
            return copy;
        }

        // Every option as text, keyed by field name.
        public Dictionary<string, string> ToMap()
        {
            var map = new Dictionary<string, string>();
            foreach (FieldInfo field in Fields)
            {
                object value = field.GetValue(this);
                map[field.Name] = value is bool ? ((bool)value ? "1" : "0") : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            return map;
        }

        // Reads options from text values. Anything missing or invalid keeps
        // its default, so a damaged file can never break start-up.
        public static Settings FromMap(IDictionary<string, string> map)
        {
            var settings = new Settings();
            foreach (FieldInfo field in Fields)
            {
                string text;
                if (map == null || !map.TryGetValue(field.Name, out text) || text == null) continue;
                if (field.FieldType == typeof(bool))
                {
                    if (text == "1" || text == "true" || text == "True") field.SetValue(settings, true);
                    else if (text == "0" || text == "false" || text == "False") field.SetValue(settings, false);
                }
                else if (field.FieldType == typeof(int))
                {
                    int number;
                    if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number)) field.SetValue(settings, number);
                }
                else field.SetValue(settings, text);
            }
            settings.Sanitize();
            return settings;
        }

        // A stable text form of every option, used to remember work that
        // depends on the options.
        public string Key()
        {
            return string.Join("|", ToMap().Select(pair => pair.Key + "=" + pair.Value));
        }

        // Puts every value back into its allowed range or list.
        public void Sanitize()
        {
            if (!Modes.Contains(Mode)) Mode = "passphrase";
            if (!LetterCases.Contains(LetterCase)) LetterCase = "any";
            if (!Separators.Contains(Separator)) Separator = "-";
            if (!WordLists.Contains(WordList)) WordList = "common";
            if (!PinTypes.Contains(PinType)) PinType = "digits";
            if (!HashSizes.Contains(HashBits)) HashBits = 256;
            if (!HashFormats.Contains(HashFormat)) HashFormat = "hex";
            PwSymbolSet = Cleaned(PwSymbolSet, 60);
            Exclude = Cleaned(Exclude, 60);
            Blocked = Cleaned(Blocked, 300);
            PwLength = Math.Min(128, Math.Max(4, PwLength));
            Group = new[] { 0, 3, 4, 5, 6 }.Contains(Group) ? Group : 0;
            SiteMinLength = Math.Min(128, Math.Max(0, SiteMinLength));
            SiteMaxLength = Math.Min(128, Math.Max(0, SiteMaxLength));
            SiteMinDigits = Math.Min(10, Math.Max(0, SiteMinDigits));
            SiteMaxDigits = Math.Min(10, Math.Max(0, SiteMaxDigits));
            SiteMinSymbols = Math.Min(20, Math.Max(0, SiteMinSymbols));
            SiteMaxSymbols = Math.Min(20, Math.Max(0, SiteMaxSymbols));
            MaxRepeat = Math.Min(10, Math.Max(0, MaxRepeat));
            Words = Math.Min(12, Math.Max(3, Words));
            NumberDigits = NumberDigits == 0 ? 0 : Math.Min(10, Math.Max(2, NumberDigits));
            WordMin = WordMin == 0 ? 0 : Math.Min(10, Math.Max(3, WordMin));
            WordMax = WordMax == 0 ? 0 : Math.Min(10, Math.Max(3, WordMax));
            PinLength = Math.Min(16, Math.Max(4, PinLength));
            Count = Math.Min(100, Math.Max(1, Count));
            ClearSeconds = Math.Min(3600, Math.Max(0, ClearSeconds));
        }

        // Single line, trimmed to a length.
        private static string Cleaned(string text, int max)
        {
            text = (text ?? "").Replace("\r", "").Replace("\n", "");
            return text.Length > max ? text.Substring(0, max) : text;
        }
    }

    /// <summary>A named set of options.</summary>
    public class Preset
    {
        public string Id;
        public string Name;
        public Settings Settings;
    }

    /// <summary>
    /// A small ini reader and writer: sections of key=value lines. Values
    /// are kept exactly as written, so a space separator survives.
    /// </summary>
    public class IniFile
    {
        public readonly List<KeyValuePair<string, Dictionary<string, string>>> Sections = new List<KeyValuePair<string, Dictionary<string, string>>>();

        // Reads ini text. Lines before the first section are ignored.
        public static IniFile Parse(string text)
        {
            var ini = new IniFile();
            Dictionary<string, string> current = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == ';') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    current = new Dictionary<string, string>();
                    ini.Sections.Add(new KeyValuePair<string, Dictionary<string, string>>(line.Substring(1, line.Length - 2), current));
                    continue;
                }
                int equals = line.IndexOf('=');
                if (current == null || equals <= 0) continue;
                current[line.Substring(0, equals)] = line.Substring(equals + 1);
            }
            return ini;
        }

        // The first section with this name, or null.
        public Dictionary<string, string> Section(string name)
        {
            foreach (var pair in Sections) if (pair.Key == name) return pair.Value;
            return null;
        }

        // Ini text for every section, in order.
        public string Render()
        {
            var text = new StringBuilder();
            foreach (var section in Sections)
            {
                text.Append('[').Append(section.Key).Append("]\r\n");
                foreach (var pair in section.Value) text.Append(pair.Key).Append('=').Append((pair.Value ?? "").Replace("\r", "").Replace("\n", "")).Append("\r\n");
                text.Append("\r\n");
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// Everything that is kept between runs: presets, which one is active,
    /// the install stamp and window position. One ini file, saved through a
    /// temporary file so a failed write never leaves half a file.
    /// </summary>
    public static class Store
    {
        public const string AppVersion = "1.0.0";
        public const int MaxPresets = 20;
        public const string HelpUrl = "https://github.com/MithUK1/passgen#readme";
        private const string PresetPrefix = "Preset:";

        public static readonly List<Preset> Presets = new List<Preset>();
        public static string ActiveId;
        public static string InstalledVersion = "";
        public static string PendingNotice = "";
        public static string CustomListPath = "";
        public static string LastFolder = "";
        public static int WindowX = int.MinValue;
        public static int WindowY = int.MinValue;
        public static bool MinimizeToTray = true;
        public static bool CloseToTray = true;
        public static bool StartWithWindows = true;
        public static bool HadFile;
        public static bool Removed { get; private set; }

        public static string IniPath => Path.Combine(Installer.AppDir, "Passgen.ini");

        // The preset the main window works on.
        public static Preset Active
        {
            get
            {
                Preset found = Presets.FirstOrDefault(p => p.Id == ActiveId);
                if (found == null)
                {
                    found = Presets[0];
                    ActiveId = found.Id;
                }
                return found;
            }
        }

        // Loads the ini file. A file that cannot be read is kept as .bad and
        // the app starts with defaults.
        public static void Load()
        {
            Presets.Clear();
            HadFile = File.Exists(IniPath);
            if (HadFile)
            {
                try
                {
                    ReadFrom(File.ReadAllText(IniPath, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    AppLog.Write("Settings file could not be read: " + ex.Message);
                    try { File.Copy(IniPath, IniPath + ".bad", true); } catch (IOException) { }
                    Presets.Clear();
                    PendingNotice = "Your earlier settings couldn't be read, so Passgen started with defaults. The old file was kept as Passgen.ini.bad.";
                }
            }
            if (Presets.Count == 0) AddDefaultPreset();
            if (!Presets.Any(p => p.Id == ActiveId)) ActiveId = Presets[0].Id;
        }

        // Fills the store from ini text.
        private static void ReadFrom(string text)
        {
            IniFile ini = IniFile.Parse(text);
            Dictionary<string, string> app = ini.Section("App");
            if (app != null)
            {
                app.TryGetValue("Active", out ActiveId);
                InstalledVersion = Get(app, "InstalledVersion");
                PendingNotice = Get(app, "PendingNotice");
                CustomListPath = Get(app, "CustomList");
                LastFolder = Get(app, "LastFolder");
                MinimizeToTray = Get(app, "MinimizeToTray") != "0";
                CloseToTray = Get(app, "CloseToTray") != "0";
                StartWithWindows = Get(app, "StartWithWindows") != "0";
                int x, y;
                if (int.TryParse(Get(app, "WindowX"), out x) && int.TryParse(Get(app, "WindowY"), out y)) { WindowX = x; WindowY = y; }
            }
            foreach (var section in ini.Sections)
            {
                if (!section.Key.StartsWith(PresetPrefix) || Presets.Count >= MaxPresets) continue;
                string name = Get(section.Value, "PresetName").Trim();
                if (name.Length == 0) continue;
                Presets.Add(new Preset { Id = section.Key.Substring(PresetPrefix.Length), Name = name.Length > 30 ? name.Substring(0, 30) : name, Settings = Settings.FromMap(section.Value) });
            }
        }

        private static string Get(Dictionary<string, string> map, string key)
        {
            string value;
            return map.TryGetValue(key, out value) && value != null ? value : "";
        }

        // One preset called Default, with every option at its default.
        private static void AddDefaultPreset()
        {
            var preset = new Preset { Id = NewId(), Name = "Default", Settings = new Settings() };
            Presets.Add(preset);
            ActiveId = preset.Id;
        }

        public static string NewId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        // Writes the ini file through a temporary file that is read back
        // before it replaces the real one.
        public static void Save()
        {
            if (Removed) return;
            try
            {
                Directory.CreateDirectory(Installer.AppDir);
                IniFile ini = new IniFile();
                var app = new Dictionary<string, string>
                {
                    { "Active", ActiveId ?? "" },
                    { "InstalledVersion", InstalledVersion },
                    { "PendingNotice", PendingNotice },
                    { "CustomList", CustomListPath },
                    { "LastFolder", LastFolder },
                    { "MinimizeToTray", MinimizeToTray ? "1" : "0" },
                    { "CloseToTray", CloseToTray ? "1" : "0" },
                    { "StartWithWindows", StartWithWindows ? "1" : "0" }
                };
                if (WindowX != int.MinValue) { app["WindowX"] = WindowX.ToString(); app["WindowY"] = WindowY.ToString(); }
                ini.Sections.Add(new KeyValuePair<string, Dictionary<string, string>>("App", app));
                foreach (Preset preset in Presets)
                {
                    Dictionary<string, string> map = preset.Settings.ToMap();
                    var section = new Dictionary<string, string> { { "PresetName", preset.Name } };
                    foreach (var pair in map) section[pair.Key] = pair.Value;
                    ini.Sections.Add(new KeyValuePair<string, Dictionary<string, string>>(PresetPrefix + preset.Id, section));
                }
                string text = ini.Render();
                string temp = IniPath + ".tmp";
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (IniFile.Parse(File.ReadAllText(temp, Encoding.UTF8)).Sections.Count != ini.Sections.Count) throw new IOException("The saved copy did not read back correctly.");
                if (File.Exists(IniPath)) File.Delete(IniPath);
                File.Move(temp, IniPath);
            }
            catch (Exception ex)
            {
                AppLog.Write("Settings could not be saved: " + ex.Message);
            }
        }

        // Removes the ini file and log, for the full reset and uninstall. Nothing
        // is saved again afterwards, so the window closing can't bring it back.
        public static void DeleteAll()
        {
            Removed = true;
            foreach (string file in new[] { IniPath, IniPath + ".bad", IniPath + ".tmp", AppLog.LogPath })
            {
                try { if (File.Exists(file)) File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        // Puts every preset back to one default preset.
        public static void RestoreDefaults()
        {
            Presets.Clear();
            AddDefaultPreset();
            Save();
        }

        public static string TakePendingNotice()
        {
            string text = PendingNotice;
            if (text.Length == 0) return "";
            PendingNotice = "";
            Save();
            return text;
        }

        // Backup as JSON: the presets and which one is active. Nothing else.
        public static string ExportJson()
        {
            var list = new List<object>();
            foreach (Preset preset in Presets)
            {
                var settings = new Dictionary<string, object>();
                foreach (var pair in preset.Settings.ToMap()) settings[pair.Key] = pair.Value;
                list.Add(new Dictionary<string, object> { { "id", preset.Id }, { "name", preset.Name }, { "settings", settings } });
            }
            return MiniJson.Write(new Dictionary<string, object> { { "active", ActiveId }, { "presets", list } });
        }

        // Replaces the presets from a backup. Throws with a plain message
        // when the file is not a usable backup, and changes nothing then.
        public static void ImportJson(string json)
        {
            object root;
            try { root = MiniJson.Parse(json); }
            catch (FormatException) { throw new FormatException("That file is not a Passgen backup."); }
            var top = root as Dictionary<string, object>;
            var given = top != null && top.ContainsKey("presets") ? top["presets"] as List<object> : null;
            if (given == null) throw new FormatException("That file is not a Passgen backup.");
            var loaded = new List<Preset>();
            var seen = new HashSet<string>();
            string active = top.ContainsKey("active") ? top["active"] as string : null;
            string activeId = null;
            foreach (object item in given.Take(MaxPresets))
            {
                var entry = item as Dictionary<string, object>;
                string name = entry != null && entry.ContainsKey("name") ? (entry["name"] as string ?? "").Trim() : "";
                var values = entry != null && entry.ContainsKey("settings") ? entry["settings"] as Dictionary<string, object> : null;
                if (name.Length == 0 || values == null) continue;
                string id = entry.ContainsKey("id") ? entry["id"] as string : null;
                string unique = !string.IsNullOrEmpty(id) && seen.Add(id) ? id : NewId();
                if (id != null && id == active) activeId = unique;
                var map = new Dictionary<string, string>();
                foreach (var pair in values) map[pair.Key] = pair.Value == null ? null : Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture);
                loaded.Add(new Preset { Id = unique, Name = name.Length > 30 ? name.Substring(0, 30) : name, Settings = Settings.FromMap(map) });
            }
            if (loaded.Count == 0) throw new FormatException("That file has no presets in it.");
            Presets.Clear();
            Presets.AddRange(loaded);
            ActiveId = activeId ?? loaded[0].Id;
            Save();
        }
    }

    /// <summary>The log: one line per event, trimmed so it never grows.</summary>
    public static class AppLog
    {
        private const int MaxLines = 500;
        public static string LogPath => Path.Combine(Installer.AppDir, "Passgen.log");

        // Adds a line. Logging must never cause a failure of its own.
        public static void Write(string message)
        {
            if (Store.Removed) return;
            try
            {
                Directory.CreateDirectory(Installer.AppDir);
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message.Replace("\r", " ").Replace("\n", " ") + "\r\n");
            }
            catch (Exception) { }
        }

        // Keeps only the newest lines.
        public static void Trim()
        {
            try
            {
                if (!File.Exists(LogPath)) return;
                string[] lines = File.ReadAllLines(LogPath);
                if (lines.Length > MaxLines + 100) File.WriteAllLines(LogPath, lines.Skip(lines.Length - MaxLines).ToArray());
            }
            catch (Exception) { }
        }
    }

    /// <summary>Just enough JSON for backup files: objects, lists, text, numbers, true, false and null.</summary>
    public static class MiniJson
    {
        // JSON text for nested dictionaries, lists, strings and bools.
        public static string Write(object value)
        {
            var text = new StringBuilder();
            WriteValue(text, value, 0);
            return text.ToString();
        }

        private static void WriteValue(StringBuilder text, object value, int depth)
        {
            string indent = new string(' ', depth * 2);
            var dictionary = value as Dictionary<string, object>;
            var list = value as List<object>;
            if (dictionary != null)
            {
                text.Append("{\n");
                int i = 0;
                foreach (var pair in dictionary)
                {
                    text.Append(indent).Append("  ");
                    WriteString(text, pair.Key);
                    text.Append(": ");
                    WriteValue(text, pair.Value, depth + 1);
                    text.Append(++i < dictionary.Count ? ",\n" : "\n");
                }
                text.Append(indent).Append('}');
            }
            else if (list != null)
            {
                text.Append("[\n");
                for (int i = 0; i < list.Count; i++)
                {
                    text.Append(indent).Append("  ");
                    WriteValue(text, list[i], depth + 1);
                    text.Append(i + 1 < list.Count ? ",\n" : "\n");
                }
                text.Append(indent).Append(']');
            }
            else if (value is bool) text.Append((bool)value ? "true" : "false");
            else if (value == null) text.Append("null");
            else WriteString(text, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder text, string value)
        {
            text.Append('"');
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (ch < 32) text.Append("\\u").Append(((int)ch).ToString("x4"));
                        else text.Append(ch);
                        break;
                }
            }
            text.Append('"');
        }

        // Parses JSON text. Throws FormatException when it is not valid.
        public static object Parse(string json)
        {
            int pos = 0;
            object value = ReadValue(json, ref pos);
            SkipSpace(json, ref pos);
            if (pos != json.Length) throw new FormatException();
            return value;
        }

        private static void SkipSpace(string json, ref int pos)
        {
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
        }

        private static object ReadValue(string json, ref int pos)
        {
            SkipSpace(json, ref pos);
            if (pos >= json.Length) throw new FormatException();
            char ch = json[pos];
            if (ch == '{')
            {
                pos++;
                var dictionary = new Dictionary<string, object>();
                SkipSpace(json, ref pos);
                if (pos < json.Length && json[pos] == '}') { pos++; return dictionary; }
                while (true)
                {
                    SkipSpace(json, ref pos);
                    string key = ReadString(json, ref pos);
                    SkipSpace(json, ref pos);
                    if (pos >= json.Length || json[pos++] != ':') throw new FormatException();
                    dictionary[key] = ReadValue(json, ref pos);
                    SkipSpace(json, ref pos);
                    if (pos >= json.Length) throw new FormatException();
                    char next = json[pos++];
                    if (next == '}') return dictionary;
                    if (next != ',') throw new FormatException();
                }
            }
            if (ch == '[')
            {
                pos++;
                var list = new List<object>();
                SkipSpace(json, ref pos);
                if (pos < json.Length && json[pos] == ']') { pos++; return list; }
                while (true)
                {
                    list.Add(ReadValue(json, ref pos));
                    SkipSpace(json, ref pos);
                    if (pos >= json.Length) throw new FormatException();
                    char next = json[pos++];
                    if (next == ']') return list;
                    if (next != ',') throw new FormatException();
                }
            }
            if (ch == '"') return ReadString(json, ref pos);
            return ReadLiteral(json, ref pos);
        }

        private static string ReadString(string json, ref int pos)
        {
            if (pos >= json.Length || json[pos] != '"') throw new FormatException();
            pos++;
            var text = new StringBuilder();
            while (pos < json.Length)
            {
                char ch = json[pos++];
                if (ch == '"') return text.ToString();
                if (ch != '\\') { text.Append(ch); continue; }
                if (pos >= json.Length) break;
                char escape = json[pos++];
                switch (escape)
                {
                    case 'n': text.Append('\n'); break;
                    case 'r': text.Append('\r'); break;
                    case 't': text.Append('\t'); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'u':
                        int code;
                        if (pos + 4 > json.Length || !int.TryParse(json.Substring(pos, 4), System.Globalization.NumberStyles.HexNumber, null, out code)) throw new FormatException();
                        text.Append((char)code);
                        pos += 4;
                        break;
                    default: text.Append(escape); break;
                }
            }
            throw new FormatException();
        }

        // true, false, null or a number. Numbers come back as text, as every option is stored as text.
        private static object ReadLiteral(string json, ref int pos)
        {
            int start = pos;
            while (pos < json.Length && ",]} \t\r\n".IndexOf(json[pos]) < 0) pos++;
            string word = json.Substring(start, pos - start);
            if (word == "true") return "1";
            if (word == "false") return "0";
            if (word == "null") return null;
            double number;
            if (word.Length > 0 && double.TryParse(word, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number)) return word;
            throw new FormatException();
        }
    }
}
