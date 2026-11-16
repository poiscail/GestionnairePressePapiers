using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PressePapiers;

public class MainForm : Form
{
    [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);

    const int WM_CLIPBOARDUPDATE = 0x031D, WM_HOTKEY = 0x0312, HOTKEY_ID = 1;
    const uint MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000, VK_V = 0x56;

    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GestionnairePressePapiers");
    static readonly string ImageDir = Path.Combine(DataDir, "images");

    readonly History history = new(Path.Combine(DataDir, "historique.json"));
    readonly TextBox search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
    readonly NotifyIcon tray = new() { Text = "Presse-papiers (Ctrl+Maj+V)", Icon = SystemIcons.Application, Visible = true };
    readonly ToolTip tip = new();
    bool allowShow;
    IntPtr previousWindow;

    public MainForm()
    {
        Text = "Presse-papiers";
        Size = new Size(420, 480);
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        Font = new Font("Segoe UI", 10);
        Controls.Add(list);
        Controls.Add(search);

        history.Load();

        search.TextChanged += (_, _) => Refresh_();
        search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); list.SelectedIndex = Math.Min(1, list.Items.Count - 1); e.Handled = true; }
            if (e.KeyCode == Keys.Enter) { PasteSelected(); e.SuppressKeyPress = true; }
        };
        list.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left && list.IndexFromPoint(e.Location) >= 0) PasteSelected(); };
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { PasteSelected(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Delete) DeleteSelected();
        };
        list.MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) { int i = list.IndexFromPoint(e.Location); if (i >= 0) list.SelectedIndex = i; } };
        list.MouseMove += (_, e) =>
        {
            int i = list.IndexFromPoint(e.Location);
            var t = i >= 0 && list.Items[i] is Item it && it.Kind == Kind.Text ? it.Text : "";
            if (t.Length > 1000) t = t[..1000] + "…";
            if (tip.GetToolTip(list) != t) tip.SetToolTip(list, t);
        };

        var itemMenu = new ContextMenuStrip();
        itemMenu.Items.Add("Épingler / désépingler", null, (_, _) => TogglePin());
        itemMenu.Items.Add("Supprimer", null, (_, _) => DeleteSelected());
        list.ContextMenuStrip = itemMenu;

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Ouvrir (Ctrl+Maj+V)", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("Vider l'historique (garde les épinglés)", null, (_, _) => { DeleteImages(history.ClearUnpinned()); SaveAndRefresh(); });
        trayMenu.Items.Add("Quitter", null, (_, _) => { tray.Visible = false; Application.Exit(); });
        tray.ContextMenuStrip = trayMenu;
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };

        Deactivate += (_, _) => Hide();
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        Refresh_();
    }

    // La fenêtre reste cachée au démarrage, mais son handle doit exister pour recevoir les messages.
    protected override void SetVisibleCore(bool value)
    {
        if (!allowShow) { value = false; if (!IsHandleCreated) CreateHandle(); }
        base.SetVisibleCore(value);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AddClipboardFormatListener(Handle);
        if (!RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_V))
            tray.ShowBalloonTip(5000, "Presse-papiers", "Ctrl+Maj+V est déjà pris par une autre application. Cliquez sur l'icône pour ouvrir.", ToolTipIcon.Warning);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        RemoveClipboardFormatListener(Handle);
        UnregisterHotKey(Handle, HOTKEY_ID);
        tray.Visible = false;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE) CaptureClipboard();
        else if (m.Msg == WM_HOTKEY) ShowWindow();
        base.WndProc(ref m);
    }

    void CaptureClipboard()
    {
        // Le presse-papiers peut être verrouillé un court instant par l'application qui copie.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Item? it = null;
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text)) it = new Item { Kind = Kind.Text, Text = text, Key = text };
                }
                else if (Clipboard.ContainsImage())
                {
                    using var img = Clipboard.GetImage();
                    if (img != null) it = SaveImage(img);
                }
                if (it != null) { DeleteImages(history.Add(it)); SaveAndRefresh(); }
                return;
            }
            catch (ExternalException) { Thread.Sleep(50); }
        }
    }

    static Item SaveImage(Image img)
    {
        using var ms = new MemoryStream();
        img.Save(ms, ImageFormat.Png);
        var hash = Convert.ToHexString(SHA256.HashData(ms.ToArray()));
        Directory.CreateDirectory(ImageDir);
        var path = Path.Combine(ImageDir, hash + ".png");
        if (!File.Exists(path)) File.WriteAllBytes(path, ms.ToArray());
        return new Item { Kind = Kind.Image, Text = $"Image {img.Width}×{img.Height}", Key = hash };
    }

    static void DeleteImages(IEnumerable<Item> removed)
    {
        foreach (var it in removed.Where(x => x.Kind == Kind.Image))
            try { File.Delete(Path.Combine(ImageDir, it.Key + ".png")); } catch (IOException) { }
    }

    void ShowWindow()
    {
        var fg = GetForegroundWindow();
        if (fg != Handle) previousWindow = fg;
        var cur = Cursor.Position;
        var wa = Screen.FromPoint(cur).WorkingArea;
        Location = new Point(Math.Clamp(cur.X, wa.Left, wa.Right - Width), Math.Clamp(cur.Y, wa.Top, wa.Bottom - Height));
        allowShow = true;
        search.Text = "";
        Refresh_();
        Show();
        Activate();
        search.Focus();
    }

    async void PasteSelected()
    {
        if (list.SelectedItem is not Item it) return;
        try
        {
            if (it.Kind == Kind.Text) Clipboard.SetText(it.Text);
            else
            {
                using var ms = new MemoryStream(File.ReadAllBytes(Path.Combine(ImageDir, it.Key + ".png")));
                using var img = Image.FromStream(ms);
                Clipboard.SetImage(img);
            }
        }
        catch (Exception ex) when (ex is ExternalException or IOException)
        {
            MessageBox.Show("Impossible de copier cet élément : " + ex.Message, "Presse-papiers");
            return;
        }
        Hide();
        if (previousWindow == IntPtr.Zero) return;
        SetForegroundWindow(previousWindow);
        await Task.Delay(80); // laisse la fenêtre cible reprendre le focus avant Ctrl+V
        SendKeys.SendWait("^v");
    }

    void TogglePin()
    {
        if (list.SelectedItem is not Item it) return;
        it.Pinned = !it.Pinned;
        SaveAndRefresh();
    }

    void DeleteSelected()
    {
        if (list.SelectedItem is not Item it) return;
        history.Items.Remove(it);
        DeleteImages(new[] { it });
        SaveAndRefresh();
    }

    void SaveAndRefresh()
    {
        try { history.Save(); }
        catch (IOException ex) { tray.ShowBalloonTip(3000, "Presse-papiers", "Sauvegarde impossible : " + ex.Message, ToolTipIcon.Error); }
        Refresh_();
    }

    void Refresh_()
    {
        var sel = list.SelectedItem;
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var it in history.View(search.Text.Trim())) list.Items.Add(it);
        if (sel != null && list.Items.Contains(sel)) list.SelectedItem = sel;
        else if (list.Items.Count > 0) list.SelectedIndex = 0;
        list.EndUpdate();
    }
}
