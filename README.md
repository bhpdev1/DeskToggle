# DesktopIconsToggle

Masque et affiche les icônes du bureau Windows sans passer par le menu contextuel,
et optionnellement la barre des tâches avec.

## Utilisation

Deux façons de basculer :

- **Raccourci clavier global**, `Ctrl+Alt+D` par défaut, configurable depuis le menu.
- **Icône de notification**, présente uniquement quand le bureau est au premier
  plan. Clic gauche pour basculer, clic droit pour le menu.

Le menu :

| Entrée | Effet |
|---|---|
| *Afficher / Masquer les icônes* | Bascule, comme le raccourci |
| *Masquer aussi la barre des tâches* | Étend la bascule à la barre des tâches |
| *Raccourci : …* | Ouvre la fenêtre de capture pour changer la combinaison |
| *Démarrage automatique* | Lance l'application à l'ouverture de session |
| *Quitter* | Restaure tout, puis ferme |

### La barre des tâches

L'option n'efface pas la barre : elle active le **masquage automatique** de
Windows. La barre revient donc quand le pointeur atteint le bas de l'écran, et
c'est l'explorateur qui s'en charge — l'application ne surveille rien, ce qui
explique l'absence de latence.

Deux conséquences :

- Si vous utilisiez déjà le masquage automatique, il est préservé : l'application
  lit votre réglage au lancement et y revient en restaurant.
- Le réglage est **global**. Il n'existe aucune API Windows pour masquer la barre
  sur un seul écran, donc l'option s'applique à tous.

### Changer le raccourci

Menu → *Raccourci : …*. Appuyez sur la combinaison souhaitée ; au moins une touche
de modification est requise. **Échap seul et Alt+F4 annulent** — Échap accompagné
d'un modificateur reste un choix valide, et Alt+F4 est refusé parce que
l'enregistrer confisquerait « fermer la fenêtre » à tout le système.

Au tout premier lancement, tant que vous n'avez rien choisi vous-même,
l'application essaie `Ctrl+Alt+D`, puis `Ctrl+Alt+Maj+D`, puis `Ctrl+Alt+Maj+F9`,
et garde la première que Windows accorde. Le menu affiche celle réellement obtenue.

**Évitez les combinaisons avec la touche Windows.** L'explorateur traite les
`Win+<touche>` par son propre hook clavier, en amont de la distribution des
raccourcis enregistrés : l'enregistrement peut réussir et le raccourci ne jamais
se déclencher. Rien ne permet de le détecter à l'enregistrement — c'est pourquoi
aucune combinaison de cette famille n'est proposée par défaut. Dès que vous en choisissez
une, elle n'est plus jamais remplacée : si elle cesse de fonctionner,
l'application le signale au lieu d'en substituer une autre.

La combinaison est testée par un enregistrement réel avant d'être acceptée, ce qui
écarte les combinaisons que Windows refuse. Cela ne couvre pas le cas ci-dessus :
une combinaison accordée puis consommée en amont passe le test et ne fonctionne
pas pour autant.

Une autre réserve : si une combinaison est **déjà enregistrée par Windows ou par une
autre application**, c'est cette application qui la reçoit — la fenêtre de capture
ne voit rien passer et reste silencieuse. Si vos touches semblent sans effet dans
la fenêtre, c'est presque toujours ça. Essayez quelque chose de manifestement
libre, par exemple `Ctrl+Alt+Maj+F9`, pour vérifier que la fenêtre répond.

## Sécurité

L'application restaure les icônes à chaque fermeture, et remet la barre des tâches
dans l'état où elle l'a trouvée.

Si le processus est tué sans passer par *Quitter*, Windows conserve l'état masqué —
l'application le note et **répare au lancement suivant**, pour les icônes comme
pour la barre des tâches. Si la restauration ne peut pas être confirmée (Explorer
en cours de redémarrage), la note est **conservée** plutôt qu'effacée, et la
réparation est retentée au lancement d'après.

Reste un cas qu'aucun programme ne peut couvrir : processus tué avec les icônes
masquées, et jamais relancé. La récupération est alors manuelle et tient en deux
clics — **clic droit sur le bureau → Affichage → Afficher les éléments du Bureau**.
Ce menu reste toujours disponible : l'application n'envoie rien d'autre que cette
même commande et ne désactive jamais rien.

Les bascules trop rapprochées (moins de 300 ms) sont ignorées. Cela évite le
clignotement d'un appui répété, et empêche qu'une deuxième bascule parte avant que
l'explorateur ait appliqué la première.

## Installation

1. Copier `DesktopIconsToggle.exe` où vous voulez, par exemple `C:\Tools\`.
2. Le lancer.
3. Clic droit sur l'icône de notification → cocher *Démarrage automatique*.

L'exécutable est autonome : aucun runtime .NET n'est requis.

## Désinstallation

1. Clic droit sur l'icône → décocher *Démarrage automatique*.
2. Clic droit → *Quitter*.
3. Supprimer l'exécutable.
4. Facultatif, pour ne rien laisser : supprimer la clé de registre
   `HKCU\Software\DesktopIconsToggle`, qui contient le raccourci choisi et l'état
   des options.

## Compilation

Prérequis : SDK .NET 10, MSVC et le Windows SDK (fournis par Visual Studio Build Tools).

    dotnet test tests/DesktopIconsToggle.Tests
    dotnet publish src/DesktopIconsToggle -c Release -r win-x64

Le binaire est produit dans
`src/DesktopIconsToggle/bin/Release/net10.0-windows/win-x64/publish/`.
