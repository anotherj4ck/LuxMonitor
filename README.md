# Lux Monitor

**Le moniteur système pour Windows qui se fond dans votre fond d'écran.**
On le lance, il affiche tout. On clique sur une mesure, il ouvre le bon outil.

*[English version](README.en.md)*

![Licence GPL-3.0](https://img.shields.io/badge/licence-GPL--3.0-blue)
![Windows 11](https://img.shields.io/badge/Windows-11-0078D4)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Version](https://img.shields.io/badge/version-0.2.0-lightgrey)
![Langues](https://img.shields.io/badge/langues-FR%20%7C%20EN-informational)

<p align="center">
  <img src="docs/captures/themes.png" alt="Le widget Lux Monitor et sa fenêtre de choix des thèmes">
</p>

| Bandeau | Miniature | Mode jeu |
|:---:|:---:|:---:|
| ![Disposition bandeau](docs/captures/bandeau.png) | ![Disposition miniature](docs/captures/miniature.png) | ![Mode jeu par-dessus un jeu](docs/captures/mode-jeu.png) |

---

## Pourquoi Lux Monitor ?

Les outils de supervision existants sont puissants, mais souvent pénibles à mettre en place : plugins, fichiers de configuration, dizaines de capteurs à choisir. Lux Monitor fait le pari inverse : **zéro configuration**. Il affiche l'essentiel, joliment, et chaque mesure **mène quelque part** :

| On clique sur… | Lux Monitor ouvre… |
|---|---|
| Le processeur | Intel Driver & Support Assistant, ou AMD Software (sinon la page officielle des pilotes) |
| La carte graphique | NVIDIA App ou AMD Software (sinon le Gestionnaire des tâches) |
| La RAM | Le Gestionnaire des tâches |
| Un disque | Le disque dans l'Explorateur de fichiers |

## Fonctionnalités

- **Processeur** : charge, modèle, température*
- **Cartes graphiques** : charge, température, VRAM. Le GPU intégré se masque tout seul quand il ne sert pas
- **RAM** : utilisation et détail des barrettes (fabricant, capacité)
- **Disques** : espace utilisé et libre
- **FPS*** de l'application au premier plan
- **9 thèmes**, choisis dans une petite fenêtre qui s'ouvre à côté du widget (voir ci-dessous)
- **Trois dispositions**, selon la largeur que vous lui donnez avec la poignée en bas à droite : **miniature** (étroit), **normale**, et **bandeau** horizontal (large)
- **Mode jeu** : une barre d'infos toujours au premier plan, traversée par les clics, qui ne gêne jamais le jeu. `Ctrl+Maj+L` ou le bouton manette pour l'activer
- **Français et anglais** : langue de Windows par défaut, modifiable à tout moment dans le menu de l'icône, sans redémarrer
- **Figer la fenêtre** : plus de déplacement ni de redimensionnement par erreur (menu de l'icône)
- **Zone de notification**, lancement au démarrage, position et taille mémorisées

\* *Nécessite PawnIO et/ou les droits administrateur : voir plus bas.*

## Les thèmes

| | | |
|:---:|:---:|:---:|
| ![Liquide](docs/captures/theme-liquide.png)<br>**Liquide** : verre flouté qui laisse voir le fond d'écran | ![Sombre](docs/captures/theme-sombre.png)<br>**Sombre** | ![Clair](docs/captures/theme-clair.png)<br>**Clair** : verre laiteux |
| ![Tactical Radar](docs/captures/theme-tactical-radar.png)<br>**Tactical Radar** : vert radar sur verre sombre | ![Cyber HUD](docs/captures/theme-cyber-hud.png)<br>**Cyber HUD** : néon cyan futuriste | ![Kawaii Neko](docs/captures/theme-kawaii-neko.png)<br>**Kawaii Neko** : rose poudré, doux et mignon |
| ![Lune Rouge](docs/captures/theme-lune-rouge.png)<br>**Lune Rouge** : noir profond et rouge sang | ![Sakura Kintsugi](docs/captures/theme-sakura-kintsugi.png)<br>**Sakura Kintsugi** : bordeaux, or et cerisier | ![Sunset Minimal](docs/captures/theme-sunset-minimal.png)<br>**Sunset Minimal** : pêche et corail, apaisant |

Chaque thème a ses couleurs, ses arrondis et parfois une petite touche discrète (un radar, une fleur de cerisier, une patte de chat…). Toutes les illustrations sont des dessins vectoriels originaux.

## Installation

1. Téléchargez **`LuxMonitor.exe`** dans la section **Assets** de la page [Releases](../../releases). *(Les archives « Source code » ne servent qu'à compiler le projet.)*
2. Placez-le à un emplacement définitif, par exemple `%LocalAppData%\Programs\LuxMonitor`, puis lancez-le. C'est tout : pas d'installation, pas de prérequis (le moteur .NET est inclus).

> **Avertissement Windows SmartScreen** : l'exécutable n'est pas encore signé numériquement, Windows peut donc afficher « Windows a protégé votre ordinateur ». Cliquez sur **Informations complémentaires → Exécuter quand même**. Vous pouvez vérifier que le fichier est authentique en comparant son empreinte SHA-256 (`(Get-FileHash .\LuxMonitor.exe).Hash` dans PowerShell) à celle publiée dans les notes de version. La signature est prévue.

## Température du processeur et FPS

Windows ne fournit pas ces deux informations directement :

- **La température du processeur** nécessite le pilote [PawnIO](https://pawnio.eu), gratuit et open source, utilisé aussi par LibreHardwareMonitor et Fan Control.
- **La température et les FPS** nécessitent les **droits administrateur**.

Au premier lancement, une fenêtre d'accueil explique tout et propose de l'activer. Rien n'est installé ni activé sans votre accord. Vous pouvez revenir sur ce choix à tout moment : menu de l'icône → **Température et FPS…**

Si vous activez les droits administrateur, Lux Monitor (après un écran de confirmation Windows) :
- copie son exe dans **`C:\Program Files\LuxMonitor`**, un dossier que seul un administrateur peut modifier ;
- crée une **tâche planifiée** (« Lux Monitor » dans le Planificateur de tâches) qui lance cette copie en administrateur à chaque ouverture de session, sans écran de confirmation.

Pourquoi une copie ? Pour qu'aucun programme malveillant ne puisse remplacer le fichier lancé en administrateur : tous les détails sont dans [SECURITY.md](SECURITY.md). Ensuite, un double-clic sur votre exe passe simplement la main à la tâche planifiée.

**Après une mise à jour**, si les droits administrateur sont activés : « Température et FPS… » → **Désactiver**, puis **Activer**, pour remplacer la copie protégée par la nouvelle version.

> **À propos de PawnIO**
>
> PawnIO est un projet **indépendant**, développé et maintenu par ses propres auteurs. Lux Monitor n'est ni affilié à PawnIO ni approuvé par ses auteurs, ne le distribue pas et ne l'installe pas : la fenêtre d'accueil ouvre seulement son site officiel, et c'est vous qui décidez de l'installer.
>
> Un pilote a un accès très profond au système. N'installez PawnIO que si vous faites confiance à sa source, et téléchargez-le uniquement depuis son [site officiel](https://pawnio.eu). Son installation, son fonctionnement et ses éventuels problèmes relèvent de PawnIO et de ses auteurs, pas de Lux Monitor. Pour toute question à son sujet, adressez-vous à [son projet](https://github.com/namazso/PawnIO).
>
> Sans PawnIO, tout le reste de Lux Monitor fonctionne normalement : seule la température du processeur est absente.

**Comment les FPS sont mesurés** : à chaque image affichée, DirectX signale un événement dans ETW (*Event Tracing for Windows*), le système de traçage intégré à Windows. Lux Monitor compte ces événements, comme l'outil PresentMon de Microsoft et Intel. **Rien n'est injecté dans les jeux** : aucun risque avec les anti-triche.

## Le mode jeu ne s'affiche pas ?

Le jeu est probablement en **plein écran exclusif** : dans ce mode, le jeu prend le contrôle direct de l'écran et aucune fenêtre ne peut s'afficher par-dessus. Dans les options graphiques du jeu, choisissez **« Fenêtré sans bordure »** (*Borderless*) : le rendu est identique, et la barre s'affiche.

Les outils qui s'affichent malgré tout en plein écran exclusif s'injectent dans le jeu. Lux Monitor ne le fait volontairement pas.

## Sécurité

Lux Monitor peut tourner en administrateur : sa sécurité a donc fait l'objet d'un **audit complet** pour la v0.2 (7 failles corrigées, dont une élévation de privilèges présente dans la v0.1.0). Le bilan, les risques résiduels et la façon de signaler une faille sont dans **[SECURITY.md](SECURITY.md)**.

> **Vous utilisez la v0.1.0 avec les droits administrateur activés ?** Mettez à jour, puis « Température et FPS… » → Désactiver, puis Activer.

## Limites connues

- **Processeurs AMD** : pris en charge dans le code, mais pas encore testés sur une vraie machine. Vos retours sont bienvenus !
- **FPS** : certains jeux en Vulkan ou OpenGL n'émettent pas les événements DirectX mesurés et n'affichent donc pas de FPS.
- **Cartes Intel Arc** : vues comme un GPU intégré, elles sont masquées quand elles sont au repos.
- **Taille de l'exe (~70 Mo) et mémoire (~200 Mo)** : c'est le prix d'un fichier unique qui fonctionne sans rien installer, avec le moteur .NET et les bibliothèques de capteurs. Une optimisation est prévue.
- **Démarrage administrateur** : une console peut apparaître une fraction de seconde à l'ouverture de session (voir [SECURITY.md](SECURITY.md)).

## Confidentialité

Lux Monitor **n'envoie aucune donnée** et n'utilise pas le réseau. Ses réglages sont enregistrés dans `%AppData%\LuxMonitor\settings.json`.

## Désinstaller

1. Si les droits administrateur sont activés : « Température et FPS… » → **Désactiver** (supprime la tâche planifiée et `C:\Program Files\LuxMonitor`).
2. Quittez Lux Monitor, puis supprimez l'exe, le dossier `%AppData%\LuxMonitor` et, s'il existe, `%LocalAppData%\LuxMonitor`.

## Compiler depuis les sources

Prérequis : Windows 10/11 et le [SDK .NET 10](https://dotnet.microsoft.com/download) (ou Visual Studio 2026 avec la charge « Développement .NET Desktop »).

```powershell
git clone https://github.com/anotherj4ck/LuxMonitor.git
cd LuxMonitor
dotnet publish LuxMonitor -c Release -o publish
```

L'exécutable se trouve dans `publish\LuxMonitor.exe`.

Options de lancement utiles :
- `--demo` : fausses mesures réalistes, pour travailler l'interface sans vrai matériel (dans une machine virtuelle, par exemple)
- `--diagnostic` : écrit `diagnostic-capteurs.txt` dans `%LocalAppData%\LuxMonitor` (droits, présence de PawnIO, liste de tous les capteurs, erreurs rencontrées)

### Organisation du code

```
LuxMonitor/
├── Models/          Les données : une « photo » de la machine à un instant donné
├── Sensors/         La lecture du matériel (API Windows, LibreHardwareMonitor, WMI, ETW)
├── ViewModels/      Ce que l'interface affiche (WPF, modèle MVVM)
├── Services/        Thèmes, effets Windows, zone de notification, démarrage admin, raccourci clavier...
├── Localization/    Les textes en français et en anglais, et le changement de langue en direct
├── Themes/          Un fichier par thème (couleurs, formes, décorations) et les styles communs
└── *.xaml           Les fenêtres : widget, choix du thème, mode jeu, accueil
```

Principes clés :
- **L'interface ne lit jamais le matériel elle-même.** Les capteurs produisent une photo de la machine, sur un fil d'exécution séparé (l'interface ne se fige jamais), et l'interface ne fait que l'afficher. C'est ce qui permet le mode `--demo`, et plusieurs dispositions (miniature, normale, bandeau, mode jeu) pour les mêmes données.
- **Un thème ne contient que des ressources** (couleurs, arrondis, dessins vectoriels) : en ajouter un ne demande pas de toucher au code de l'interface.
- **Une langue = un dictionnaire de textes** (`Localization/Strings.cs`) : en ajouter une ne demande pas de toucher aux fenêtres.

## Contribuer

Les retours sont les bienvenus, en particulier sur les **processeurs AMD** et les **traductions**. Ouvrez une *issue* pour un bug ou une idée. Pour une faille de sécurité, suivez plutôt [SECURITY.md](SECURITY.md).

## Crédits

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0) : lecture des capteurs GPU et de la température CPU
- [PawnIO](https://github.com/namazso/PawnIO) (licence GPL, avec une exception pour les programmes qui l'utilisent) : pilote d'accès au matériel, installé séparément par l'utilisateur
- [Microsoft.Diagnostics.Tracing.TraceEvent](https://github.com/microsoft/perfview) (MIT) : écoute des événements ETW pour les FPS

## Absence de garantie

Lux Monitor est fourni **« tel quel », sans aucune garantie**, comme le prévoient les articles 15 et 16 de la licence GPL v3.0. L'auteur ne peut être tenu responsable des dommages liés à son utilisation, ni à celle des logiciels tiers mentionnés ici (PawnIO, pilotes et outils des fabricants).

## Licence

Lux Monitor est un logiciel libre, distribué sous licence [GNU GPL v3.0](LICENSE) : vous pouvez l'utiliser, l'étudier, le modifier et le redistribuer, à condition que les versions modifiées restent libres sous la même licence.

Projet personnel de [anotherj4ck](https://github.com/anotherj4ck).
