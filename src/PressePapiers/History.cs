using System.Text.Json;

namespace PressePapiers;

public enum Kind { Text, Image }

public class Item
{
    public Kind Kind { get; set; }
    public string Text { get; set; } = "";   // le texte, ou une description pour une image
    public string Key { get; set; } = "";    // texte ou hash de l'image : sert au dédoublonnage
    public bool Pinned { get; set; }
    public DateTime Time { get; set; } = DateTime.Now;

    public override string ToString()
    {
        var t = Text.ReplaceLineEndings(" ").Trim();
        if (t.Length > 120) t = t[..120] + "…";
        return (Pinned ? "📌 " : "") + (Kind == Kind.Image ? "🖼 " : "") + t;
    }
}

public class History
{
    public List<Item> Items { get; private set; } = new();
    public int Max { get; }
    readonly string file;

    public History(string file, int max = 200) { this.file = file; Max = max; }

    // Ajoute en tête ; un doublon remonte en gardant son épingle. Renvoie les éléments retirés (trop vieux).
    public List<Item> Add(Item it)
    {
        int i = Items.FindIndex(x => x.Kind == it.Kind && x.Key == it.Key);
        if (i >= 0) { it.Pinned = Items[i].Pinned; Items.RemoveAt(i); }
        Items.Insert(0, it);
        var removed = new List<Item>();
        for (int j = Items.Count - 1; j >= 1 && Items.Count > Max; j--)
            if (!Items[j].Pinned) { removed.Add(Items[j]); Items.RemoveAt(j); }
        return removed;
    }

    public List<Item> ClearUnpinned()
    {
        var removed = Items.Where(x => !x.Pinned).ToList();
        Items.RemoveAll(x => !x.Pinned);
        return removed;
    }

    // Épinglés d'abord, puis du plus récent au plus ancien (OrderBy est stable).
    public IEnumerable<Item> View(string query) =>
        Items.Where(x => query.Length == 0 || x.Text.Contains(query, StringComparison.CurrentCultureIgnoreCase))
             .OrderByDescending(x => x.Pinned);

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Items));
        File.Move(tmp, file, true); // écriture atomique : pas d'historique à moitié écrit
    }

    public void Load()
    {
        if (!File.Exists(file)) return;
        try { Items = JsonSerializer.Deserialize<List<Item>>(File.ReadAllText(file)) ?? new(); }
        catch (JsonException) { File.Copy(file, file + ".bak", true); Items = new(); }
    }
}
