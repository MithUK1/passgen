using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Passgen
{
    /// <summary>A strength rating from a number of bits.</summary>
    public class Rating
    {
        public string Label;
        public int Level;
    }

    /// <summary>The estimate for a password someone typed in.</summary>
    public class Estimate
    {
        public double Bits;
        public string Note;
    }

    /// <summary>Strength labels, crack times and the estimate for a typed-in password.</summary>
    public static class Strength
    {
        private static readonly HashSet<string> CommonPasswords = new HashSet<string>
        {
            "password", "password1", "password123", "123456", "1234567", "12345678", "123456789", "1234567890",
            "qwerty", "qwerty123", "qwertyuiop", "abc123", "letmein", "welcome", "monkey", "dragon", "football",
            "iloveyou", "admin", "login", "princess", "sunshine", "master", "shadow", "superman", "trustno1",
            "baseball", "passw0rd", "starwars", "whatever", "freedom", "michael", "charlie", "donald", "hello",
            "111111", "000000", "121212", "654321", "666666", "696969", "123123", "123321", "1q2w3e4r", "zaq12wsx"
        };

        private const double SecondsPerYear = 31557600;
        private static readonly Regex Runs = new Regex("[A-Za-z]+|[0-9]+|[^A-Za-z0-9]+");

        // The label and level for a number of bits.
        public static Rating Of(double bits)
        {
            if (bits >= 128) return new Rating { Label = "Excellent", Level = 4 };
            if (bits >= Generator.MinBits) return new Rating { Label = "Strong", Level = 3 };
            if (bits >= 60) return new Rating { Label = "Fair", Level = 2 };
            if (bits >= 40) return new Rating { Label = "Weak", Level = 1 };
            return new Rating { Label = "Very weak", Level = 0 };
        }

        // How long trying every possibility would take at the given guesses a second.
        public static string CrackTime(double bits, double rate = 1e10)
        {
            double seconds = Math.Pow(2, bits - 1) / rate;
            if (seconds < 1) return "less than a second";
            double years = seconds / SecondsPerYear;
            CultureInfo uk = CultureInfo.GetCultureInfo("en-GB");
            if (years >= 1.38e10) return "longer than the age of the universe";
            if (years >= 1e9) return "about " + Math.Round(years / 1e9).ToString("N0", uk) + " billion years";
            if (years >= 1e6) return "about " + Math.Round(years / 1e6).ToString("N0", uk) + " million years";
            if (years >= 1)
            {
                double whole = Math.Round(years);
                return "about " + whole.ToString("N0", uk) + (whole == 1 ? " year" : " years");
            }
            var units = new[] { new KeyValuePair<double, string>(86400, "day"), new KeyValuePair<double, string>(3600, "hour"), new KeyValuePair<double, string>(60, "minute"), new KeyValuePair<double, string>(1, "second") };
            foreach (var unit in units)
            {
                if (seconds >= unit.Key)
                {
                    double count = Math.Round(seconds / unit.Key);
                    return "about " + count + " " + unit.Value + (count == 1 ? "" : "s");
                }
            }
            return "less than a second";
        }

        // The size of the character pool a typed-in password seems to use.
        private static int PoolSize(string text)
        {
            int pool = 0;
            if (Regex.IsMatch(text, "[a-z]")) pool += 26;
            if (Regex.IsMatch(text, "[A-Z]")) pool += 26;
            if (Regex.IsMatch(text, "[0-9]")) pool += 10;
            if (Regex.IsMatch(text, "[ -/:-@\\[-`{-~]")) pool += 33;
            if (Regex.IsMatch(text, "[^ -~]")) pool += 150;
            return pool;
        }

        // Every estimate starts from "each character picked freely from the
        // pool" and only goes lower where it can see a pattern: a word, a
        // year, a repeat or a run.
        private static double LetterBits(string run, HashSet<string> wordSet, double perChar)
        {
            string lower = run.ToLowerInvariant();
            double perWord = Math.Log(wordSet.Count, 2);
            var best = new double[lower.Length + 1];
            for (int i = 1; i < best.Length; i++) best[i] = double.PositiveInfinity;
            for (int end = 1; end <= lower.Length; end++)
            {
                best[end] = best[end - 1] + perChar;
                for (int start = Math.Max(0, end - 10); start <= end - 4; start++)
                    if (wordSet.Contains(lower.Substring(start, end - start))) best[end] = Math.Min(best[end], best[start] + perWord);
            }
            bool capitalised = run == lower.Substring(0, 1).ToUpperInvariant() + lower.Substring(1);
            int caseBits = run == lower ? 0 : capitalised ? 1 : run.Length;
            return Math.Min(best[lower.Length] + caseBits, run.Length * perChar);
        }

        private static double DigitBits(string run, double perChar)
        {
            int size = run.Length;
            double free = size * perChar;
            if (Regex.IsMatch(run, "^(19|20)[0-9][0-9]$")) return Math.Min(free, 7);
            if (size > 2 && run.Distinct().Count() == 1) return Math.Min(free, perChar + Math.Log(size, 2));
            if (size > 2)
            {
                int step = run[1] - run[0];
                bool steady = Math.Abs(step) == 1;
                for (int i = 0; steady && i < size; i++) steady = run[i] == run[0] + i * step;
                if (steady) return Math.Min(free, 5);
            }
            return free;
        }

        private static double SymbolBits(string run, double perChar)
        {
            List<string> chars = Generator.CodePoints(run);
            double free = chars.Count * perChar;
            if (chars.Count > 2 && chars.Distinct().Count() == 1) return Math.Min(free, perChar + Math.Log(chars.Count, 2));
            return free;
        }

        // A rough strength for a password someone already has. Null for an empty box.
        public static Estimate EstimatePassword(string text, HashSet<string> wordSet)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (CommonPasswords.Contains(text.ToLowerInvariant()))
                return new Estimate { Bits = 7, Note = "This is one of the most common passwords there is." };
            double perChar = Math.Log(PoolSize(text), 2);
            double random = Generator.CodePoints(text).Count * perChar;
            double bits = 0;
            foreach (Match match in Runs.Matches(text))
            {
                string run = match.Value;
                if (Regex.IsMatch(run, "^[A-Za-z]+$")) bits += LetterBits(run, wordSet, perChar);
                else if (Regex.IsMatch(run, "^[0-9]+$")) bits += DigitBits(run, perChar);
                else bits += SymbolBits(run, perChar);
            }
            return new Estimate
            {
                Bits = bits,
                Note = bits < random - 8 ? "It contains parts that are easy to guess, like everyday words, years or runs of numbers." : ""
            };
        }
    }
}
