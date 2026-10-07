namespace MxBattery;

public sealed class SettingsForm : Form
{
    public SettingsForm(Settings s)
    {
        Text = "MX Battery settings";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new(12), Dock = DockStyle.Fill };
        Controls.Add(t);

        NumericUpDown Num(int v, int min, int max) => new() { Minimum = min, Maximum = max, Value = Math.Clamp(v, min, max), Width = 120 };
        void Row(string label, Control c) { t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new(3, 6, 12, 3) }); t.Controls.Add(c); }

        var low = Num(s.LowPercent, 5, 95);
        var crit = Num(s.CriticalPercent, 1, 50);
        var hyst = Num(s.Hysteresis, 0, 20);
        var poll = Num(s.PollMinutes, 1, 60);
        var remind = Num(s.ReminderMinutes, 0, 240);
        var full = new CheckBox { Checked = s.NotifyFull, AutoSize = true };
        var auto = new CheckBox { Checked = s.StartWithWindows, AutoSize = true };
        var updates = new CheckBox { Checked = s.CheckForUpdates, AutoSize = true };
        var style = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<IconStyle>(), Width = 120 };
        var pref = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<Preferred>(), Width = 120 };
        Load += (_, _) => { style.SelectedItem = s.IconStyle; pref.SelectedItem = s.Preferred; };   // binding exists only once shown
        var name = new TextBox { Text = s.DeviceFilter, Width = 160 };

        Row("Low threshold (%)", low);
        Row("Critical threshold (%)", crit);
        Row("Hysteresis (%)", hyst);
        Row("Poll interval (min)", poll);
        Row("Repeat reminder (min, 0 = off)", remind);
        Row("Notify when fully charged", full);
        Row("Start with Windows", auto);
        Row("Check for updates", updates);
        Row("Icon style", style);
        Row("Preferred connection", pref);
        Row("Device name contains", name);

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        bar.Controls.AddRange([cancel, ok]);
        t.Controls.Add(new Label()); t.Controls.Add(bar);
        AcceptButton = ok; CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            s.LowPercent = (int)low.Value;
            s.CriticalPercent = Math.Min((int)crit.Value, s.LowPercent);   // critical can't exceed low
            s.Hysteresis = (int)hyst.Value;
            s.PollMinutes = (int)poll.Value;
            s.ReminderMinutes = (int)remind.Value;
            s.NotifyFull = full.Checked;
            s.StartWithWindows = auto.Checked;
            s.CheckForUpdates = updates.Checked;
            s.IconStyle = (IconStyle)style.SelectedItem!;
            s.Preferred = (Preferred)pref.SelectedItem!;
            s.DeviceFilter = name.Text.Trim().Length > 0 ? name.Text.Trim() : "MX Master 3S";
            s.Save();
        };
    }
}
