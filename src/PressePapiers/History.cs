using System.Text.Json;
using System.Text.Json.Serialization;

namespace PressePapiers;

public enum Kind { Text, Image }

public class Item
{
    public Kind Kind { get; set; }
    public string Text { get; set; } = "";   // le texte, ou une description pour une image
    public string Key { get; set; } = "";    // texte ou hash de l'image : sert au dédoublonnage
    public bool Pinned { get; set; }
    public DateTime Time { get; set; } = DateTime.Now;
    public string Source { get; set; } = "";  // application d'où vient la copie
    public string? Rtf { get; set; }   // mise en forme d'origine (Word, navigateur…), null si texte brut
    public string? Html { get; set; }

    // Lien web si le texte est une seule adresse http(s), sinon null.
    [JsonIgnore]
    public Uri? Link =>
        Kind == Kind.Text && Uri.TryCreate(Text.Trim(), UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")
        && !Text.Trim().Any(char.IsWhiteSpace) ? u : null;

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
    public int Max { get; set; }
    public int MaxDays { get; set; }  // 0 = jamais d'effacement par l'âge
    readonly string file;

    public History(string file, int max = 200) { this.file = file; Max = max; }

    // Ajoute en tête ; un doublon remonte en gardant son épingle. Renvoie les éléments retirés (trop vieux).
    public List<Item> Add(Item it)
    {
        int i = Items.FindIndex(x => x.Kind == it.Kind && x.Key == it.Key);
        if (i >= 0)
        {
            it.Pinned = Items[i].Pinned;
            if (it.Source.Length == 0) it.Source = Items[i].Source;
            // Recollé en texte brut depuis l'historique : on garde la mise en forme connue.
            it.Rtf ??= Items[i].Rtf;
            it.Html ??= Items[i].Html;
            Items.RemoveAt(i);
        }
        Items.Insert(0, it);
        return Trim();
    }

    // Retire les non épinglés plus vieux que MaxDays, puis les plus vieux au-delà de Max (le plus récent est toujours gardé).
    public List<Item> Trim()
    {
        var removed = new List<Item>();
        var limit = DateTime.Now.AddDays(-MaxDays);
        for (int j = Items.Count - 1; j >= 1 && MaxDays > 0; j--)
            if (!Items[j].Pinned && Items[j].Time < limit) { removed.Add(Items[j]); Items.RemoveAt(j); }
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

public static class Release
{
    // Tag GitHub ("v1.2" ou "1.2.0") plus récent que la version en cours ?
    public static bool IsNewer(string? tag, Version current)
    {
        if (!Version.TryParse(tag?.TrimStart('v', 'V'), out var v)) return false;
        static Version N(Version x) => new(x.Major, x.Minor, Math.Max(x.Build, 0));
        return N(v) > N(current);
    }
}
