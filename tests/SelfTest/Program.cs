using PressePapiers;

int failures = 0;
void Check(bool ok, string name) { Console.WriteLine((ok ? "OK   " : "ÉCHEC ") + name); if (!ok) failures++; }
Item T(string s) => new() { Kind = Kind.Text, Text = s, Key = s };

var dir = Path.Combine(Path.GetTempPath(), "pp-selftest-" + Guid.NewGuid().ToString("N"));
var file = Path.Combine(dir, "historique.json");
try
{
    var h = new History(file, max: 3);
    h.Add(T("a")); h.Add(T("b")); h.Add(T("c"));
    Check(string.Join(",", h.Items.Select(x => x.Text)) == "c,b,a", "ajout en tête");

    h.Items.First(x => x.Text == "a").Pinned = true;
    h.Add(T("a"));
    Check(h.Items[0].Text == "a" && h.Items[0].Pinned && h.Items.Count == 3, "doublon remonte et garde l'épingle");

    var removed = h.Add(T("d"));
    Check(removed.Count == 1 && removed[0].Text == "b" && h.Items.Count == 3, "limite : retire le plus vieux non épinglé");

    h.Add(T("e")); h.Add(T("f"));
    Check(h.Items.Any(x => x.Text == "a"), "un épinglé n'est jamais retiré");

    Check(h.View("").First().Text == "a", "épinglés en premier");
    h.Add(T("Bonjour Monde"));
    Check(h.View("monde").Single().Text == "Bonjour Monde", "recherche insensible à la casse");

    h.Save();
    var h2 = new History(file, max: 3); h2.Load();
    Check(h2.Items.Select(x => (x.Text, x.Pinned)).SequenceEqual(h.Items.Select(x => (x.Text, x.Pinned))), "sauvegarde puis rechargement");

    h2.ClearUnpinned();
    Check(h2.Items.Count == 1 && h2.Items[0].Text == "a", "vider garde les épinglés");

    var hs = new History(file, max: 5);
    hs.Add(new Item { Kind = Kind.Text, Text = "x", Key = "x", Source = "Firefox" });
    hs.Add(new Item { Kind = Kind.Text, Text = "x", Key = "x" });
    Check(hs.Items[0].Source == "Firefox", "doublon sans source garde l'ancienne source");

    hs.Add(T("y")); hs.Add(T("z")); hs.Max = 1; var cut = hs.Trim();
    Check(hs.Items.Count == 1 && hs.Items[0].Text == "z" && cut.Count == 2, "baisser la limite retire les plus vieux");

    Check(T("https://exemple.fr/page").Link != null && T("voir https://exemple.fr").Link == null && T(@"C:\dossier").Link == null, "détection des liens");

    var hd = new History(file, max: 10) { MaxDays = 7 };
    hd.Add(new Item { Kind = Kind.Text, Text = "vieux", Key = "vieux", Time = DateTime.Now.AddDays(-30) });
    hd.Add(new Item { Kind = Kind.Text, Text = "vieux épinglé", Key = "ve", Time = DateTime.Now.AddDays(-30), Pinned = true });
    hd.Add(T("récent"));
    Check(hd.Items.Count == 2 && hd.Items.All(x => x.Text != "vieux"), "effacement après X jours, sauf épinglés");

    var hr = new History(file, max: 5);
    hr.Add(new Item { Kind = Kind.Text, Text = "g", Key = "g", Rtf = "{\\rtf1 g}" });
    hr.Add(T("g"));
    Check(hr.Items[0].Rtf != null, "recoller en texte brut garde la mise en forme connue");

    var v = new Version(1, 1, 0, 0);
    Check(Release.IsNewer("v1.2", v) && !Release.IsNewer("v1.1", v) && !Release.IsNewer("1.0.0", v) && !Release.IsNewer("abc", v), "comparaison des versions");

    File.WriteAllText(file, "{ cassé");
    var h3 = new History(file); h3.Load();
    Check(h3.Items.Count == 0 && File.Exists(file + ".bak"), "fichier corrompu : sauvegarde .bak et repart à vide");
}
finally { Directory.Delete(dir, true); }

Console.WriteLine(failures == 0 ? "Tous les tests passent." : $"{failures} test(s) en échec.");
return failures == 0 ? 0 : 1;
