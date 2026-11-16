namespace PressePapiers;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "GestionnairePressePapiers", out bool first);
        if (!first)
        {
            MessageBox.Show("Le gestionnaire de presse-papiers tourne déjà (icône près de l'horloge).", "Presse-papiers");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
