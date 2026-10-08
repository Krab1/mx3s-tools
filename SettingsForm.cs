namespace MxBattery;

public sealed class SettingsForm : Form
{
    /// <param name="testScale">Test only: emulate a display scale by scaling the font (what real DPI scaling does).</param>
    public SettingsForm(Settings s, float? testScale = null)
    {
        Text = "MX Battery settings";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        // No WinForms auto-scaling: every size below is a multiple of the font height, and the font is what the
        // OS scales with DPI. One rule, so it cannot disagree with itself at 125/150/200%.
        AutoScaleMode = AutoScaleMode.None;
        if (testScale is { } k) Font = new Font("Segoe UI", 9f * k);
        int U(float v) => (int)Math.Round(v * Font.Height / 15f);                          // 15 px = Segoe UI 9pt at 96 dpi
        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(U(12)), Location = Point.Empty };
        Controls.Add(t);
        Load += (_, _) => { ClientSize = t.GetPreferredSize(Size.Empty); CenterToScreen(); };   // size to content after DPI scaling, never clip the buttons

        NumericUpDown Num(int v, int min, int max) => new() { Minimum = min, Maximum = max, Value = Math.Clamp(v, min, max), Width = U(120) };
        void Row(string label, Control c) { t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(U(3), U(6), U(12), U(3)) }); c.Margin = new Padding(U(3)); t.Controls.Add(c); }

        var low = Num(s.LowPercent, 5, 95);
        var crit = Num(s.CriticalPercent, 1, 50);
        var hyst = Num(s.Hysteresis, 0, 20);
        var green = Num(s.GreenFrom, 2, 100);
        var yellow = Num(s.YellowFrom, 1, 99);
        var poll = Num(s.PollMinutes, 1, 60);
        var remind = Num(s.ReminderMinutes, 0, 240);
        var full = new CheckBox { Checked = s.NotifyFull, AutoSize = true, Anchor = AnchorStyles.Left };
        var auto = new CheckBox { Checked = s.StartWithWindows, AutoSize = true, Anchor = AnchorStyles.Left };
        var updates = new CheckBox { Checked = s.CheckForUpdates, AutoSize = true, Anchor = AnchorStyles.Left };
        var style = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<IconStyle>(), Width = U(120) };
        var pref = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<Preferred>(), Width = U(120) };
        Load += (_, _) => { style.SelectedItem = s.IconStyle; pref.SelectedItem = s.Preferred; };   // binding exists only once shown
        var name = new TextBox { Text = s.DeviceFilter, Width = U(160) };

        Row("Low threshold (%)", low);
        Row("Critical threshold (%)", crit);
        Row("Hysteresis (%)", hyst);
        Row("Icon green from (%)", green);
        Row("Icon yellow from (%, red below)", yellow);
        Row("Poll interval (min)", poll);
        Row("Repeat reminder (min, 0 = off)", remind);
        Row("Notify when fully charged", full);
        Row("Start with Windows", auto);
        Row("Check for updates", updates);
        Row("Icon style", style);
        Row("Preferred connection", pref);
        Row("Device name contains", name);

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(U(80), 0) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(U(80), 0) };
        var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        bar.Controls.AddRange([cancel, ok]);
        t.Controls.Add(new Label()); t.Controls.Add(bar);
        AcceptButton = ok; CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            s.LowPercent = (int)low.Value;
            s.CriticalPercent = Math.Min((int)crit.Value, s.LowPercent);   // critical can't exceed low
            s.Hysteresis = (int)hyst.Value;
            s.GreenFrom = (int)green.Value;
            s.YellowFrom = Math.Min((int)yellow.Value, s.GreenFrom - 1);        // yellow band must sit below green
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
