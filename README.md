# Gestionnaire de presse-papiers

Petit outil Windows qui garde l'historique de tout ce que vous copiez (texte et images).

## Installation

Il faut le SDK .NET 8.

```bash
dotnet build src/PressePapiers -c Release
```

L'exécutable est dans `src/PressePapiers/bin/Release/net8.0-windows/GestionnairePressePapiers.exe`.

Pour le lancer au démarrage de Windows : bouton ⚙ de la fenêtre, case « Lancer au démarrage de Windows ».

## Utilisation

- L'outil tourne en arrière-plan avec une icône près de l'horloge.
- `Ctrl+Maj+V` (ou clic sur l'icône) ouvre la fenêtre.
- Tapez pour rechercher, `↓` pour descendre dans la liste.
- Clic ou `Entrée` sur un élément : il est recollé dans l'application d'où vous venez.
- `Ctrl+A` sélectionne tous les éléments, `Ctrl`/`Maj` + clic en sélectionne plusieurs. Copier, épingler et supprimer agissent sur toute la sélection (les textes sont joints ligne par ligne).
- Sous chaque élément : l'heure de la copie et l'application d'où il vient.
- Survol : les images s'affichent en grand à côté de la fenêtre, les longs textes en entier dans une bulle.
- Les liens (`http`/`https`) sont en bleu ; clic droit → « Ouvrir dans le navigateur ».
- Clic droit sur un élément : copier, enregistrer sous (`.png` ou `.txt`), épingler / désépingler, supprimer. `Suppr` supprime aussi.
- Les éléments épinglés restent en haut et ne sont jamais effacés automatiquement.
- Les captures d'écran (`Impr. écran`, `Win+Maj+S`) sont enregistrées aussi, avec une miniature dans la liste.
- `Échap` ferme la fenêtre. Clic droit sur l'icône : vider l'historique ou quitter.

- Bouton ⚙ (ou clic droit sur l'icône → Paramètres) : lancement au démarrage, nombre d'éléments gardés, raccourci clavier (cliquez dans la case puis appuyez sur la combinaison), coller ou seulement copier au clic, enregistrer ou non les images, aperçu au survol, ligne heure/application, ouverture près de l'horloge.

L'historique (200 éléments par défaut, hors épinglés) et les paramètres (`parametres.json`) sont enregistrés dans `%APPDATA%\GestionnairePressePapiers`.

L'icône (`src/PressePapiers/app.ico`) se régénère avec `powershell -File tools/make_icon.ps1`.

## Tests

```bash
dotnet run --project tests/SelfTest
```
