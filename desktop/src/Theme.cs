using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Passgen
{
    /// <summary>The shared palette, fonts and control styling. Matches the other tools in the collection.</summary>
    public static class Theme
    {
        public static readonly Font UiFont = new Font("Segoe UI", 9.5f);
        public static readonly Font UiFontBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font UiFontSmall = new Font("Segoe UI", 8.5f);
        public static readonly Font UiFontMono = new Font("Consolas", 11f);

        public static readonly Color WindowBg = Color.FromArgb(231, 234, 239);
        public static readonly Color CardBg = Color.White;
        public static readonly Color Border = Color.FromArgb(188, 194, 202);
        public static readonly Color Text = Color.FromArgb(31, 35, 40);
        public static readonly Color MutedText = Color.FromArgb(107, 114, 128);
        public static readonly Color Accent = Color.FromArgb(43, 108, 223);
        public static readonly Color AccentDark = Color.FromArgb(27, 79, 173);
        public static readonly Color CurrentBg = Color.FromArgb(224, 242, 233);
        public static readonly Color CurrentFg = Color.FromArgb(15, 110, 75);
        public static readonly Color WarningFg = Color.FromArgb(180, 83, 9);
        public static readonly Color ErrorFg = Color.FromArgb(180, 0, 0);

        public static readonly Icon AppIcon = NewAppIcon();

        // The bytes of the app icon, built into the exe. Also written beside
        // the install so shortcuts can show it.
        public static byte[] GetAppIconBytes()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
            using (var copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }

        // The window icon, made from the 32 pixel picture inside the icon file.
        private static Icon NewAppIcon()
        {
            byte[] file = GetAppIconBytes();
            int count = BitConverter.ToUInt16(file, 4);
            for (int i = 0; i < count; i++)
            {
                int entry = 6 + i * 16;
                if (file[entry] != 32) continue;
                int size = BitConverter.ToInt32(file, entry + 8);
                int offset = BitConverter.ToInt32(file, entry + 12);
                using (var stream = new MemoryStream(file, offset, size))
                using (var bitmap = new Bitmap(stream))
                    return Icon.FromHandle(bitmap.GetHicon());
            }
            return SystemIcons.Application;
        }

        // Background, font, icon and the slim accent bar on every window.
        // A dialog gets no taskbar button of its own.
        public static void ApplyChrome(Form form, bool dialog = false)
        {
            if (dialog) form.ShowInTaskbar = false;
            form.BackColor = WindowBg;
            form.Font = UiFont;
            form.Icon = AppIcon;
            var bar = new Panel { Height = 4, Dock = DockStyle.Top, BackColor = Accent };
            form.Controls.Add(bar);
            bar.BringToFront();
        }

        // Flat buttons: one primary action, warning only for destructive ones.
        public static void StyleButton(Button button, bool primary = false, bool warning = false)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.Font = UiFont;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.BorderSize = 1;
            if (primary)
            {
                button.BackColor = Accent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Accent;
                button.FlatAppearance.MouseOverBackColor = AccentDark;
                button.FlatAppearance.MouseDownBackColor = AccentDark;
            }
            else if (warning)
            {
                button.BackColor = CardBg;
                button.ForeColor = WarningFg;
                button.FlatAppearance.BorderColor = WarningFg;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 247, 237);
            }
            else
            {
                button.BackColor = CardBg;
                button.ForeColor = Text;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = WindowBg;
            }
        }

        // Builds a styled button at a position.
        public static Button NewButton(string text, int x, int y, int width, int height, bool primary = false, bool warning = false)
        {
            var button = new Button { Text = text };
            button.SetBounds(x, y, width, height);
            StyleButton(button, primary, warning);
            return button;
        }

        // Builds a plain label at a position.
        public static Label NewLabel(string text, int x, int y, int width, int height, Color? color = null, Font font = null)
        {
            var label = new Label { Text = text, ForeColor = color ?? Text, AutoEllipsis = true };
            label.SetBounds(x, y, width, height);
            if (font != null) label.Font = font;
            return label;
        }

        // A tick box at a position.
        public static CheckBox NewCheck(string text, int x, int y, int width)
        {
            var box = new CheckBox { Text = text, ForeColor = Text, AutoSize = false, FlatStyle = FlatStyle.System };
            box.SetBounds(x, y, width, 22);
            return box;
        }

        // A drop-down list at a position, filled from the given items.
        public static ComboBox NewDropDown(int x, int y, int width, params string[] items)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = CardBg };
            box.Items.AddRange(items);
            box.SetBounds(x, y, width, 26);
            return box;
        }

        // A text box at a position.
        public static TextBox NewText(int x, int y, int width, Font font = null)
        {
            var box = new TextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = CardBg, ForeColor = Text };
            if (font != null) box.Font = font;
            box.SetBounds(x, y, width, 26);
            return box;
        }

        // A small number box with a range.
        public static NumericUpDown NewNumber(int x, int y, int width, int min, int max)
        {
            var box = new NumericUpDown { Minimum = min, Maximum = max, BorderStyle = BorderStyle.FixedSingle };
            box.SetBounds(x, y, width, 26);
            return box;
        }
    }

    /// <summary>A white panel with a thin border, used to group controls.</summary>
    public class Card : Panel
    {
        public Card()
        {
            BackColor = Theme.CardBg;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    /// <summary>A thin strength bar that fills and changes colour with the rating.</summary>
    public class Meter : Control
    {
        private int _level = -1;

        public Meter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Height = 8;
        }

        // -1 shows an empty bar, 0 to 4 fills it in steps.
        public int Level
        {
            get { return _level; }
            set { _level = value; Invalidate(); }
        }

        // The colour for each rating.
        public static Color ColourFor(int level)
        {
            switch (level)
            {
                case 0: return Theme.ErrorFg;
                case 1: return Theme.WarningFg;
                case 2: return Color.FromArgb(161, 98, 7);
                default: return Theme.CurrentFg;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(222, 226, 232));
            if (_level < 0) return;
            using (var brush = new SolidBrush(ColourFor(_level)))
                e.Graphics.FillRectangle(brush, 0, 0, Width * (_level + 1) / 5, Height);
        }
    }
}
