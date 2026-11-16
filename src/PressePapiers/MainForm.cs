using System.ComponentModel;
using System.Diagnostics;
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
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    const int WM_CLIPBOARDUPDATE = 0x031D, WM_HOTKEY = 0x0312, HOTKEY_ID = 1;
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GestionnairePressePapiers");
    static readonly string ImageDir = Path.Combine(DataDir, "images");
    static readonly string SettingsFile = Path.Combine(DataDir, "parametres.json");

    readonly Settings settings = Settings.Load(SettingsFile);
    readonly History history;
    readonly TextBox search = new() { Dock = DockStyle.Fill, PlaceholderText = "Rechercher…" };
    readonly Button gear = new() { Text = "⚙", Dock = DockStyle.Right, Width = 34, FlatStyle = FlatStyle.Flat, TabStop = false };
    readonly Font small = new("Segoe UI", 8);
    readonly PreviewForm preview = new();
    string? previewKey;
    readonly ToolStripMenuItem trayOpen = new();
    readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, SelectionMode = SelectionMode.MultiExtended, DrawMode = DrawMode.OwnerDrawVariable };
    const int ThumbHeight = 90;
    readonly Dictionary<string, Image?> thumbs = new();
    readonly NotifyIcon tray = new() { Icon = AppIcon, Visible = true };
    static readonly Icon AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
    readonly ToolTip tip = new() { AutoPopDelay = 30000 };
    bool allowShow, inDialog;
    IntPtr previousWindow;

    public MainForm()
    {
        Text = "Presse-papiers";
        Icon = AppIcon;
        Size = new Size(420, 480);
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        Font = new Font("Segoe UI", 10);
        var top = new Panel { Dock = DockStyle.Top };
        top.Controls.Add(search);
        top.Controls.Add(gear);
        Controls.Add(list);
        Controls.Add(top);
        top.Height = search.PreferredHeight;
        gear.FlatAppearance.BorderSize = 0;
        gear.Click += (_, _) => OpenSettings();
        tip.SetToolTip(gear, "Paramètres");

        history = new(Path.Combine(DataDir, "historique.json"), settings.Max);
        history.Load();

        search.TextChanged += (_, _) => Refresh_();
        search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); list.ClearSelected(); list.SetSelected(Math.Min(1, list.Items.Count - 1), true); e.Handled = true; }
            if (e.KeyCode == Keys.Enter) { PasteSelected(); e.SuppressKeyPress = true; }
        };
        // Ctrl/Maj + clic = sélection multiple, pas de collage
        list.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left && ModifierKeys == Keys.None && ItemAt(e.Location) >= 0) PasteSelected(); };
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { PasteSelected(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Delete) DeleteSelected();
        };
        list.MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) { int i = ItemAt(e.Location); if (i >= 0 && !list.GetSelected(i)) { list.ClearSelected(); list.SetSelected(i, true); } } };
        list.MeasureItem += (_, e) =>
            e.ItemHeight = (list.Items[e.Index] is Item { Kind: Kind.Image } ? ThumbHeight + 18 : list.Font.Height + 14) + small.Height;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            var it = (Item)list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var color = selected ? SystemColors.HighlightText : it.Link != null ? Color.RoyalBlue : list.ForeColor;
            var b = e.Bounds;
            const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            int metaY;
            if (it.Kind == Kind.Image && Thumb(it) is Image t)
            {
                int x = b.X + 4;
                if (it.Pinned) { TextRenderer.DrawText(e.Graphics, "📌", list.Font, new Point(x, b.Y + 4), color); x += 26; }
                e.Graphics.DrawImage(t, x, b.Y + 7, t.Width, t.Height);
                e.Graphics.DrawRectangle(SystemPens.ControlDark, x, b.Y + 7, t.Width - 1, t.Height - 1);
                metaY = b.Y + 9 + t.Height;
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, it.ToString(), list.Font, new Rectangle(b.X + 8, b.Y + 5, b.Width - 16, list.Font.Height), color, flags);
                metaY = b.Y + 5 + list.Font.Height;
            }
            // Petite ligne grise : heure de la copie et application d'où elle vient
            var when = it.Time.Date == DateTime.Today ? it.Time.ToString("HH:mm") : it.Time.ToString("dd/MM HH:mm");
            TextRenderer.DrawText(e.Graphics, it.Source.Length > 0 ? $"{when} · {it.Source}" : when, small, new Rectangle(b.X + 8, metaY, b.Width - 16, small.Height),
                selected ? SystemColors.HighlightText : SystemColors.GrayText, flags);
            // Ligne de séparation entre éléments
            e.Graphics.DrawLine(SystemPens.ControlLight, b.X + 4, b.Bottom - 1, b.Right - 4, b.Bottom - 1);
        };
        list.MouseMove += (_, e) =>
        {
            int i = ItemAt(e.Location);
            var it = i >= 0 ? (Item)list.Items[i] : null;
            var t = it?.Kind == Kind.Text ? it.Text : "";
            if (t.Length > 3000) t = t[..3000] + "…";
            if (tip.GetToolTip(list) != t) tip.SetToolTip(list, t);
            ShowPreview(it);
        };
        list.MouseLeave += (_, _) => HidePreview();
        VisibleChanged += (_, _) => { if (!Visible) HidePreview(); };

        var itemMenu = new ContextMenuStrip();
        var openLink = new ToolStripMenuItem("Ouvrir dans le navigateur", null, (_, _) => OpenLink());
        itemMenu.Items.Add(openLink);
        itemMenu.Opening += (_, _) => { HidePreview(); openLink.Visible = Selected is [{ Link: not null }]; };
        itemMenu.Items.Add("Copier", null, (_, _) => CopySelected());
        itemMenu.Items.Add("Enregistrer sous…", null, (_, _) => SaveSelectedAs());
        itemMenu.Items.Add("Épingler / désépingler", null, (_, _) => TogglePin());
        itemMenu.Items.Add("Supprimer", null, (_, _) => DeleteSelected());
        list.ContextMenuStrip = itemMenu;

        var trayMenu = new ContextMenuStrip();
        trayOpen.Click += (_, _) => ShowWindow();
        trayMenu.Items.Add(trayOpen);
        trayMenu.Items.Add("Vider l'historique (garde les épinglés)", null, (_, _) => { DeleteImages(history.ClearUnpinned()); SaveAndRefresh(); });
        trayMenu.Items.Add("Quitter", null, (_, _) => { tray.Visible = false; Application.Exit(); });
        trayMenu.Items.Add("Paramètres…", null, (_, _) => { ShowWindow(); OpenSettings(); });
        tray.ContextMenuStrip = trayMenu;
        UpdateHotkeyText();
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };

        Deactivate += (_, _) => { if (!inDialog) Hide(); };
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Hide();
            // Ctrl+A : tous les éléments (dans la recherche, seulement si elle est vide, sinon sélection du texte)
            if (e.KeyData == (Keys.Control | Keys.A) && (list.Focused || search.Text.Length == 0))
            {
                list.BeginUpdate();
                for (int i = 0; i < list.Items.Count; i++) list.SetSelected(i, true);
                list.EndUpdate();
                list.Focus();
                e.SuppressKeyPress = true;
            }
        };
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
        if (!RegisterHotkey(settings.Hotkey))
            tray.ShowBalloonTip(5000, "Presse-papiers", $"{Settings.HotkeyText(settings.Hotkey)} est déjà pris par une autre application. Cliquez sur l'icône pour ouvrir.", ToolTipIcon.Warning);
    }

    bool RegisterHotkey(Keys k)
    {
        uint mods = MOD_NOREPEAT | ((k & Keys.Control) != 0 ? MOD_CONTROL : 0) | ((k & Keys.Shift) != 0 ? MOD_SHIFT : 0) | ((k & Keys.Alt) != 0 ? MOD_ALT : 0);
        return RegisterHotKey(Handle, HOTKEY_ID, mods, (uint)(k & Keys.KeyCode));
    }

    void UpdateHotkeyText()
    {
        var k = Settings.HotkeyText(settings.Hotkey);
        tray.Text = $"Presse-papiers ({k})";
        trayOpen.Text = $"Ouvrir ({k})";
    }

    void OpenSettings()
    {
        HidePreview();
        using var dlg = new SettingsForm(settings, Settings.Startup);
        inDialog = true;
        try
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var r = dlg.Result;
            try { Settings.Startup = dlg.StartupChecked; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                MessageBox.Show(this, "Impossible de changer le lancement au démarrage : " + ex.Message, "Presse-papiers");
            }
            if (r.Hotkey != settings.Hotkey)
            {
                UnregisterHotKey(Handle, HOTKEY_ID);
                if (RegisterHotkey(r.Hotkey)) settings.Hotkey = r.Hotkey;
                else
                {
                    RegisterHotkey(settings.Hotkey);
                    MessageBox.Show(this, $"{Settings.HotkeyText(r.Hotkey)} est déjà pris par une autre application. Raccourci inchangé.", "Presse-papiers");
                }
            }
            settings.Max = history.Max = r.Max;
            DeleteImages(history.Trim());
            try { settings.Save(SettingsFile); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Enregistrement des paramètres impossible : " + ex.Message, "Presse-papiers");
            }
            UpdateHotkeyText();
            SaveAndRefresh();
        }
        finally { inDialog = false; if (Visible) Activate(); }
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
                if (it != null) it.Source = SourceApp();
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

    // Nom de l'application qui a rempli le presse-papiers ; "" si c'est nous (recopie depuis l'historique).
    static string SourceApp()
    {
        var w = GetClipboardOwner();
        if (w == IntPtr.Zero) w = GetForegroundWindow();
        GetWindowThreadProcessId(w, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return "";
        try
        {
            using var p = Process.GetProcessById((int)pid);
            string? d = null;
            try { d = p.MainModule?.FileVersionInfo.FileDescription; } catch (Win32Exception) { } // processus protégé ou administrateur
            return string.IsNullOrWhiteSpace(d) ? p.ProcessName : d.Trim();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return ""; }
    }

    void OpenLink()
    {
        if (Selected is not [{ Link: Uri u }]) return;
        try { Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true }); Hide(); }
        catch (Win32Exception ex) { tray.ShowBalloonTip(3000, "Presse-papiers", "Impossible d'ouvrir le lien : " + ex.Message, ToolTipIcon.Error); }
    }

    // Aperçu en grand d'une image survolée, à côté de la fenêtre.
    void ShowPreview(Item? it)
    {
        if (it?.Kind != Kind.Image) { HidePreview(); return; }
        if (previewKey == it.Key) return;
        Bitmap img;
        try
        {
            using var ms = new MemoryStream(File.ReadAllBytes(ImagePath(it)));
            using var src = Image.FromStream(ms);
            img = new Bitmap(src);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException) { HidePreview(); return; }
        var wa = Screen.FromControl(this).WorkingArea;
        double s = Math.Min(1.0, Math.Min(Math.Min(800, wa.Width / 2) / (double)img.Width, (wa.Height - 40) / (double)img.Height));
        var size = new Size(Math.Max(1, (int)(img.Width * s)) + 2, Math.Max(1, (int)(img.Height * s)) + 2);
        int x = Right + 8 + size.Width <= wa.Right ? Right + 8 : Math.Max(wa.Left, Left - 8 - size.Width);
        int y = Math.Clamp(Cursor.Position.Y - size.Height / 2, wa.Top, Math.Max(wa.Top, wa.Bottom - size.Height));
        preview.Image = img;
        previewKey = it.Key;
        // Affichage Win32 sans activation : Show() de WinForms vole le focus et la fenêtre principale se cacherait.
        SetWindowPos(preview.Handle, new IntPtr(-1), x, y, size.Width, size.Height, 0x0010 | 0x0040); // HWND_TOPMOST, SWP_NOACTIVATE | SWP_SHOWWINDOW
    }

    void HidePreview()
    {
        if (previewKey == null) return;
        previewKey = null;
        ShowWindow(preview.Handle, 0); // SW_HIDE
        preview.Image = null;
    }

    // Fenêtre sans bordure qui ne prend jamais le focus (sinon la fenêtre principale se cacherait).
    sealed class PreviewForm : Form
    {
        Image? image;
        // Dessin direct (pas de PictureBox : la fenêtre est montrée en Win32, ses contrôles enfants resteraient cachés).
        public Image? Image { get => image; set { image?.Dispose(); image = value; Invalidate(); } }
        public PreviewForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Padding = new Padding(1);
            BackColor = SystemColors.ControlDark;
            DoubleBuffered = true;
        }
        protected override bool ShowWithoutActivation => true;
        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (image == null) return;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(image, 1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            base.WndProc(ref m);
        }
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x80 | 0x8; return cp; } // NOACTIVATE | TOOLWINDOW | TOPMOST
        }
    }

    static string ImagePath(Item it) => Path.Combine(ImageDir, it.Key + ".png");

    // Miniature mise en cache ; null si le fichier image est absent ou illisible.
    Image? Thumb(Item it)
    {
        if (thumbs.TryGetValue(it.Key, out var t)) return t;
        try
        {
            using var ms = new MemoryStream(File.ReadAllBytes(ImagePath(it)));
            using var img = Image.FromStream(ms);
            double s = Math.Min(1.0, Math.Min(ThumbHeight / (double)img.Height, (list.ClientSize.Width - 40) / (double)img.Width));
            t = new Bitmap(img, Math.Max(1, (int)(img.Width * s)), Math.Max(1, (int)(img.Height * s)));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException) { t = null; }
        return thumbs[it.Key] = t;
    }

    void DeleteImages(IEnumerable<Item> removed)
    {
        foreach (var it in removed.Where(x => x.Kind == Kind.Image))
        {
            if (thumbs.Remove(it.Key, out var t)) t?.Dispose();
            try { File.Delete(ImagePath(it)); } catch (IOException) { }
        }
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

    bool CopyToClipboard(Item it)
    {
        try
        {
            if (it.Kind == Kind.Text) Clipboard.SetText(it.Text);
            else
            {
                using var ms = new MemoryStream(File.ReadAllBytes(ImagePath(it)));
                using var img = Image.FromStream(ms);
                Clipboard.SetImage(img);
            }
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or IOException)
        {
            inDialog = true;
            MessageBox.Show(this, "Impossible de copier cet élément : " + ex.Message, "Presse-papiers");
            inDialog = false;
            return false;
        }
    }

    void SaveSelectedAs()
    {
        if (list.SelectedItem is not Item it) return;
        bool image = it.Kind == Kind.Image;
        using var dlg = new SaveFileDialog
        {
            Filter = image ? "Image PNG (*.png)|*.png" : "Texte (*.txt)|*.txt",
            FileName = (image ? "capture_" : "texte_") + it.Time.ToString("yyyy-MM-dd_HH-mm-ss"),
        };
        inDialog = true;
        try
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (image) File.Copy(ImagePath(it), dlg.FileName, true);
            else File.WriteAllText(dlg.FileName, it.Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Enregistrement impossible : " + ex.Message, "Presse-papiers");
        }
        finally { inDialog = false; Activate(); }
    }

    async void PasteSelected()
    {
        if (!CopySelected()) return;
        Hide();
        if (previousWindow == IntPtr.Zero) return;
        SetForegroundWindow(previousWindow);
        await Task.Delay(80); // laisse la fenêtre cible reprendre le focus avant Ctrl+V
        SendKeys.SendWait("^v");
    }

    // IndexFromPoint peut renvoyer 65535 (liste vide, zone sans élément) : -1 dans ce cas.
    int ItemAt(Point p) { int i = list.IndexFromPoint(p); return i >= 0 && i < list.Items.Count ? i : -1; }

    List<Item> Selected => list.SelectedItems.Cast<Item>().ToList();

    // Un élément : copié tel quel. Plusieurs : les textes sont joints ligne par ligne (les images sont ignorées).
    bool CopySelected()
    {
        var sel = Selected;
        if (sel.Count == 0) return false;
        var texts = sel.Where(x => x.Kind == Kind.Text).Select(x => x.Text).ToList();
        if (sel.Count == 1 || texts.Count == 0) return CopyToClipboard(sel[0]);
        return CopyToClipboard(new Item { Kind = Kind.Text, Text = string.Join(Environment.NewLine, texts) });
    }

    void TogglePin()
    {
        var sel = Selected;
        bool pin = sel.Any(x => !x.Pinned);
        foreach (var it in sel) it.Pinned = pin;
        SaveAndRefresh();
    }

    void DeleteSelected()
    {
        var sel = Selected;
        if (sel.Count == 0) return;
        foreach (var it in sel) history.Items.Remove(it);
        DeleteImages(sel);
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
        var sel = Selected;
        list.BeginUpdate();
        list.Items.Clear();
        foreach (var it in history.View(search.Text.Trim())) list.Items.Add(it);
        foreach (var it in sel) { int i = list.Items.IndexOf(it); if (i >= 0) list.SetSelected(i, true); }
        if (list.SelectedIndices.Count == 0 && list.Items.Count > 0) list.SetSelected(0, true);
        list.EndUpdate();
    }
}
