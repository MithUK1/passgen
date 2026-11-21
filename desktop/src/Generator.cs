using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Passgen
{
    /// <summary>Raised when the chosen options cannot all be met together. The text is shown to the user.</summary>
    public class RuleException : Exception
    {
        public RuleException(string message) : base(message) { }
    }

    /// <summary>One batch of generated results and how they were made.</summary>
    public class GenResult
    {
        public List<string> Items;
        public double Bits;
        public string Adjusted;
        public string Suggest;
        public string Detail;
        public int Used;
        public bool Short;
    }

    /// <summary>
    /// The generator. Every result comes from the system's secure random
    /// source, and the strength figure is worked out from how the result
    /// was made, not from how it looks.
    /// </summary>
    public static class Generator
    {
        public const double MinBits = 80;
        public const int MinCustomWords = 1000;

        private const string Lower = "abcdefghijklmnopqrstuvwxyz";
        private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Digits = "0123456789";
        private const string Ambiguous = "Il1O0";
        private const string SeparatorChars = "-_.:;,+=";
        private const string SymbolChars = "!@#$%^&*-_=+?.";

        private static readonly RandomNumberGenerator Secure = RandomNumberGenerator.Create();
        private static readonly Random Fast = new Random();
        private static readonly Dictionary<string, double> AcceptCache = new Dictionary<string, double>();

        private delegate int Rnd(int max);

        // Characters for the optional fun packs.
        private static List<string> Range(int from, int to)
        {
            var list = new List<string>();
            for (int code = from; code <= to; code++) list.Add(char.ConvertFromUtf32(code));
            return list;
        }

        private static List<string> Pack(string name)
        {
            List<string> list;
            switch (name)
            {
                case "music":
                    list = Range(0x2669, 0x266f);
                    foreach (int code in new[] { 0x1F3B5, 0x1F3B6, 0x1F3BC, 0x1F3B7, 0x1F3B8, 0x1F3B9, 0x1F3BA, 0x1F3BB, 0x1F941 }) list.Add(char.ConvertFromUtf32(code));
                    return list;
                case "chess":
                    return Range(0x2654, 0x265f);
                default:
                    list = Range(0x1F600, 0x1F64F);
                    list.AddRange(Range(0x1F400, 0x1F43E));
                    return list;
            }
        }

        // A fair random whole number from 0 up to but not including max.
        // Values that would favour small numbers are thrown away.
        public static int RandomInt(int max)
        {
            ulong limit = (0x100000000UL / (ulong)max) * (ulong)max;
            var buffer = new byte[4];
            uint value;
            do
            {
                Secure.GetBytes(buffer);
                value = BitConverter.ToUInt32(buffer, 0);
            } while (value >= limit);
            return (int)(value % (uint)max);
        }

        // Quick, not secure. Only for measuring how often rules accept a try.
        private static int FastInt(int max)
        {
            return Fast.Next(max);
        }

        private static T Pick<T>(IList<T> list, Rnd rnd)
        {
            return list[rnd(list.Count)];
        }

        // Rounds, then keeps the number between low and high. Zero and
        // anything unusable becomes low.
        private static int Clamp(double value, int low, int high)
        {
            int number = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (number == 0) number = low;
            return Math.Min(high, Math.Max(low, number));
        }

        // The characters of a string as separate code points, so emoji stay whole.
        public static List<string> CodePoints(string text)
        {
            var list = new List<string>();
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    list.Add(text.Substring(i, 2));
                    i++;
                }
                else list.Add(text[i].ToString());
            }
            return list;
        }

        private static bool IsLetter(string ch)
        {
            return ch.Length == 1 && ((ch[0] >= 'a' && ch[0] <= 'z') || (ch[0] >= 'A' && ch[0] <= 'Z'));
        }

        private static bool IsDigit(string ch)
        {
            return ch.Length == 1 && ch[0] >= '0' && ch[0] <= '9';
        }

        private static bool IsSymbol(string ch)
        {
            return !IsLetter(ch) && !IsDigit(ch);
        }

        private static bool IsLetterClass(List<string> chars)
        {
            return chars.All(IsLetter);
        }

        // Splits a term list typed by the user on spaces, commas and semicolons.
        public static List<string> ParseTerms(string text)
        {
            return Regex.Split((text ?? "").ToLowerInvariant(), "[\\s,;]+").Where(t => t.Length > 0).ToList();
        }

        // Removes words that match a term exactly, and words that contain a
        // term of four or more letters.
        public static List<string> WithoutTerms(List<string> words, List<string> terms)
        {
            if (terms.Count == 0) return words;
            var exact = new HashSet<string>(terms);
            List<string> parts = terms.Where(t => t.Length >= 4).ToList();
            return words.Where(word =>
            {
                string lower = word.ToLowerInvariant();
                return !exact.Contains(lower) && !parts.Any(term => lower.Contains(term));
            }).ToList();
        }

        // The character groups a password draws from, after the site's
        // exclusions and the look-alike filter. Each group must appear.
        private static List<List<string>> CharacterClasses(Settings s)
        {
            var raw = new List<List<string>>();
            string rule = s.LetterCase;
            if (rule == "any" ? s.PwLower : rule != "upper") raw.Add(CodePoints(Lower));
            if (rule == "any" ? s.PwUpper : rule != "lower") raw.Add(CodePoints(Upper));
            if (s.PwDigits || s.SiteMinDigits > 0) raw.Add(CodePoints(Digits));
            if (s.PwSymbols || s.SiteMinSymbols > 0) raw.Add(CodePoints(s.PwSymbolSet.Length > 0 ? s.PwSymbolSet : SymbolChars));
            if (s.PackMusic) raw.Add(Pack("music"));
            if (s.PackChess) raw.Add(Pack("chess"));
            if (s.PackEmoji) raw.Add(Pack("emoji"));
            var excluded = new HashSet<string>(CodePoints(s.Exclude));
            var seen = new HashSet<string>();
            var classes = new List<List<string>>();
            foreach (List<string> chars in raw)
            {
                var kept = new List<string>();
                foreach (string ch in chars)
                {
                    if (seen.Contains(ch) || excluded.Contains(ch) || ch.Trim().Length == 0) continue;
                    if (s.AvoidAmbiguous && Ambiguous.Contains(ch)) continue;
                    seen.Add(ch);
                    kept.Add(ch);
                }
                if (kept.Count > 0) classes.Add(kept);
            }
            return classes;
        }

        // Entropy of a string drawn evenly from the pool and kept only when
        // it holds a character of every class (inclusion-exclusion over the
        // classes), and starts with a letter when that is asked for.
        private static double PasswordBits(List<List<string>> classes, int length, bool startLetter)
        {
            int[] sizes = classes.Select(c => c.Count).ToArray();
            bool[] letters = classes.Select(IsLetterClass).ToArray();
            int total = sizes.Sum();
            int letterTotal = 0;
            for (int i = 0; i < sizes.Length; i++) if (letters[i]) letterTotal += sizes[i];
            double accept = 0;
            for (int mask = 0; mask < 1 << sizes.Length; mask++)
            {
                int removed = 0, removedLetters = 0, members = 0;
                for (int i = 0; i < sizes.Length; i++)
                {
                    if ((mask & (1 << i)) == 0) continue;
                    removed += sizes[i];
                    members++;
                    if (letters[i]) removedLetters += sizes[i];
                }
                double rest = Math.Pow((double)(total - removed) / total, startLetter ? length - 1 : length);
                double first = startLetter ? (double)(letterTotal - removedLetters) / total : 1;
                accept += (members % 2 == 1 ? -1 : 1) * first * rest;
            }
            return length * Math.Log(total, 2) + Math.Log(Math.Max(accept, 1e-12), 2);
        }

        // Inserts a hyphen every few characters, for easier reading.
        private static string Grouped(string text, int size)
        {
            if (size == 0) return text;
            List<string> chars = CodePoints(text);
            var blocks = new List<string>();
            for (int i = 0; i < chars.Count; i += size) blocks.Add(string.Concat(chars.Skip(i).Take(size)));
            return string.Join("-", blocks);
        }

        // 1 for a letter, 2 for a digit, 0 for anything else.
        private static int CharKind(string ch)
        {
            return IsLetter(ch) ? 1 : IsDigit(ch) ? 2 : 0;
        }

        // True for three letters or digits in a row that step by one, like abc or 321.
        public static bool HasSequence(string text)
        {
            List<string> chars = CodePoints(text.ToLowerInvariant());
            for (int i = 2; i < chars.Count; i++)
            {
                int kind = CharKind(chars[i]);
                if (kind == 0 || CharKind(chars[i - 1]) != kind || CharKind(chars[i - 2]) != kind) continue;
                int a = chars[i - 2][0], b = chars[i - 1][0], c = chars[i][0];
                if (b - a == c - b && Math.Abs(b - a) == 1) return true;
            }
            return false;
        }

        private static bool DayMonth(int a, int b)
        {
            return (a >= 1 && a <= 31 && b >= 1 && b <= 12) || (a >= 1 && a <= 12 && b >= 1 && b <= 31);
        }

        private static bool YearLike(string text)
        {
            return text.Length == 2 || (text.Length == 4 && (text.StartsWith("19") || text.StartsWith("20")));
        }

        // Dates in day-first or month-first order, written 31/12/24 or 311224 or 12312024 and so on.
        public static bool LooksLikeDate(string text)
        {
            foreach (Match m in Regex.Matches(text, "([0-9]{1,4})([/.\\-])([0-9]{1,2})\\2([0-9]{1,4})"))
            {
                string a = m.Groups[1].Value, c = m.Groups[4].Value;
                int b = int.Parse(m.Groups[3].Value);
                if (a.Length <= 2 && c.Length >= 2 && YearLike(c) && DayMonth(int.Parse(a), b)) return true;
                if (a.Length == 4 && c.Length <= 2 && YearLike(a) && DayMonth(b, int.Parse(c))) return true;
            }
            foreach (Match m in Regex.Matches(text, "[0-9]{6,}"))
            {
                string run = m.Value;
                for (int i = 0; i + 6 <= run.Length; i++)
                {
                    string x = run.Substring(i, 2), y = run.Substring(i + 2, 2), z = run.Substring(i + 4, 2);
                    if ((YearLike(z) && DayMonth(int.Parse(x), int.Parse(y))) || (YearLike(x) && DayMonth(int.Parse(y), int.Parse(z)))) return true;
                    if (i + 8 > run.Length) continue;
                    string first = run.Substring(i, 4), last = run.Substring(i + 4, 4);
                    if (YearLike(last) && DayMonth(int.Parse(first.Substring(0, 2)), int.Parse(first.Substring(2)))) return true;
                    if (YearLike(first) && DayMonth(int.Parse(last.Substring(0, 2)), int.Parse(last.Substring(2)))) return true;
                }
            }
            return false;
        }

        // The site's text rules as one test: true when the text breaks one.
        private static Func<string, bool> RuleCheck(Settings s)
        {
            Regex repeat = s.MaxRepeat > 0 ? new Regex("(?s)(.)\\1{" + s.MaxRepeat + "}") : null;
            return text => (repeat != null && repeat.IsMatch(text)) || (s.NoSequences && HasSequence(text)) || (s.NoDates && LooksLikeDate(text));
        }

        private static bool HasTextRules(Settings s)
        {
            return s.MaxRepeat > 0 || s.NoSequences || s.NoDates;
        }

        // Rules that throw results away change how many results are possible,
        // so the share that survives is measured once and remembered, which
        // keeps the figure steady.
        private static double AcceptRate(string key, Func<bool> trial, int runs)
        {
            double rate;
            if (!AcceptCache.TryGetValue(key, out rate))
            {
                int kept = 0;
                for (int i = 0; i < runs; i++) if (trial()) kept++;
                if (AcceptCache.Count > 200) AcceptCache.Clear();
                rate = kept > 0 ? (double)kept / runs : 0;
                AcceptCache[key] = rate;
            }
            if (rate == 0) throw new RuleException("Those rules cannot all be met together. Loosen one of them.");
            return rate;
        }

        private static bool Between(int value, int least, int most)
        {
            return value >= least && (most == 0 || value <= most);
        }

        private static void CheckRange(int least, int most, string what)
        {
            if (most > 0 && least > most) throw new RuleException("The least " + what + " is more than the most.");
        }

        private class Plan
        {
            public Func<string> Make;
            public double Bits;
            public string Adjusted;
            public int Used;
            public string Detail;
            public double PerUnit;
            public string Unit;
            public bool CanGrow;
        }

        // Works out how to make a password under the options and how strong it will be.
        private static Plan PlanPassword(Settings s)
        {
            List<List<string>> classes = CharacterClasses(s);
            if (classes.Count == 0) throw new RuleException("Select at least one character type.");
            List<string> pool = classes.SelectMany(c => c).ToList();
            if (s.StartLetter && !classes.Any(IsLetterClass)) throw new RuleException("Tick lowercase or uppercase letters to start with a letter.");
            if (s.SiteMinDigits > 0 && !pool.Any(IsDigit)) throw new RuleException("Every digit is on the never-use list.");
            if (s.SiteMinSymbols > 0 && !pool.Any(IsSymbol)) throw new RuleException("Every symbol is on the never-use list.");
            CheckRange(s.SiteMinDigits, s.SiteMaxDigits, "numbers");
            CheckRange(s.SiteMinSymbols, s.SiteMaxSymbols, "symbols");
            CheckRange(s.SiteMinLength, s.SiteMaxLength, "length");

            int group = Clamp(s.Group, 0, 8);
            Func<int, int> shown = n => n + (group > 0 ? (n - 1) / group : 0);
            int maxLength = s.SiteMaxLength > 0 ? Clamp(s.SiteMaxLength, 1, 128) : 0;
            int cap = 128;
            while (maxLength > 0 && cap > 1 && shown(cap) > maxLength) cap--;
            int floor = Math.Max(Math.Max(classes.Count, s.SiteMinDigits + s.SiteMinSymbols), 1);
            while (s.SiteMinLength > 0 && floor < 128 && shown(floor) < s.SiteMinLength) floor++;
            if (floor > cap) throw new RuleException("Those length and character rules cannot all fit together.");
            int requested = Clamp(s.PwLength, 4, 128);
            int length = Math.Min(cap, Math.Max(floor, requested));

            List<string> starts = s.StartLetter ? classes.Where(IsLetterClass).SelectMany(c => c).ToList() : pool;
            List<HashSet<string>> sets = classes.Select(c => new HashSet<string>(c)).ToList();
            Func<string, bool> breaks = RuleCheck(s);
            bool counted = s.SiteMinDigits > 0 || s.SiteMaxDigits > 0 || s.SiteMinSymbols > 0 || s.SiteMaxSymbols > 0;
            Func<Rnd, List<string>> attempt = rnd =>
            {
                var chars = new List<string>(length);
                for (int i = 0; i < length; i++) chars.Add(Pick(i == 0 ? starts : pool, rnd));
                return chars;
            };
            Func<List<string>, bool> valid = chars =>
            {
                if (!sets.All(set => chars.Any(set.Contains))) return false;
                if (counted)
                {
                    int digits = chars.Count(IsDigit);
                    int symbols = chars.Count(IsSymbol);
                    if (!Between(digits, s.SiteMinDigits, s.SiteMaxDigits) || !Between(symbols, s.SiteMinSymbols, s.SiteMaxSymbols)) return false;
                }
                return !breaks(Grouped(string.Concat(chars), group));
            };

            double bits;
            if (counted || HasTextRules(s))
            {
                double proposals = (s.StartLetter ? Math.Log(starts.Count, 2) : Math.Log(pool.Count, 2)) + (length - 1) * Math.Log(pool.Count, 2);
                bits = proposals + Math.Log(AcceptRate("pw|" + pool.Count + "|" + length + "|" + s.Key(), () => valid(attempt(FastInt)), 20000), 2);
            }
            else bits = PasswordBits(classes, length, s.StartLetter);

            Func<string> make = () =>
            {
                for (int tries = 0; tries < 200000; tries++)
                {
                    List<string> chars = attempt(RandomInt);
                    if (valid(chars)) return Grouped(string.Concat(chars), group);
                }
                throw new RuleException("Could not meet every rule at this length.");
            };
            return new Plan
            {
                Make = make,
                Bits = bits,
                Adjusted = length == requested ? null : "Length changed from " + requested + " to " + length + " to fit the site's rules.",
                Used = length,
                Detail = pool.Count + " possible characters",
                PerUnit = Math.Log(pool.Count, 2),
                Unit = "characters",
                CanGrow = length < cap
            };
        }

        private class Layout
        {
            public int N, Parts, Extra;
            public bool Safe, Possible;
            public double FixedLength;
        }

        // Works out how to make a passphrase under the options and how strong it will be.
        private static Plan PlanPassphrase(Settings s, List<string> words)
        {
            int shortest = Clamp(s.WordMin, 0, 64);
            int longest = Clamp(s.WordMax, 0, 64);
            int low = shortest > 0 && longest > 0 && shortest > longest ? longest : shortest;
            int high = shortest > 0 && longest > 0 && shortest > longest ? shortest : longest;
            var excluded = new HashSet<char>(s.Exclude.ToLowerInvariant());
            Func<string, bool> allowed = word => excluded.Count == 0 || !word.ToLowerInvariant().Any(excluded.Contains);
            var byLength = new SortedDictionary<int, List<string>>();
            int total = 0;
            foreach (string word in words)
            {
                int size = word.Length;
                if ((low > 0 && size < low) || (high > 0 && size > high) || !allowed(word)) continue;
                List<string> bucket;
                if (!byLength.TryGetValue(size, out bucket)) byLength[size] = bucket = new List<string>();
                bucket.Add(word);
                total++;
            }
            if (total < 2) throw new RuleException("No words of that length in this list. Try a wider range.");
            List<int> lengths = byLength.Keys.ToList();
            int smallest = byLength.Values.Min(list => list.Count);
            List<string> flat = s.RandomLength ? null : byLength.Values.SelectMany(l => l).ToList();
            Func<Rnd, string> nextWord = rnd => s.RandomLength ? Pick(byLength[Pick(lengths, rnd)], rnd) : Pick(flat, rnd);
            // Worst case per word, so the figure holds for the most likely word an attacker could try.
            double perWord = s.RandomLength ? Math.Log((double)lengths.Count * smallest, 2) : Math.Log(total, 2);
            int shortestWord = lengths.Min();
            int longestWord = lengths.Max();

            Func<string, string> styled = word =>
            {
                if (s.LetterCase == "upper") return word.ToUpperInvariant();
                if (s.LetterCase == "lower") return word.ToLowerInvariant();
                return s.LetterCase == "both" || s.Capitalize ? word.Substring(0, 1).ToUpperInvariant() + word.Substring(1) : word;
            };

            CheckRange(s.SiteMinDigits, s.SiteMaxDigits, "numbers");
            CheckRange(s.SiteMinSymbols, s.SiteMaxSymbols, "symbols");
            CheckRange(s.SiteMinLength, s.SiteMaxLength, "length");
            int minDigits = Clamp(s.SiteMinDigits, 0, 10);
            int maxDigits = s.SiteMaxDigits > 0 ? Clamp(s.SiteMaxDigits, 1, 10) : 10;
            bool wantsNumber = s.AddNumber || minDigits > 0;
            int fixedDigits = s.NumberDigits == 0 ? 0 : Clamp(s.NumberDigits, 2, 10);
            List<string> digitPool = CodePoints(Digits).Where(ch => !s.Exclude.Contains(ch)).ToList();
            if (wantsNumber && digitPool.Count < 2) throw new RuleException("Too many digits are on the never-use list for a number.");
            List<int> numberLengths = new List<int>();
            if (wantsNumber)
            {
                IEnumerable<int> candidates = fixedDigits > 0 ? new[] { fixedDigits } : Enumerable.Range(2, 9);
                numberLengths = candidates.Where(n => n >= minDigits && n <= maxDigits).ToList();
                if (numberLengths.Count == 0) numberLengths.Add(Math.Min(maxDigits, Math.Max(minDigits, fixedDigits > 0 ? fixedDigits : 2)));
            }
            double digitBits = Math.Log(Math.Max(digitPool.Count, 1), 2);
            double numberBits = wantsNumber ? Math.Log(numberLengths.Count, 2) + numberLengths.Min() * digitBits : 0;
            Func<Rnd, string> makeNumber = rnd =>
            {
                int size = Pick(numberLengths, rnd);
                var sb = new StringBuilder();
                for (int i = 0; i < size; i++) sb.Append(Pick(digitPool, rnd));
                return sb.ToString();
            };

            bool random = s.Separator == "random";
            List<string> separators = random
                ? CodePoints(SeparatorChars).Where(ch => !s.Exclude.Contains(ch)).ToList()
                : new List<string> { s.Separator };
            if (separators.Count == 0) throw new RuleException("Every separator is on the never-use list.");
            if (!random && s.Separator.Length > 0 && s.Exclude.Contains(s.Separator)) throw new RuleException("The separator is on the never-use list. Pick another one.");
            double gapBits = random ? Math.Log(separators.Count, 2) : 0;
            bool separatorIsSymbol = random || (s.Separator != "" && s.Separator != " ");
            int sepLength = random ? 1 : s.Separator.Length;
            List<string> symbolPool = CodePoints(SymbolChars).Where(ch => !s.Exclude.Contains(ch)).ToList();
            Func<Rnd, string> separator = rnd => random ? Pick(separators, rnd) : s.Separator;

            int minLength = s.SiteMinLength;
            int maxLength = s.SiteMaxLength;
            Func<int, Layout> lay = n =>
            {
                int parts = n + (wantsNumber ? 1 : 0);
                int symbols = separatorIsSymbol ? parts - 1 : 0;
                int extra = Math.Max(0, s.SiteMinSymbols - symbols);
                if ((s.SiteMaxSymbols > 0 && symbols > s.SiteMaxSymbols) || (extra > 0 && symbolPool.Count == 0)) return null;
                int fixedLength = (parts - 1) * sepLength + extra;
                int best = n * shortestWord + fixedLength + (wantsNumber ? numberLengths.Min() : 0);
                int worst = n * longestWord + fixedLength + (wantsNumber ? numberLengths.Max() : 0);
                bool safe = (maxLength == 0 || worst <= maxLength) && (minLength == 0 || best >= minLength);
                bool possible = (maxLength == 0 || best <= maxLength) && (minLength == 0 || worst >= minLength);
                return new Layout
                {
                    N = n, Parts = parts, Extra = extra, Safe = safe, Possible = possible,
                    FixedLength = fixedLength + (wantsNumber ? (numberLengths.Min() + numberLengths.Max()) / 2.0 : 0)
                };
            };
            int requested = Clamp(s.Words, 3, 20);
            if (separatorIsSymbol && s.SiteMaxSymbols > 0 && requested + (wantsNumber ? 1 : 0) - 1 > s.SiteMaxSymbols)
                throw new RuleException("The separator is a symbol and adds more symbols than the site allows. Choose Space or None.");
            List<Layout> options = Enumerable.Range(1, 20).Select(lay).Where(l => l != null).ToList();
            Func<IEnumerable<Layout>, Layout> nearest = list =>
                list.OrderBy(l => Math.Abs(l.N - requested)).ThenByDescending(l => l.N).FirstOrDefault();
            // When the limits only fit some of the words, prefer the count whose usual length lands inside them.
            double typical = s.RandomLength
                ? lengths.Sum() / (double)lengths.Count
                : byLength.Sum(pair => (double)pair.Key * pair.Value.Count) / total;
            Func<Layout, double> miss = o =>
            {
                double usual = o.N * typical + o.FixedLength;
                return Math.Max(0, Math.Max(minLength - usual, usual - (maxLength > 0 ? maxLength : double.PositiveInfinity)));
            };
            Func<IEnumerable<Layout>, Layout> closest = list =>
                list.OrderBy(miss).ThenBy(l => Math.Abs(l.N - requested)).ThenByDescending(l => l.N).FirstOrDefault();
            Layout layout = nearest(options.Where(o => o.Safe)) ?? closest(options.Where(o => o.Possible));
            if (layout == null) throw new RuleException("Those rules cannot all be met with these words and separator.");
            int count = layout.N;
            Layout next = lay(count + 1);

            Func<string, bool> breaks = RuleCheck(s);
            Func<string, bool> okText = text => (minLength == 0 || text.Length >= minLength) && (maxLength == 0 || text.Length <= maxLength) && !breaks(text);
            Func<Rnd, string> build = rnd =>
            {
                var parts = new List<string>();
                for (int i = 0; i < count; i++) parts.Add(styled(nextWord(rnd)));
                if (wantsNumber) parts.Add(makeNumber(rnd));
                var text = new StringBuilder(parts[0]);
                for (int i = 1; i < parts.Count; i++) text.Append(separator(rnd)).Append(parts[i]);
                for (int i = 0; i < layout.Extra; i++) text.Append(Pick(symbolPool, rnd));
                return text.ToString();
            };

            double bits = count * perWord + (layout.Parts - 1) * gapBits + numberBits + layout.Extra * Math.Log(Math.Max(symbolPool.Count, 1), 2);
            if (!layout.Safe || HasTextRules(s))
                bits += Math.Log(AcceptRate("pp|" + total + "|" + count + "|" + s.Key() + "|" + string.Join(",", lengths), () => okText(build(FastInt)), 4000), 2);
            Func<string> make = () =>
            {
                for (int tries = 0; tries < 20000; tries++)
                {
                    string text = build(RandomInt);
                    if (okText(text)) return text;
                }
                throw new RuleException("Could not meet every rule with these settings.");
            };
            return new Plan
            {
                Make = make,
                Bits = bits,
                Adjusted = count == requested ? null : "Words changed from " + requested + " to " + count + " to fit the site's rules.",
                Used = count,
                Detail = count + " words",
                PerUnit = perWord + gapBits,
                Unit = "words",
                CanGrow = next != null && next.Safe && count < 20
            };
        }

        // A PIN: numbers, or numbers and capital letters.
        private static Plan PlanPin(Settings s)
        {
            int length = Clamp(s.PinLength, 4, 16);
            List<string> pool = CodePoints(s.PinType == "alnum" ? Upper + Digits : Digits);
            return new Plan
            {
                Make = () =>
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < length; i++) sb.Append(Pick(pool, RandomInt));
                    return sb.ToString();
                },
                Bits = length * Math.Log(pool.Count, 2),
                Detail = pool.Count + " possible characters"
            };
        }

        // A random token the size of a hash, in hex or base64.
        private static Plan PlanHash(Settings s)
        {
            int bits = Math.Min(1024, Math.Max(32, s.HashBits / 8 * 8));
            Func<string> make = () =>
            {
                var bytes = new byte[bits / 8];
                Secure.GetBytes(bytes);
                if (s.HashFormat == "hex") return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                string base64 = Convert.ToBase64String(bytes);
                return s.HashFormat == "base64url" ? base64.Replace('+', '-').Replace('/', '_').TrimEnd('=') : base64;
            };
            return new Plan { Make = make, Bits = bits, Detail = bits + " random bits" };
        }

        // Makes the requested number of results and describes their strength.
        // Throws RuleException when the options cannot be met.
        public static GenResult Generate(Settings s, List<string> words)
        {
            int count = Clamp(s.Count, 1, 100);
            Plan plan;
            switch (s.Mode)
            {
                case "password": plan = PlanPassword(s); break;
                case "passphrase": plan = PlanPassphrase(s, words); break;
                case "pin": plan = PlanPin(s); break;
                default: plan = PlanHash(s); break;
            }
            string suggest = null;
            if (plan.PerUnit > 0 && plan.Bits < MinBits)
            {
                if (plan.CanGrow)
                {
                    int more = (int)Math.Ceiling((MinBits - plan.Bits) / plan.PerUnit);
                    suggest = plan.Unit == "words"
                        ? "Add " + more + " more words to reach the strong level."
                        : "Make it " + (plan.Used + more) + " characters or more to reach the strong level.";
                }
                else suggest = "The site's rules keep this at " + (int)Math.Floor(plan.Bits) + " bits.";
            }
            var items = new List<string>();
            for (int i = 0; i < count; i++) items.Add(plan.Make());
            return new GenResult { Items = items, Bits = plan.Bits, Adjusted = plan.Adjusted, Suggest = suggest, Detail = plan.Detail, Used = plan.Used, Short = s.Mode == "pin" };
        }
    }
}
