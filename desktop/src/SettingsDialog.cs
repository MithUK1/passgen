using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Passgen
{
    /// <summary>
    /// Settings: rules for fussy sites, how many results and how long the
    /// clipboard keeps them, presets, and backup and reset. Like the main
    /// window, everything is saved as soon as it changes.
    /// </summary>
    public class SettingsDialog : Form
    {
        private readonly Binder _binder;
        private readonly TabControl _tabs = new TabControl();
        private ListBox _presetList;
        private Button _resetDefaults, _resetAll;
        private Label _dataStatus;

        private static Settings Current => Store.Active.Settings;

        public SettingsDialog()
        {
            Text = "Settings";
            ClientSize = new Size(560, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Theme.ApplyChrome(this, true);
            _binder = new Binder(() => Current, () => Store.Save());

            _tabs.SetBounds(16, 16, 528, 394);
            _tabs.Font = Theme.UiFont;
            AddTab("Site rules", BuildSiteTab);
            AddTab("Output", BuildOutputTab);
            AddTab("Presets", BuildPresetsTab);
            AddTab("Data", BuildDataTab);
            Controls.Add(_tabs);

            Button help = Theme.NewButton("Help", 16, 424, 90, 30);
            help.Click += (s, e) => OpenHelp();
            Button close = Theme.NewButton("Close", 454, 424, 90, 30, primary: true);
            close.DialogResult = DialogResult.Cancel;
            CancelButton = close;
            Controls.AddRange(new Control[] { help, close });
            Controls.Add(Theme.NewLabel(Installer.VersionLabel, 120, 431, 200, 18, Theme.MutedText, Theme.UiFontSmall));

            _binder.Load();
            RefreshPresets();
        }

        // A tab page with the shared background, filled by a builder.
        private void AddTab(string title, Action<Panel> build)
        {
            var page = new TabPage(title) { BackColor = Theme.CardBg, UseVisualStyleBackColor = false };
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CardBg, Padding = new Padding(12) };
            page.Controls.Add(panel);
            _tabs.TabPages.Add(page);
            build(panel);
        }

        private static Label Hint(string text, int x, int y, int width, int height = 34)
        {
            var label = Theme.NewLabel(text, x, y, width, height, Theme.MutedText, Theme.UiFontSmall);
            label.AutoEllipsis = false;
            return label;
        }

        // "at least [n] at most [n]" row for a site rule.
        private void AddRange(Panel p, string title, int y, int max, Func<Settings, int> getLow, Action<Settings, int> setLow, Func<Settings, int> getHigh, Action<Settings, int> setHigh)
        {
            p.Controls.Add(Theme.NewLabel(title, 12, y + 4, 80, 20));
            var low = Theme.NewNumber(150, y, 64, 0, max);
            var high = Theme.NewNumber(330, y, 64, 0, max);
            p.Controls.AddRange(new Control[] { Theme.NewLabel("at least", 96, y + 4, 52, 20), low, Theme.NewLabel("at most", 268, y + 4, 60, 20), high });
            _binder.Number(low, getLow, setLow);
            _binder.Number(high, getHigh, setHigh);
        }

        private void BuildSiteTab(Panel p)
        {
            p.Controls.Add(Hint("Rules a site makes you follow. Leave a box at 0 for no rule.", 12, 10, 490, 20));
            AddRange(p, "Length", 40, 128, s => s.SiteMinLength, (s, v) => s.SiteMinLength = v, s => s.SiteMaxLength, (s, v) => s.SiteMaxLength = v);
            AddRange(p, "Numbers", 76, 10, s => s.SiteMinDigits, (s, v) => s.SiteMinDigits = v, s => s.SiteMaxDigits, (s, v) => s.SiteMaxDigits = v);
            AddRange(p, "Symbols", 112, 20, s => s.SiteMinSymbols, (s, v) => s.SiteMinSymbols = v, s => s.SiteMaxSymbols, (s, v) => s.SiteMaxSymbols = v);
            p.Controls.Add(Theme.NewLabel("Letters", 12, 154, 80, 20));
            ComboBox letters = Theme.NewDropDown(150, 150, 244, "No rule", "Upper and lower case", "Uppercase only", "Lowercase only");
            p.Controls.Add(letters);
            _binder.Combo(letters, Settings.LetterCases, s => s.LetterCase, (s, v) => s.LetterCase = v);
            p.Controls.Add(Theme.NewLabel("Same character in a row, at most", 12, 190, 220, 20));
            var repeat = Theme.NewNumber(240, 186, 64, 0, 10);
            p.Controls.Add(repeat);
            _binder.Number(repeat, s => s.MaxRepeat, (s, v) => s.MaxRepeat = v);
            CheckBox start = Theme.NewCheck("Must start with a letter (passwords)", 12, 224, 380);
            CheckBox runs = Theme.NewCheck("No runs like abc or 123", 12, 250, 380);
            CheckBox dates = Theme.NewCheck("Nothing like a birth date", 12, 276, 380);
            p.Controls.AddRange(new Control[] { start, runs, dates });
            _binder.Check(start, s => s.StartLetter, (s, v) => s.StartLetter = v);
            _binder.Check(runs, s => s.NoSequences, (s, v) => s.NoSequences = v);
            _binder.Check(dates, s => s.NoDates, (s, v) => s.NoDates = v);
            p.Controls.Add(Theme.NewLabel("Never use these", 12, 312, 120, 20));
            TextBox exclude = Theme.NewText(150, 308, 340, new Font("Consolas", 9.5f));
            p.Controls.Add(exclude);
            _binder.Text(exclude, s => s.Exclude, (s, v) => s.Exclude = v);
            p.Controls.Add(Hint("Characters the site refuses. They are left out of passwords, passphrases and separators.", 12, 338, 490));
        }

        private void BuildOutputTab(Panel p)
        {
            p.Controls.Add(Theme.NewLabel("How many", 12, 14, 130, 20));
            var count = Theme.NewNumber(150, 10, 64, 1, 100);
            p.Controls.Add(count);
            _binder.Number(count, s => s.Count, (s, v) => s.Count = v);
            p.Controls.Add(Hint("1 to 100, used for Copy and Save.", 12, 42, 490, 20));
            p.Controls.Add(Theme.NewLabel("Clear clipboard after", 12, 82, 140, 20));
            ComboBox clear = Theme.NewDropDown(150, 78, 160, "15 seconds", "30 seconds", "1 minute", "90 seconds", "2 minutes", "5 minutes", "Never");
            p.Controls.Add(clear);
            _binder.ComboInt(clear, new[] { 15, 30, 60, 90, 120, 300, 0 }, s => s.ClearSeconds, (s, v) => s.ClearSeconds = v);
            p.Controls.Add(Hint("The clipboard is only cleared if it still holds what was copied, so something else you copied is never wiped.", 12, 112, 490));
            p.Controls.Add(Theme.NewLabel("Save a batch as", 12, 170, 130, 20));
            Button txt = Theme.NewButton(".txt file", 150, 165, 100, 30);
            Button csv = Theme.NewButton(".csv file", 260, 165, 100, 30);
            txt.Click += (s, e) => SaveBatch(false);
            csv.Click += (s, e) => SaveBatch(true);
            p.Controls.AddRange(new Control[] { txt, csv });
            p.Controls.Add(Hint("Makes a new batch with the current type and options. Saved files are not encrypted.", 12, 202, 490));
        }

        // Makes a fresh batch and saves it as text or CSV.
        private void SaveBatch(bool csv)
        {
            try
            {
                Settings s = Current;
                GenResult batch = Generator.Generate(s, s.Mode == "passphrase" ? Words.Active(s) : null);
                using (var dialog = new SaveFileDialog { Filter = csv ? "CSV files (*.csv)|*.csv" : "Text files (*.txt)|*.txt", FileName = "passwords" + (csv ? ".csv" : ".txt"), OverwritePrompt = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    string text = csv
                        ? "password\r\n" + string.Join("\r\n", batch.Items.Select(item => "\"" + item.Replace("\"", "\"\"") + "\""))
                        : string.Join("\r\n", batch.Items);
                    File.WriteAllText(dialog.FileName, text + "\r\n", new UTF8Encoding(false));
                }
            }
            catch (RuleException ex)
            {
                MessageBox.Show(ex.Message, "Passgen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (IOException ex)
            {
                AppLog.Write("Save failed: " + ex.Message);
                MessageBox.Show("The file couldn't be saved: " + ex.Message, "Passgen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BuildPresetsTab(Panel p)
        {
            p.Controls.Add(Hint("A preset is a saved set of options with a name, like \"Wi-Fi\" or \"Strict site\". Every change you make is saved into the selected one.", 12, 10, 490, 34));
            _presetList = new ListBox { BorderStyle = BorderStyle.FixedSingle, Font = Theme.UiFont, IntegralHeight = false };
            _presetList.SetBounds(12, 52, 330, 250);
            _presetList.SelectedIndexChanged += (s, e) =>
            {
                if (_presetList.SelectedIndex >= 0 && Store.Presets[_presetList.SelectedIndex].Id != Store.ActiveId)
                {
                    Store.ActiveId = Store.Presets[_presetList.SelectedIndex].Id;
                    Store.Save();
                    _binder.Load();
                }
            };
            Button add = Theme.NewButton("New from current", 354, 52, 150, 30);
            Button rename = Theme.NewButton("Rename", 354, 90, 150, 30);
            Button delete = Theme.NewButton("Delete", 354, 128, 150, 30, warning: true);
            add.Click += (s, e) => NewPreset();
            rename.Click += (s, e) => RenamePreset();
            delete.Click += (s, e) => DeletePreset();
            p.Controls.AddRange(new Control[] { _presetList, add, rename, delete });
            p.Controls.Add(Hint("Up to " + Store.MaxPresets + " presets. The selected one is what the main window uses.", 12, 310, 490, 20));
        }

        // Lists the presets with the active one selected.
        private void RefreshPresets()
        {
            _presetList.Items.Clear();
            foreach (Preset preset in Store.Presets) _presetList.Items.Add(preset.Name);
            _presetList.SelectedIndex = Store.Presets.IndexOf(Store.Active);
        }

        private void NewPreset()
        {
            if (Store.Presets.Count >= Store.MaxPresets)
            {
                MessageBox.Show("You can keep up to " + Store.MaxPresets + " presets. Delete one first.", "Passgen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string name = PromptDialog.Show(this, "New preset", "Name for this preset", "");
            if (name == null) return;
            var preset = new Preset { Id = Store.NewId(), Name = name, Settings = Current.Clone() };
            Store.Presets.Add(preset);
            Store.ActiveId = preset.Id;
            Store.Save();
            RefreshPresets();
            _binder.Load();
        }

        private void RenamePreset()
        {
            string name = PromptDialog.Show(this, "Rename preset", "New name", Store.Active.Name);
            if (name == null) return;
            Store.Active.Name = name;
            Store.Save();
            RefreshPresets();
        }

        private void DeletePreset()
        {
            if (Store.Presets.Count <= 1)
            {
                MessageBox.Show("You need at least one preset.", "Passgen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Delete the preset \"" + Store.Active.Name + "\"? This can't be undone.", "Passgen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Store.Presets.Remove(Store.Active);
            Store.ActiveId = Store.Presets[0].Id;
            Store.Save();
            RefreshPresets();
            _binder.Load();
        }

        private void BuildDataTab(Panel p)
        {
            p.Controls.Add(Hint("Your settings are kept in one file. Export or import them as a backup, or open the file to look at it.", 12, 10, 490, 34));
            Button export = Theme.NewButton("Export settings...", 12, 52, 160, 30);
            Button import = Theme.NewButton("Import settings...", 180, 52, 160, 30);
            Button open = Theme.NewButton("Open settings file", 348, 52, 150, 30);
            export.Click += (s, e) => ExportSettings();
            import.Click += (s, e) => ImportSettings();
            open.Click += (s, e) => OpenSettingsFile();
            p.Controls.AddRange(new Control[] { export, import, open });
            _dataStatus = Hint("", 12, 92, 490, 34);
            p.Controls.Add(_dataStatus);
            p.Controls.Add(Theme.NewLabel("Reset", 12, 150, 100, 20, null, Theme.UiFontBold));
            _resetDefaults = Theme.NewButton("Restore defaults", 12, 178, 160, 30, warning: true);
            _resetAll = Theme.NewButton("Remove all app data", 180, 178, 170, 30, warning: true);
            _resetDefaults.Click += (s, e) => RestoreDefaults();
            _resetAll.Click += (s, e) => RemoveAllData();
            p.Controls.AddRange(new Control[] { _resetDefaults, _resetAll });
            p.Controls.Add(Hint("Restore defaults puts every preset back to one default preset. Remove all app data also deletes the settings file and log, then closes Passgen. Neither removes the program or its shortcuts.", 12, 214, 490, 54));
        }

        private void ExportSettings()
        {
            using (var dialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = "passgen-settings.json", OverwritePrompt = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dialog.FileName, Store.ExportJson(), new UTF8Encoding(false));
                    _dataStatus.Text = "Exported.";
                }
                catch (IOException ex)
                {
                    AppLog.Write("Export failed: " + ex.Message);
                    _dataStatus.Text = "The file couldn't be saved: " + ex.Message;
                }
            }
        }

        private void ImportSettings()
        {
            using (var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (MessageBox.Show("Importing replaces all of your current presets with the ones in the file. Continue?", "Passgen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                try
                {
                    Store.ImportJson(File.ReadAllText(dialog.FileName, Encoding.UTF8));
                    RefreshPresets();
                    _binder.Load();
                    _dataStatus.Text = "Imported " + Store.Presets.Count + (Store.Presets.Count == 1 ? " preset." : " presets.");
                }
                catch (FormatException ex) { _dataStatus.Text = ex.Message; }
                catch (IOException ex)
                {
                    AppLog.Write("Import failed: " + ex.Message);
                    _dataStatus.Text = "The file couldn't be read: " + ex.Message;
                }
            }
        }

        private void OpenSettingsFile()
        {
            Store.Save();
            if (File.Exists(Store.IniPath)) Process.Start(new ProcessStartInfo("notepad.exe", "\"" + Store.IniPath + "\"") { UseShellExecute = true });
        }

        private void RestoreDefaults()
        {
            if (MessageBox.Show("Put every preset back to one default preset? Your presets will be lost.", "Passgen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Store.RestoreDefaults();
            RefreshPresets();
            _binder.Load();
            _dataStatus.Text = "Defaults restored.";
        }

        private void RemoveAllData()
        {
            if (MessageBox.Show("Delete the settings file and the log, then close Passgen? Next time it starts as if new.", "Passgen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Store.DeleteAll();
            Application.Exit();
        }

        // The help page is the README on the repository.
        private void OpenHelp()
        {
            try { Process.Start(new ProcessStartInfo(Store.HelpUrl) { UseShellExecute = true }); }
            catch (Exception ex) { AppLog.Write("Couldn't open help: " + ex.Message); }
        }
    }
}
