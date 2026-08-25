# DesktopIconsToggle

Masque et affiche les icônes du bureau Windows sans passer par le menu contextuel.

## Utilisation

- **Double-clic molette** sur le bureau : bascule les icônes.
- **Icône de notification** : présente uniquement quand le bureau est au premier
  plan. Clic gauche pour basculer, clic droit pour le menu.
- Menu : *Afficher / Masquer les icônes*, *Démarrage automatique*, *Quitter*.

Les icônes sont toujours restaurées quand l'application se ferme.

## Installation

1. Copier `DesktopIconsToggle.exe` où vous voulez (par exemple `C:\Tools\`).
2. Le lancer.
3. Clic droit sur l'icône de notification → cocher *Démarrage automatique*.

L'exécutable est autonome : aucun runtime .NET n'est requis.

## Désinstallation

1. Clic droit sur l'icône → décocher *Démarrage automatique*.
2. Clic droit → *Quitter*.
3. Supprimer l'exécutable.

## Compilation

Prérequis : SDK .NET 10, MSVC et le Windows SDK (fournis par Visual Studio Build Tools).

    dotnet test tests/DesktopIconsToggle.Tests
    dotnet publish src/DesktopIconsToggle -c Release -r win-x64

Le binaire est produit dans
`src/DesktopIconsToggle/bin/Release/net10.0-windows/win-x64/publish/`.
