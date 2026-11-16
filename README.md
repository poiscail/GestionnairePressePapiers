# Gestionnaire de presse-papiers

Petit outil Windows qui garde l'historique de tout ce que vous copiez (texte et images).

## Installation

Il faut le SDK .NET 8.

```bash
dotnet build src/PressePapiers -c Release
```

L'exécutable est dans `src/PressePapiers/bin/Release/net8.0-windows/GestionnairePressePapiers.exe`.

Pour le lancer au démarrage de Windows : `Win+R`, tapez `shell:startup`, et mettez-y un raccourci vers l'exécutable.

## Utilisation

- L'outil tourne en arrière-plan avec une icône près de l'horloge.
- `Ctrl+Maj+V` (ou clic sur l'icône) ouvre la fenêtre.
- Tapez pour rechercher, `↓` pour descendre dans la liste.
- Clic ou `Entrée` sur un élément : il est recollé dans l'application d'où vous venez.
- Clic droit : épingler / désépingler, supprimer. `Suppr` supprime aussi.
- Les éléments épinglés restent en haut et ne sont jamais effacés automatiquement.
- `Échap` ferme la fenêtre. Clic droit sur l'icône : vider l'historique ou quitter.

L'historique (200 éléments max, hors épinglés) est enregistré dans `%APPDATA%\GestionnairePressePapiers`.

## Tests

```bash
dotnet run --project tests/SelfTest
```
