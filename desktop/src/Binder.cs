using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Passgen
{
    /// <summary>
    /// Ties controls to one preset's options. Loading fills every control
    /// from the preset, and a change by the user writes straight back into
    /// it, so there is no save button anywhere.
    /// </summary>
    public class Binder
    {
        private readonly Func<Settings> _current;
        private readonly Action _changed;
        private readonly List<Action> _loaders = new List<Action>();
        private bool _loading;

        public Binder(Func<Settings> current, Action changed)
        {
            _current = current;
            _changed = changed;
        }

        // True while controls are being filled, so that does not count as a change.
        public bool Loading => _loading;

        // Fills every bound control from the current preset.
        public void Load()
        {
            _loading = true;
            try { foreach (Action loader in _loaders) loader(); }
            finally { _loading = false; }
        }

        private void Edited(Action<Settings> apply)
        {
            if (_loading) return;
            apply(_current());
            _changed();
        }

        public void Check(CheckBox box, Func<Settings, bool> get, Action<Settings, bool> set)
        {
            _loaders.Add(() => box.Checked = get(_current()));
            box.CheckedChanged += (sender, e) => Edited(s => set(s, box.Checked));
        }

        public void Text(TextBox box, Func<Settings, string> get, Action<Settings, string> set)
        {
            _loaders.Add(() => box.Text = get(_current()));
            box.TextChanged += (sender, e) => Edited(s => set(s, box.Text));
        }

        public void Number(NumericUpDown box, Func<Settings, int> get, Action<Settings, int> set)
        {
            _loaders.Add(() => box.Value = Math.Min(box.Maximum, Math.Max(box.Minimum, get(_current()))));
            box.ValueChanged += (sender, e) => Edited(s => set(s, (int)box.Value));
        }

        // A drop-down whose items match the given values in order.
        public void Combo(ComboBox box, string[] values, Func<Settings, string> get, Action<Settings, string> set)
        {
            _loaders.Add(() => box.SelectedIndex = Math.Max(0, Array.IndexOf(values, get(_current()))));
            box.SelectedIndexChanged += (sender, e) => Edited(s => { if (box.SelectedIndex >= 0) set(s, values[box.SelectedIndex]); });
        }

        public void ComboInt(ComboBox box, int[] values, Func<Settings, int> get, Action<Settings, int> set)
        {
            _loaders.Add(() => box.SelectedIndex = Math.Max(0, Array.IndexOf(values, get(_current()))));
            box.SelectedIndexChanged += (sender, e) => Edited(s => { if (box.SelectedIndex >= 0) set(s, values[box.SelectedIndex]); });
        }

        // A slider with a label beside it that shows the value.
        public void Track(TrackBar bar, Label value, Func<Settings, int> get, Action<Settings, int> set)
        {
            _loaders.Add(() =>
            {
                bar.Value = Math.Min(bar.Maximum, Math.Max(bar.Minimum, get(_current())));
                value.Text = bar.Value.ToString();
            });
            bar.ValueChanged += (sender, e) =>
            {
                value.Text = bar.Value.ToString();
                Edited(s => set(s, bar.Value));
            };
        }
    }
}
