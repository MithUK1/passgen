using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Passgen
{
    /// <summary>
    /// The word lists. They are built into the exe, so nothing is read from
    /// or downloaded to anywhere else. The filtered list is remembered until
    /// the options that shape it change.
    /// </summary>
    public static class Words
    {
        private static readonly Dictionary<string, List<string>> Loaded = new Dictionary<string, List<string>>();
        private static string _lastKey = "";
        private static List<string> _lastWords;

        // A built-in list by resource name, read once.
        private static List<string> Resource(string name)
        {
            List<string> list;
            if (Loaded.TryGetValue(name, out list)) return list;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                list = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()).Where(w => w.Length > 0).Distinct().ToList();
            Loaded[name] = list;
            return list;
        }

        // The words in a text file, one per line. The last item on a line is
        // the word, so a numbered dice list works too.
        public static List<string> ReadFile(string path)
        {
            var words = new HashSet<string>();
            var ordered = new List<string>();
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                string[] parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && words.Add(parts[parts.Length - 1])) ordered.Add(parts[parts.Length - 1]);
            }
            return ordered;
        }

        // The commonest everyday words, used by the password checker.
        public static HashSet<string> CommonSet()
        {
            return new HashSet<string>(Resource("common.txt"));
        }

        // The words the current options draw from, with profanities and
        // excluded words removed. A custom list that is missing or too short
        // falls back to the common words.
        public static List<string> Active(Settings s)
        {
            List<string> baseList = null;
            bool preCleaned = false;
            if (s.WordList == "custom" && Store.CustomListPath.Length > 0)
            {
                try
                {
                    List<string> custom = ReadFile(Store.CustomListPath);
                    if (custom.Count >= Generator.MinCustomWords) baseList = custom;
                }
                catch (Exception ex)
                {
                    AppLog.Write("Custom word list could not be read: " + ex.Message);
                }
            }
            if (baseList == null && s.WordList == "eff") baseList = Resource("eff.txt");
            if (baseList == null)
            {
                bool big = s.WordList == "big";
                baseList = s.FilterProfanity ? Resource(big ? "big_clean.txt" : "common.txt") : Resource(big ? "big_all.txt" : "common_all.txt");
                preCleaned = s.FilterProfanity;
            }
            var terms = new List<string>();
            if (s.FilterProfanity && !preCleaned) terms.AddRange(Resource("profanity.txt"));
            terms.AddRange(Generator.ParseTerms(s.Blocked));
            string key = s.WordList + "|" + Store.CustomListPath + "|" + baseList.Count + "|" + s.FilterProfanity + "|" + terms.Count + "|" + s.Blocked;
            if (key != _lastKey)
            {
                _lastWords = Generator.WithoutTerms(baseList, terms);
                _lastKey = key;
            }
            return _lastWords;
        }
    }
}
