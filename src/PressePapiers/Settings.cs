using System.Text.Json;
using Microsoft.Win32;

namespace PressePapiers;

public class Settings
{
    public int Max { get; set; } = 200;
    public Keys Hotkey { get; set; } = Keys.Control | Keys.Shift | Keys.V;

    public static Settings Load(string file)
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(file)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException) { return new(); }
    }

    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(this));
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "GestionnairePressePapiers";

    // Lancement au démarrage : valeur dans HKCU\...\Run (pas besoin d'être administrateur).
    public static bool Startup
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(RunName) != null; }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue(RunName, $"\"{Application.ExecutablePath}\"");
            else k.DeleteValue(RunName, false);
        }
    }

    public static string HotkeyText(Keys k) => string.Join("+", new[]
    {
        (k & Keys.Control) != 0 ? "Ctrl" : null,
        (k & Keys.Shift) != 0 ? "Maj" : null,
        (k & Keys.Alt) != 0 ? "Alt" : null,
        (k & Keys.KeyCode).ToString(),
    }.Where(x => x != null));
}

public class SettingsForm : Form
{
    readonly CheckBox startup = new() { Text = "Lancer au démarrage de Windows", AutoSize = true, Margin = new Padding(3, 3, 3, 12) };
    readonly NumericUpDown max = new() { Minimum = 10, Maximum = 5000, Increment = 50, Width = 90, Margin = new Padding(3, 3, 3, 12) };
    readonly TextBox hotkey = new() { ReadOnly = true, Width = 180, Margin = new Padding(3, 3, 3, 12) };
    Keys key;

    public bool StartupChecked => startup.Checked;
    public Settings Result => new() { Max = (int)max.Value, Hotkey = key };

    public SettingsForm(Settings s, bool startupOn)
    {
        Text = "Paramètres";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 10);

        startup.Checked = startupOn;
        max.Value = Math.Clamp(s.Max, 10, 5000);
        key = s.Hotkey;
        hotkey.Text = Settings.HotkeyText(key);
        // Cliquer dans la case puis appuyer sur la combinaison voulue (au moins Ctrl, Maj ou Alt).
        hotkey.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu || e.Modifiers == Keys.None) return;
            key = e.KeyData;
            hotkey.Text = Settings.HotkeyText(key);
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = ok;
        CancelButton = cancel;
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.AddRange(new Control[] { cancel, ok });

        var root = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12), WrapContents = false };
        root.Controls.AddRange(new Control[]
        {
            startup,
            new Label { Text = "Nombre d'éléments gardés (hors épinglés) :", AutoSize = true },
            max,
            new Label { Text = "Raccourci pour ouvrir (cliquez puis appuyez sur les touches) :", AutoSize = true },
            hotkey,
            buttons,
        });
        Controls.Add(root);
    }
}
