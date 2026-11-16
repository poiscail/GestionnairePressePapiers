using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace PressePapiers;

public class Settings
{
    public int Max { get; set; } = 200;
    public Keys Hotkey { get; set; } = Keys.Control | Keys.Shift | Keys.V;
    public bool PasteOnClick { get; set; } = true;
    public bool CaptureImages { get; set; } = true;
    public bool Preview { get; set; } = true;
    public bool ShowMeta { get; set; } = true;
    public bool OpenAtClock { get; set; }
    public string Theme { get; set; } = "Système"; // Système, Clair ou Sombre

    // Système : suit le réglage « mode des applications » de Windows.
    public bool IsDark => Theme switch
    {
        "Sombre" => true,
        "Clair" => false,
        _ => Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")?.GetValue("AppsUseLightTheme") is 0,
    };

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

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Windows 10 20H1 et plus)
    public static void DarkTitleBar(IntPtr hwnd, bool dark) { int v = dark ? 1 : 0; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int)); }

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
    readonly CheckBox paste = Box("Clic / Entrée colle dans l'application (sinon copie seulement)");
    readonly CheckBox images = Box("Enregistrer aussi les images");
    readonly CheckBox preview = Box("Aperçu en grand des images au survol");
    readonly CheckBox meta = Box("Afficher l'heure et l'application sous chaque élément");
    readonly CheckBox clock = Box("Le raccourci ouvre la fenêtre près de l'horloge (sinon sous la souris)");
    readonly ComboBox theme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Margin = new Padding(3, 3, 3, 12) };
    Keys key;

    static CheckBox Box(string text) => new() { Text = text, AutoSize = true };

    public bool StartupChecked => startup.Checked;
    public Settings Result => new()
    {
        Max = (int)max.Value, Hotkey = key, PasteOnClick = paste.Checked, CaptureImages = images.Checked,
        Preview = preview.Checked, ShowMeta = meta.Checked, OpenAtClock = clock.Checked,
        Theme = (string)theme.SelectedItem!,
    };

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
        paste.Checked = s.PasteOnClick;
        images.Checked = s.CaptureImages;
        preview.Checked = s.Preview;
        meta.Checked = s.ShowMeta;
        clock.Checked = s.OpenAtClock;
        clock.Margin = new Padding(3, 3, 3, 12);
        theme.Items.AddRange(new object[] { "Système", "Clair", "Sombre" });
        theme.SelectedItem = theme.Items.Contains(s.Theme) ? s.Theme : "Système";
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
            startup, paste, images, preview, meta, clock,
            new Label { Text = "Nombre d'éléments gardés (hors épinglés) :", AutoSize = true },
            max,
            new Label { Text = "Raccourci pour ouvrir (cliquez puis appuyez sur les touches) :", AutoSize = true },
            hotkey,
            new Label { Text = "Thème :", AutoSize = true },
            theme,
            buttons,
        });
        Controls.Add(root);

        if (s.IsDark)
        {
            ApplyDark(this);
            HandleCreated += (_, _) => Settings.DarkTitleBar(Handle, true);
        }
    }

    // Mode sombre : fond foncé et texte clair sur tous les contrôles.
    static void ApplyDark(Control c)
    {
        c.BackColor = c is TextBox or NumericUpDown or ComboBox or Button ? Color.FromArgb(45, 45, 45) : Color.FromArgb(32, 32, 32);
        c.ForeColor = Color.FromArgb(230, 230, 230);
        if (c is Button b) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90); }
        if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
        foreach (Control child in c.Controls) ApplyDark(child);
    }
}
