using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace Passgen
{
    /// <summary>
    /// Run the exe from anywhere and it installs itself for the current user,
    /// with Desktop and Start menu shortcuts. A file with different content
    /// and the same or a newer version upgrades the install. The installed
    /// copy is always the one that runs.
    /// </summary>
    public static class Installer
    {
        public static readonly string AppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Passgen");
        public static readonly string InstalledExe = Path.Combine(AppDir, "Passgen.exe");
        public static readonly string IconPath = Path.Combine(AppDir, "Passgen.ico");
        public static string CurrentExe => Application.ExecutablePath;

        private static string _versionLabel;

        // The version plus the start of this file's checksum, so two builds
        // of the same version can be told apart.
        public static string VersionLabel
        {
            get
            {
                if (_versionLabel == null)
                {
                    string hash = FileHash(CurrentExe);
                    _versionLabel = "v" + Store.AppVersion + (hash.Length >= 8 ? " - " + hash.Substring(0, 8) : "");
                }
                return _versionLabel;
            }
        }

        // SHA-256 of a file in lower case, or empty if it can't be read.
        public static string FileHash(string path)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }

        // The version stamp from the last install, or null if never installed.
        private static Version GetInstalledVersion()
        {
            Version version;
            return Version.TryParse(Store.InstalledVersion, out version) ? version : null;
        }

        // Creates or corrects the shortcut in a folder.
        private static void SetShortcut(string folder, string target, string workingDirectory, List<string> changed)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            string file = Path.Combine(folder, "Passgen.lnk");
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic link = shell.CreateShortcut(file);
            string wantIcon = IconPath + ",0";
            if (File.Exists(file) && string.Equals((string)link.TargetPath, target, StringComparison.OrdinalIgnoreCase) && (string)link.IconLocation == wantIcon) return;
            link.TargetPath = target;
            link.Arguments = "";
            link.IconLocation = wantIcon;
            link.WorkingDirectory = workingDirectory;
            link.Description = "Generate passwords and passphrases";
            link.Save();
            if (!changed.Contains("shortcuts")) changed.Add("shortcuts");
        }

        // The folders that get a shortcut to the installed copy.
        private static string[] ShortcutFolders()
        {
            return new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Programs) };
        }

        // The folder this copy ran from, if it should get its own shortcut:
        // not the install folder, and not just Downloads.
        private static string SourceFolder()
        {
            string source = Path.GetDirectoryName(CurrentExe);
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (string.IsNullOrEmpty(source) || string.Equals(source, AppDir, StringComparison.OrdinalIgnoreCase) || string.Equals(source, downloads, StringComparison.OrdinalIgnoreCase)) return null;
            return source;
        }

        // Brings the install in line with this file and returns what changed.
        private static List<string> SyncInstall(bool force)
        {
            var changed = new List<string>();
            Directory.CreateDirectory(AppDir);

            if (force || !File.Exists(InstalledExe) || FileHash(InstalledExe) != FileHash(CurrentExe))
            {
                File.Copy(CurrentExe, InstalledExe, true);
                changed.Add("program file");
            }

            byte[] icon = Theme.GetAppIconBytes();
            if (force || !File.Exists(IconPath) || !File.ReadAllBytes(IconPath).SequenceEqual(icon))
            {
                File.WriteAllBytes(IconPath, icon);
                changed.Add("icon");
            }

            foreach (string folder in ShortcutFolders()) SetShortcut(folder, InstalledExe, AppDir, changed);
            string source = SourceFolder();
            if (source != null) SetShortcut(source, CurrentExe, source, changed);

            Store.InstalledVersion = Store.AppVersion;
            Store.Save();
            return changed;
        }

        // Asks a running copy to exit and waits, so its file can be replaced.
        private static bool StopRunningCopy()
        {
            using (var exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Passgen.Exit"))
            {
                for (int i = 0; i < 32; i++)
                {
                    bool free;
                    using (var mutex = new Mutex(false, @"Local\Passgen"))
                    {
                        // An abandoned mutex also means nothing is running.
                        try { free = mutex.WaitOne(0); } catch (AbandonedMutexException) { free = true; }
                        if (free) mutex.ReleaseMutex();
                    }
                    if (free) return true;
                    if (i == 0) exitEvent.Set();
                    Thread.Sleep(250);
                }
                return false;
            }
        }

        // Starts the installed copy.
        private static void StartInstalledCopy()
        {
            Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = false, WorkingDirectory = AppDir });
        }

        // Removes everything the install added, including settings. The
        // running file can't delete itself, so a copy outside stays.
        public static void Uninstall()
        {
            StopRunningCopy();
            foreach (string folder in ShortcutFolders().Concat(new[] { SourceFolder() }))
            {
                if (string.IsNullOrEmpty(folder)) continue;
                string file = Path.Combine(folder, "Passgen.lnk");
                if (File.Exists(file)) File.Delete(file);
            }
            Store.DeleteAll();
            try
            {
                if (!string.Equals(Path.GetDirectoryName(CurrentExe), AppDir, StringComparison.OrdinalIgnoreCase)) Directory.Delete(AppDir, true);
            }
            catch (IOException ex) { AppLog.Write("Couldn't remove the install folder: " + ex.Message); }
            MessageBox.Show("Passgen was removed.", "Passgen");
        }

        // Returns true when the installed copy was started and this one
        // should end.
        public static bool CheckInstall(bool install)
        {
            if (string.Equals(CurrentExe, InstalledExe, StringComparison.OrdinalIgnoreCase)) return false;

            Version installed = GetInstalledVersion();
            Version mine = Version.Parse(Store.AppVersion);
            bool upgrade = install || installed == null || mine > installed || !File.Exists(InstalledExe);
            // The same version with different content is an upgrade too.
            if (!upgrade && mine == installed) upgrade = FileHash(CurrentExe) != FileHash(InstalledExe);

            if (upgrade)
            {
                if (!StopRunningCopy())
                {
                    MessageBox.Show("An older Passgen is still running and could not be closed. Close it, then run this file again.", "Passgen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return true;
                }
                List<string> changes = SyncInstall(install);
                if (installed == null) Store.PendingNotice = "Passgen " + Store.AppVersion + " is installed. Shortcuts are on the Desktop and in the Start menu.";
                else if (changes.Count > 0) Store.PendingNotice = "Passgen updated to " + Store.AppVersion + " (" + string.Join(", ", changes) + ").";
                Store.Save();
            }
            StartInstalledCopy();
            return true;
        }
    }

    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool install = args.Any(a => a.Equals("-Install", StringComparison.OrdinalIgnoreCase));
            bool uninstall = args.Any(a => a.Equals("-Uninstall", StringComparison.OrdinalIgnoreCase));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (uninstall) { Installer.Uninstall(); return 0; }

            Store.Load();
            try
            {
                if (Installer.CheckInstall(install)) return 0;
            }
            catch (Exception ex)
            {
                // A failed install is reported but never stops the app running.
                AppLog.Write("Install failed: " + ex);
                MessageBox.Show("Passgen couldn't be installed: " + ex.Message + "\n\nIt will run from this file for now. The details are in the log.", "Passgen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Only one copy runs. A second launch asks the first to show itself.
            using (var mutex = new Mutex(true, @"Local\Passgen", out bool createdNew))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Passgen.Show"))
            using (var exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Passgen.Exit"))
            {
                if (!createdNew)
                {
                    showEvent.Set();
                    return 0;
                }

                AppLog.Trim();
                AppLog.Write("Passgen started");
                try
                {
                    Application.Run(new MainWindow(showEvent, exitEvent, Store.TakePendingNotice()));
                }
                finally
                {
                    try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
                }
            }
            return 0;
        }
    }
}
