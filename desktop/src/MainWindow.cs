using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Passgen
{
    /// <summary>
    /// The main window: pick a type, see the result with its strength, and
    /// change that type's options. Every change is saved straight into the
    /// selected preset and makes a fresh result.
    /// </summary>
    public class MainWindow : Form
    {
        private static readonly string[] ModeNames = { "Password", "Passphrase", "PIN", "Hash" };

        private readonly Binder _binder;
        private readonly Button[] _modeButtons = new Button[4];
        private readonly Panel[] _panels = new Panel[4];
        private readonly ComboBox _presetBox;
        private readonly TextBox _resultBox;
        private readonly Meter _meter;
        private readonly Label _strength, _crack, _notice, _status;
        private readonly Button _copyButton;
        private readonly System.Windows.Forms.Timer _refresh = new System.Windows.Forms.Timer { Interval = 350 };
        private readonly System.Windows.Forms.Timer _clearTimer = new System.Windows.Forms.Timer();
        private readonly RegisteredWaitHandle _showWait, _exitWait;
        private TrackBar _lengthBar, _wordsBar;
        private Label _lengthValue, _wordsValue;
        private CheckBox _symbolsCheck, _numberCheck, _randomLengthCheck;
        private TextBox _symbolSet;
        private ComboBox _numberDigits, _wordMin, _wordMax, _listBox;
        private Button _chooseList;
        private Label _listName;
        private GenResult _last;
        private string _copiedText = "";
        private bool _switching;

        private static Settings Current => Store.Active.Settings;

        private NotifyIcon _tray;
        private bool _hiddenStart;

        public MainWindow(EventWaitHandle showEvent, EventWaitHandle exitEvent, string notice, bool startHidden)
        {
            _hiddenStart = startHidden && Store.MinimizeToTray;
            Text = "Passgen";
            ClientSize = new Size(720, 596);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterScreen;
            Theme.ApplyChrome(this);
            _binder = new Binder(() => Current, SettingsChanged);

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                _modeButtons[i] = Theme.NewButton(ModeNames[i], 16 + i * 118, 18, 112, 30);
                _modeButtons[i].Click += (s, e) => ChooseMode(Settings.Modes[index]);
                Controls.Add(_modeButtons[i]);
            }
            Controls.Add(Theme.NewLabel("Preset", 500, 23, 50, 20, Theme.MutedText));
            _presetBox = Theme.NewDropDown(556, 20, 148);
            _presetBox.SelectedIndexChanged += (s, e) => ChoosePreset();
            Controls.Add(_presetBox);

            var resultCard = new Card();
            resultCard.SetBounds(16, 60, 688, 236);
            _resultBox = Theme.NewText(12, 12, 664, Theme.UiFontMono);
            _resultBox.Multiline = true;
            _resultBox.ReadOnly = true;
            _resultBox.ScrollBars = ScrollBars.Vertical;
            _resultBox.BackColor = Color.FromArgb(246, 248, 250);
            _resultBox.Height = 86;
            _meter = new Meter { Left = 12, Top = 108, Width = 664 };
            _strength = Theme.NewLabel("", 12, 122, 664, 20, null, Theme.UiFontBold);
            _crack = Theme.NewLabel("", 12, 144, 664, 18, Theme.MutedText, Theme.UiFontSmall);
            _notice = Theme.NewLabel("", 12, 164, 664, 34, Theme.WarningFg, Theme.UiFontSmall);
            _notice.AutoEllipsis = false;
            Button generate = Theme.NewButton("Generate", 12, 200, 120, 30, primary: true);
            generate.Click += (s, e) => Guarded(Regenerate);
            _copyButton = Theme.NewButton("Copy", 140, 200, 84, 30);
            _copyButton.Click += (s, e) => Guarded(CopyResult);
            Button check = Theme.NewButton("Check a password", 232, 200, 150, 30);
            check.Click += (s, e) => Guarded(() => new CheckerDialog().ShowDialog(this));
            Button settings = Theme.NewButton("Settings", 390, 200, 96, 30);
            settings.Click += (s, e) => Guarded(OpenSettings);
            resultCard.Controls.AddRange(new Control[] { _resultBox, _meter, _strength, _crack, _notice, generate, _copyButton, check, settings });
            Controls.Add(resultCard);

            var optionsCard = new Card();
            optionsCard.SetBounds(16, 306, 688, 250);
            BuildPasswordPanel(optionsCard);
            BuildPassphrasePanel(optionsCard);
            BuildPinPanel(optionsCard);
            BuildHashPanel(optionsCard);
            Controls.Add(optionsCard);

            Controls.Add(Theme.NewLabel(Installer.VersionLabel, 16, 568, 130, 18, Theme.MutedText, Theme.UiFontSmall));
            _status = Theme.NewLabel(notice, 150, 568, 554, 18, Theme.MutedText, Theme.UiFontSmall);
            _status.TextAlign = ContentAlignment.TopRight;
            Controls.Add(_status);

            _refresh.Tick += (s, e) => { _refresh.Stop(); Store.Save(); Guarded(Regenerate); };
            _clearTimer.Tick += (s, e) => ClearClipboard();
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            if (Store.WindowX != int.MinValue && Screen.AllScreens.Any(screen => screen.WorkingArea.Contains(new Point(Store.WindowX + 40, Store.WindowY + 20))))
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(Store.WindowX, Store.WindowY);
            }
            _showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (state, timedOut) => RunOnWindow(BringForward), null, -1, false);
            _exitWait = ThreadPool.RegisterWaitForSingleObject(exitEvent, (state, timedOut) => RunOnWindow(Close), null, -1, false);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Passgen", null, (s, e) => BringForward());
            menu.Items.Add("Exit", null, (s, e) => Close());
            _tray = new NotifyIcon { Icon = Theme.AppIcon, Text = "Passgen", ContextMenuStrip = menu, Visible = Store.MinimizeToTray };
            _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) BringForward(); };
            Resize += (s, e) => { if (WindowState == FormWindowState.Minimized && Store.MinimizeToTray) Hide(); };

            LoadAll();
            Shown += (s, e) => Guarded(Regenerate);
        }

        // Runs something on the window's own thread, if it still exists.
        private void RunOnWindow(Action action)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(action); }
            catch (InvalidOperationException) { }
        }

        // A second launch asks this window to come to the front.
        private void BringForward()
        {
            _hiddenStart = false;
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        // Keeps a window that was started hidden from appearing, while still
        // creating its handle so it can be shown on request.
        protected override void SetVisibleCore(bool value)
        {
            if (_hiddenStart)
            {
                if (!IsHandleCreated) CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
        }

        // Shows or hides the tray icon to match the setting.
        private void ApplyTray()
        {
            if (_tray != null) _tray.Visible = Store.MinimizeToTray;
        }

        // Event handlers never let an error escape: it goes to the log and a short message.
        private void Guarded(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                AppLog.Write("Error: " + ex);
                _status.Text = "Something went wrong. The details are in the log.";
            }
        }

        // One row of a panel: a label on the left.
        private static Label RowLabel(string text, int y)
        {
            return Theme.NewLabel(text, 0, y + 3, 108, 20);
        }

        // A hint line in small muted text.
        private static Label Hint(string text, int y, int height = 18)
        {
            var label = Theme.NewLabel(text, 0, y, 664, height, Theme.MutedText, Theme.UiFontSmall);
            label.AutoEllipsis = false;
            return label;
        }

        private static Panel NewPanel(Control parent)
        {
            var panel = new Panel { BackColor = Theme.CardBg, Visible = false };
            panel.SetBounds(12, 12, 664, 226);
            parent.Controls.Add(panel);
            return panel;
        }

        // A slider with a value label, added to a panel row.
        private TrackBar AddSlider(Panel panel, string title, int y, int min, int max, out Label value)
        {
            panel.Controls.Add(RowLabel(title, y));
            var bar = new TrackBar { Minimum = min, Maximum = max, TickStyle = TickStyle.None, AutoSize = false, SmallChange = 1, LargeChange = 4 };
            bar.SetBounds(108, y, 420, 28);
            value = Theme.NewLabel("", 540, y + 3, 120, 20, Theme.Text, Theme.UiFontBold);
            panel.Controls.Add(bar);
            panel.Controls.Add(value);
            return bar;
        }

        private void BuildPasswordPanel(Control parent)
        {
            Panel p = _panels[0] = NewPanel(parent);
            _lengthBar = AddSlider(p, "Length", 0, 4, 128, out _lengthValue);
            _binder.Track(_lengthBar, _lengthValue, s => s.PwLength, (s, v) => s.PwLength = v);
            CheckBox lower = Theme.NewCheck("Lowercase a-z", 0, 34, 160), upper = Theme.NewCheck("Uppercase A-Z", 168, 34, 160), digits = Theme.NewCheck("Numbers 0-9", 336, 34, 160);
            _symbolsCheck = Theme.NewCheck("Symbols", 504, 34, 150);
            p.Controls.AddRange(new Control[] { lower, upper, digits, _symbolsCheck });
            _binder.Check(lower, s => s.PwLower, (s, v) => s.PwLower = v);
            _binder.Check(upper, s => s.PwUpper, (s, v) => s.PwUpper = v);
            _binder.Check(digits, s => s.PwDigits, (s, v) => s.PwDigits = v);
            _binder.Check(_symbolsCheck, s => s.PwSymbols, (s, v) => s.PwSymbols = v);
            p.Controls.Add(RowLabel("Symbols used", 66));
            _symbolSet = Theme.NewText(108, 66, 300, new Font("Consolas", 9.5f));
            p.Controls.Add(_symbolSet);
            _binder.Text(_symbolSet, s => s.PwSymbolSet, (s, v) => s.PwSymbolSet = v);
            CheckBox music = Theme.NewCheck("Music ♩ ♫ ♯", 0, 100, 200), chess = Theme.NewCheck("Chess ♔ ♞", 210, 100, 200), emoji = Theme.NewCheck("Emoji \U0001F600", 420, 100, 200);
            p.Controls.AddRange(new Control[] { music, chess, emoji });
            _binder.Check(music, s => s.PackMusic, (s, v) => s.PackMusic = v);
            _binder.Check(chess, s => s.PackChess, (s, v) => s.PackChess = v);
            _binder.Check(emoji, s => s.PackEmoji, (s, v) => s.PackEmoji = v);
            CheckBox ambiguous = Theme.NewCheck("Avoid look-alikes (I l 1 O 0)", 0, 130, 280);
            p.Controls.Add(ambiguous);
            _binder.Check(ambiguous, s => s.AvoidAmbiguous, (s, v) => s.AvoidAmbiguous = v);
            p.Controls.Add(Theme.NewLabel("Group into blocks", 330, 133, 130, 20));
            ComboBox group = Theme.NewDropDown(466, 130, 150, "No grouping", "Blocks of 3", "Blocks of 4", "Blocks of 5", "Blocks of 6");
            p.Controls.Add(group);
            _binder.ComboInt(group, new[] { 0, 3, 4, 5, 6 }, s => s.Group, (s, v) => s.Group = v);
            p.Controls.Add(Hint("Music, chess and emoji characters are rejected by many websites. Use them only where they are accepted.", 168));
        }

        private void BuildPassphrasePanel(Control parent)
        {
            Panel p = _panels[1] = NewPanel(parent);
            _wordsBar = AddSlider(p, "Words", 0, 3, 12, out _wordsValue);
            _binder.Track(_wordsBar, _wordsValue, s => s.Words, (s, v) => s.Words = v);
            p.Controls.Add(RowLabel("Separator", 36));
            ComboBox separator = Theme.NewDropDown(108, 34, 160, "Hyphen -", "Dot .", "Underscore _", "Space", "None", "Random symbol");
            p.Controls.Add(separator);
            _binder.Combo(separator, new[] { "-", ".", "_", " ", "", "random" }, s => s.Separator, (s, v) => s.Separator = v);
            p.Controls.Add(Theme.NewLabel("Word length", 300, 37, 90, 20));
            string[] lengthItems = { "Any", "3", "4", "5", "6", "7", "8", "9", "10" };
            int[] lengthValues = { 0, 3, 4, 5, 6, 7, 8, 9, 10 };
            _wordMin = Theme.NewDropDown(394, 34, 70, lengthItems);
            _wordMax = Theme.NewDropDown(496, 34, 70, lengthItems);
            p.Controls.AddRange(new Control[] { _wordMin, Theme.NewLabel("to", 470, 37, 22, 20), _wordMax });
            _binder.ComboInt(_wordMin, lengthValues, s => s.WordMin, (s, v) => s.WordMin = v);
            _binder.ComboInt(_wordMax, lengthValues, s => s.WordMax, (s, v) => s.WordMax = v);
            _randomLengthCheck = Theme.NewCheck("Random word length", 0, 70, 190);
            CheckBox capitalise = Theme.NewCheck("Capitalise words", 200, 70, 170), profanity = Theme.NewCheck("Filter profanities", 380, 70, 180);
            p.Controls.AddRange(new Control[] { _randomLengthCheck, capitalise, profanity });
            _binder.Check(_randomLengthCheck, s => s.RandomLength, (s, v) => s.RandomLength = v);
            _binder.Check(capitalise, s => s.Capitalize, (s, v) => s.Capitalize = v);
            _binder.Check(profanity, s => s.FilterProfanity, (s, v) => s.FilterProfanity = v);
            _numberCheck = Theme.NewCheck("Add a number at the end", 0, 100, 210);
            _numberDigits = Theme.NewDropDown(216, 98, 160, "2 digits", "3 digits", "4 digits", "5 digits", "6 digits", "7 digits", "8 digits", "9 digits", "10 digits", "Random 2 to 10");
            p.Controls.AddRange(new Control[] { _numberCheck, _numberDigits });
            _binder.Check(_numberCheck, s => s.AddNumber, (s, v) => s.AddNumber = v);
            _binder.ComboInt(_numberDigits, new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 0 }, s => s.NumberDigits, (s, v) => s.NumberDigits = v);
            p.Controls.Add(RowLabel("Words to exclude", 134));
            TextBox blocked = Theme.NewText(116, 132, 540);
            p.Controls.Add(blocked);
            _binder.Text(blocked, s => s.Blocked, (s, v) => s.Blocked = v);
            p.Controls.Add(Hint("Separate with commas. A word of 4 or more letters also removes every word containing it.", 160));
            p.Controls.Add(RowLabel("Word list", 190));
            _listBox = Theme.NewDropDown(108, 188, 200, "Common words", "Large list", "Short, easy words", "My own list");
            _chooseList = Theme.NewButton("Choose file...", 316, 187, 110, 28);
            _chooseList.Click += (s, e) => Guarded(ChooseWordFile);
            _listName = Theme.NewLabel("", 434, 192, 226, 20, Theme.MutedText, Theme.UiFontSmall);
            p.Controls.AddRange(new Control[] { _listBox, _chooseList, _listName });
            _binder.Combo(_listBox, Settings.WordLists, s => s.WordList, (s, v) => s.WordList = v);
        }

        private void BuildPinPanel(Control parent)
        {
            Panel p = _panels[2] = NewPanel(parent);
            Label value;
            TrackBar bar = AddSlider(p, "Length", 0, 4, 16, out value);
            _binder.Track(bar, value, s => s.PinLength, (s, v) => s.PinLength = v);
            p.Controls.Add(RowLabel("Characters", 36));
            ComboBox type = Theme.NewDropDown(108, 34, 260, "Numbers only", "Numbers and capital letters");
            p.Controls.Add(type);
            _binder.Combo(type, Settings.PinTypes, s => s.PinType, (s, v) => s.PinType = v);
            p.Controls.Add(Hint("A PIN is short by design, so it always rates as weak. Use one only where a site or device insists on it.", 76));
        }

        private void BuildHashPanel(Control parent)
        {
            Panel p = _panels[3] = NewPanel(parent);
            Label sizeValue;
            TrackBar size = AddSlider(p, "Size", 0, 0, Settings.HashSizes.Length - 1, out sizeValue);
            sizeValue.Width = 220;
            size.LargeChange = 1;
            size.TickStyle = TickStyle.BottomRight;
            size.TickFrequency = 1;
            size.Height = 40;
            _binder.Track(size, sizeValue,
                s => Math.Max(0, Array.IndexOf(Settings.HashSizes, s.HashBits)),
                (s, v) => s.HashBits = Settings.HashSizes[v],
                v => Settings.HashSizes[v] + " bits (" + (Settings.HashSizes[v] / 8) + " bytes)");
            p.Controls.Add(RowLabel("Format", 46));
            ComboBox format = Theme.NewDropDown(108, 44, 260, "Hex", "Base64", "Base64 URL-safe");
            p.Controls.Add(format);
            _binder.Combo(format, Settings.HashFormats, s => s.HashFormat, (s, v) => s.HashFormat = v);
            p.Controls.Add(Hint("A random value the size of a hash, for keys, secrets and salts. It is not a hash of anything.", 84));
        }

        // Fills every control from the selected preset and shows the right panel.
        private void LoadAll()
        {
            _switching = true;
            try
            {
                _presetBox.Items.Clear();
                foreach (Preset preset in Store.Presets) _presetBox.Items.Add(preset.Name);
                _presetBox.SelectedIndex = Store.Presets.IndexOf(Store.Active);
                _binder.Load();
                ShowMode();
                SyncEnabled();
            }
            finally { _switching = false; }
        }

        // Highlights the current type and shows only its options.
        private void ShowMode()
        {
            for (int i = 0; i < 4; i++)
            {
                bool on = Settings.Modes[i] == Current.Mode;
                Theme.StyleButton(_modeButtons[i]);
                if (on)
                {
                    _modeButtons[i].BackColor = Theme.CurrentBg;
                    _modeButtons[i].ForeColor = Theme.CurrentFg;
                    _modeButtons[i].FlatAppearance.BorderColor = Theme.CurrentFg;
                    _modeButtons[i].Font = Theme.UiFontBold;
                }
                _panels[i].Visible = on;
            }
        }

        // Turns off options that have no effect with the current choices.
        private void SyncEnabled()
        {
            Settings s = Current;
            _numberDigits.Enabled = s.AddNumber;
            _symbolSet.Enabled = s.PwSymbols;
            _randomLengthCheck.Enabled = !(s.WordMin > 0 && s.WordMin == s.WordMax);
            _chooseList.Visible = _listName.Visible = s.WordList == "custom";
            _listName.Text = Store.CustomListPath.Length > 0 ? Path.GetFileName(Store.CustomListPath) : "No file chosen";
        }

        private void ChooseMode(string mode)
        {
            Current.Mode = mode;
            ShowMode();
            SettingsChanged();
        }

        private void ChoosePreset()
        {
            if (_switching || _presetBox.SelectedIndex < 0) return;
            Store.ActiveId = Store.Presets[_presetBox.SelectedIndex].Id;
            LoadAll();
            SettingsChanged();
        }

        // Called after any option changes. Saves and makes a fresh result
        // once the user pauses.
        private void SettingsChanged()
        {
            SyncEnabled();
            _refresh.Stop();
            _refresh.Start();
        }

        // Asks for a word list file and checks it has enough words.
        private void ChooseWordFile()
        {
            using (var dialog = new OpenFileDialog { Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*", Title = "Choose a word list" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                List<string> words = Words.ReadFile(dialog.FileName);
                if (words.Count < Generator.MinCustomWords)
                {
                    MessageBox.Show("That list has only " + words.Count + " different words. It needs at least " + Generator.MinCustomWords + " to be safe. Pick a bigger list, or one word per line.", "Passgen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Store.CustomListPath = dialog.FileName;
                SettingsChanged();
            }
        }

        // Makes a fresh result and shows how strong it is.
        private void Regenerate()
        {
            Settings s = Current;
            Cursor = Cursors.WaitCursor;
            try
            {
                GenResult result = Generator.Generate(s, s.Mode == "passphrase" ? Words.Active(s) : null);
                _last = result;
                _resultBox.Text = string.Join(Environment.NewLine, result.Items);
                Rating rating = Strength.Of(result.Bits);
                _meter.Level = rating.Level;
                _strength.Text = rating.Label + " - " + (int)Math.Floor(result.Bits) + " bits (" + result.Detail + ")";
                _strength.ForeColor = Meter.ColourFor(rating.Level);
                _crack.Text = "Could take " + Strength.CrackTime(result.Bits) + " to crack at 10 billion guesses a second.";
                var notes = new List<string>();
                if (result.Adjusted != null) notes.Add(result.Adjusted);
                if (result.Suggest != null) notes.Add(result.Suggest);
                if (result.Short) notes.Add("PINs are short by design. Use one only where a site insists on it.");
                _notice.ForeColor = Theme.WarningFg;
                _notice.Text = string.Join(" ", notes);
                _copyButton.Enabled = true;
                ShowUsed(result.Used);
            }
            catch (RuleException ex)
            {
                _last = null;
                _resultBox.Text = "";
                _meter.Level = -1;
                _strength.Text = "";
                _crack.Text = "";
                _notice.ForeColor = Theme.ErrorFg;
                _notice.Text = ex.Message;
                _copyButton.Enabled = false;
            }
            finally { Cursor = Cursors.Default; }
        }

        // When a site rule changed the length or word count, say what was used.
        private void ShowUsed(int used)
        {
            if (used > 0 && Current.Mode == "password" && used != _lengthBar.Value) _lengthValue.Text = _lengthBar.Value + " (using " + used + ")";
            if (used > 0 && Current.Mode == "passphrase" && used != _wordsBar.Value) _wordsValue.Text = _wordsBar.Value + " (using " + used + ")";
        }

        // Puts the result on the clipboard, kept out of clipboard history,
        // and clears it again after the chosen time if it is still there.
        private void CopyResult()
        {
            if (_last == null) return;
            string text = _resultBox.Text;
            try
            {
                var data = new DataObject();
                data.SetText(text);
                data.SetData("ExcludeClipboardContentFromMonitorProcessing", false, new MemoryStream(new byte[] { 1, 0, 0, 0 }));
                Clipboard.SetDataObject(data, true, 5, 100);
            }
            catch (ExternalException)
            {
                _status.Text = "Another program is using the clipboard. Try again.";
                return;
            }
            _clearTimer.Stop();
            int seconds = Current.ClearSeconds;
            if (seconds > 0)
            {
                _copiedText = text;
                _clearTimer.Interval = seconds * 1000;
                _clearTimer.Start();
                _status.Text = "Copied. The clipboard clears in " + (seconds >= 60 && seconds % 60 == 0 ? seconds / 60 + (seconds == 60 ? " minute" : " minutes") : seconds + " seconds") + ".";
            }
            else _status.Text = "Copied.";
        }

        // Empties the clipboard, but only if it still holds what was copied.
        private void ClearClipboard()
        {
            _clearTimer.Stop();
            try
            {
                if (_copiedText.Length > 0 && Clipboard.ContainsText() && Clipboard.GetText() == _copiedText)
                {
                    Clipboard.Clear();
                    _status.Text = "Clipboard cleared.";
                }
            }
            catch (ExternalException) { }
            _copiedText = "";
        }

        private void OpenSettings()
        {
            using (var dialog = new SettingsDialog()) dialog.ShowDialog(this);
            LoadAll();
            ApplyTray();
            Store.Save();
            Regenerate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (_tray != null) _tray.Visible = false;
            if (_clearTimer.Enabled) ClearClipboard();
            if (WindowState == FormWindowState.Normal)
            {
                Store.WindowX = Left;
                Store.WindowY = Top;
            }
            Store.Save();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_showWait != null) _showWait.Unregister(null);
                if (_exitWait != null) _exitWait.Unregister(null);
                if (_tray != null) _tray.Dispose();
                _refresh.Dispose();
                _clearTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Rates a password the user already has. Nothing typed here is saved or sent anywhere.</summary>
    public class CheckerDialog : Form
    {
        private static HashSet<string> _commonWords;
        private readonly TextBox _input;
        private readonly Meter _meter;
        private readonly Label _strength, _crack, _note;

        public CheckerDialog()
        {
            Text = "Check a password";
            ClientSize = new Size(480, 270);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Theme.ApplyChrome(this, true);

            var hint = Theme.NewLabel("Type or paste a password you already use to see a rough strength. It is checked on this computer only and is never saved or sent anywhere.", 16, 18, 448, 40, Theme.MutedText, Theme.UiFontSmall);
            hint.AutoEllipsis = false;
            _input = Theme.NewText(16, 66, 350, Theme.UiFontMono);
            _input.UseSystemPasswordChar = true;
            var show = Theme.NewCheck("Show", 376, 68, 90);
            show.CheckedChanged += (s, e) => _input.UseSystemPasswordChar = !show.Checked;
            _meter = new Meter { Left = 16, Top = 108, Width = 448 };
            _strength = Theme.NewLabel("", 16, 124, 448, 20, null, Theme.UiFontBold);
            _crack = Theme.NewLabel("", 16, 148, 448, 18, Theme.MutedText, Theme.UiFontSmall);
            _note = Theme.NewLabel("", 16, 168, 448, 36, Theme.WarningFg, Theme.UiFontSmall);
            _note.AutoEllipsis = false;
            Button close = Theme.NewButton("Close", 384, 222, 80, 30);
            close.DialogResult = DialogResult.Cancel;
            CancelButton = close;
            Controls.AddRange(new Control[] { hint, _input, show, _meter, _strength, _crack, _note, close });
            _meter.Level = -1;
            _input.TextChanged += (s, e) => Rate();
        }

        // Rates the text as it is typed.
        private void Rate()
        {
            if (_commonWords == null) _commonWords = Words.CommonSet();
            Estimate found = Strength.EstimatePassword(_input.Text, _commonWords);
            if (found == null)
            {
                _meter.Level = -1;
                _strength.Text = _crack.Text = _note.Text = "";
                return;
            }
            Rating rating = Strength.Of(found.Bits);
            _meter.Level = rating.Level;
            _strength.ForeColor = Meter.ColourFor(rating.Level);
            _strength.Text = rating.Label + " - about " + (int)Math.Floor(found.Bits) + " bits";
            _crack.Text = "Could take " + Strength.CrackTime(found.Bits) + " to crack at 10 billion guesses a second.";
            _note.Text = found.Note;
        }
    }

    /// <summary>A small box that asks for one line of text.</summary>
    public static class PromptDialog
    {
        // Returns the text, or null if cancelled.
        public static string Show(IWin32Window owner, string title, string label, string initial)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.ClientSize = new Size(380, 130);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = form.MinimizeBox = false;
                form.StartPosition = FormStartPosition.CenterParent;
                Theme.ApplyChrome(form, true);
                var input = Theme.NewText(16, 46, 348);
                input.MaxLength = 30;
                input.Text = initial;
                var ok = Theme.NewButton("Save", 194, 86, 80, 30, primary: true);
                ok.DialogResult = DialogResult.OK;
                var cancel = Theme.NewButton("Cancel", 284, 86, 80, 30);
                cancel.DialogResult = DialogResult.Cancel;
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                form.Controls.AddRange(new Control[] { Theme.NewLabel(label, 16, 20, 348, 20), input, ok, cancel });
                form.Shown += (s, e) => { input.Focus(); input.SelectAll(); };
                if (form.ShowDialog(owner) != DialogResult.OK) return null;
                string text = input.Text.Trim();
                return text.Length == 0 ? null : text;
            }
        }
    }
}
