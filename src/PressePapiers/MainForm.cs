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
    readonly ListBox list = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, SelectionMode = SelectionMode.MultiExtended, DrawMode = DrawMode.OwnerDrawVariable };
    const int ThumbHeight = 90;
    readonly Dictionary<string, Image?> thumbs = new();
    readonly NotifyIcon tray = new() { Text = "Presse-papiers (Ctrl+Maj+V)", Icon = AppIcon, Visible = true };
    static readonly Icon AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
    readonly ToolTip tip = new();
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
        Controls.Add(list);
        Controls.Add(search);

        history.Load();

        search.TextChanged += (_, _) => Refresh_();
        search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); list.ClearSelected(); list.SetSelected(Math.Min(1, list.Items.Count - 1), true); e.Handled = true; }
            if (e.KeyCode == Keys.Enter) { PasteSelected(); e.SuppressKeyPress = true; }
        };
        // Ctrl/Maj + clic = sélection multiple, pas de collage
        list.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left && ModifierKeys == Keys.None && list.IndexFromPoint(e.Location) >= 0) PasteSelected(); };
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { PasteSelected(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Delete) DeleteSelected();
        };
        list.MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) { int i = list.IndexFromPoint(e.Location); if (i >= 0 && !list.GetSelected(i)) { list.ClearSelected(); list.SetSelected(i, true); } } };
        list.MeasureItem += (_, e) =>
            e.ItemHeight = list.Items[e.Index] is Item { Kind: Kind.Image } ? ThumbHeight + 14 : list.Font.Height + 14;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            var it = (Item)list.Items[e.Index];
            var color = (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : list.ForeColor;
            var b = e.Bounds;
            if (it.Kind == Kind.Image && Thumb(it) is Image t)
            {
                int x = b.X + 4;
                if (it.Pinned) { TextRenderer.DrawText(e.Graphics, "📌", list.Font, new Point(x, b.Y + 4), color); x += 26; }
                e.Graphics.DrawImage(t, x, b.Y + 7, t.Width, t.Height);
                e.Graphics.DrawRectangle(SystemPens.ControlDark, x, b.Y + 7, t.Width - 1, t.Height - 1);
            }
            else
                TextRenderer.DrawText(e.Graphics, it.ToString(), list.Font, new Rectangle(b.X + 8, b.Y, b.Width - 16, b.Height), color,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            // Ligne de séparation entre éléments
            e.Graphics.DrawLine(SystemPens.ControlLight, b.X + 4, b.Bottom - 1, b.Right - 4, b.Bottom - 1);
        };
        list.MouseMove += (_, e) =>
        {
            int i = list.IndexFromPoint(e.Location);
            var t = i >= 0 && list.Items[i] is Item it && it.Kind == Kind.Text ? it.Text : "";
            if (t.Length > 1000) t = t[..1000] + "…";
            if (tip.GetToolTip(list) != t) tip.SetToolTip(list, t);
        };

        var itemMenu = new ContextMenuStrip();
        itemMenu.Items.Add("Copier", null, (_, _) => CopySelected());
        itemMenu.Items.Add("Enregistrer sous…", null, (_, _) => SaveSelectedAs());
        itemMenu.Items.Add("Épingler / désépingler", null, (_, _) => TogglePin());
        itemMenu.Items.Add("Supprimer", null, (_, _) => DeleteSelected());
        list.ContextMenuStrip = itemMenu;

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Ouvrir (Ctrl+Maj+V)", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("Vider l'historique (garde les épinglés)", null, (_, _) => { DeleteImages(history.ClearUnpinned()); SaveAndRefresh(); });
        trayMenu.Items.Add("Quitter", null, (_, _) => { tray.Visible = false; Application.Exit(); });
        tray.ContextMenuStrip = trayMenu;
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
