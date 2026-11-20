# Sécurité de Lux Monitor

*[English summary at the end of this file.](#english-summary)*

Lux Monitor lit des capteurs matériels et, si vous l'activez, se lance **avec les droits administrateur** à l'ouverture de session. Un programme qui tourne en administrateur est une cible de choix : ce document explique comment Lux Monitor s'en protège, ce qui a été vérifié, et les limites qui restent.

## Signaler une faille

**Merci de ne pas publier une faille de sécurité dans une *issue* publique.**

Utilisez le signalement privé de GitHub : onglet **Security** du dépôt, puis **Report a vulnerability**. Seul le mainteneur voit le signalement. Je m'engage à répondre, à corriger en priorité et à vous citer dans les notes de version si vous le souhaitez.

## Versions suivies

| Version | Correctifs de sécurité |
|---|---|
| 0.2.x | ✅ Oui |
| 0.1.0 | ❌ Non : **mettez à jour** (voir ci-dessous) |

> **Utilisateurs de la v0.1.0 avec le démarrage administrateur activé** : la v0.1.0 contient la faille n° 1 décrite ci-dessous. Installez la v0.2, puis menu de l'icône → « Température et FPS… » → **Désactiver**, puis **Activer**. La tâche planifiée est alors recréée de façon sûre.

## Ce qui est sensible dans Lux Monitor

| Élément | Pourquoi c'est sensible |
|---|---|
| Démarrage administrateur (tâche planifiée) | Lance un programme avec tous les droits, sans écran de confirmation. |
| Exécutable « fichier unique » | Extrait ses bibliothèques (DLL) sur le disque avant de les charger. |
| Pilote PawnIO (facultatif) | Pilote noyau : accès très profond au système. Projet indépendant de Lux Monitor. |
| Lecture des FPS (ETW) | Demande les droits administrateur. N'injecte **rien** dans les jeux. |

Sans le démarrage administrateur, Lux Monitor tourne avec les droits d'un utilisateur normal, et seule une partie de ce document le concerne.

## Audit de sécurité de la v0.2 (novembre 2026)

Audit réalisé sur le code de Lux Monitor, avec un objectif : **un programme malveillant qui tourne sans droits sur la machine ne doit jamais pouvoir se servir de Lux Monitor pour obtenir les droits administrateur.**

### Failles corrigées

| # | Gravité | Faille | Correction |
|---|---|---|---|
| 1 | Critique | **Élévation de privilèges par la tâche planifiée.** La tâche lançait en administrateur l'exe téléchargé (Téléchargements, Bureau…), un emplacement que tout programme de l'utilisateur peut modifier. En remplaçant ce fichier, un programme sans droits obtenait les droits administrateur à la prochaine ouverture de session. | La tâche lance une **copie protégée** dans `C:\Program Files\LuxMonitor`, un dossier que seul un administrateur peut modifier. |
| 2 | Critique | **Chargement de DLL piégées.** Un exe « fichier unique » extrait ses DLL dans le dossier temporaire de l'utilisateur, modifiable sans droits. Une DLL déposée là à l'avance aurait été chargée par l'instance administrateur. | Pour l'instance lancée par la tâche, l'extraction est redirigée vers `C:\Program Files\LuxMonitor\runtime` (variable `DOTNET_BUNDLE_EXTRACT_BASE_DIR`). |
| 3 | Élevée | **Exécution de commandes « AutoRun ».** La tâche passe par `cmd.exe` pour définir cette variable. Or `cmd.exe` exécute au démarrage les commandes *AutoRun* inscrites dans le registre de l'utilisateur, qui seraient alors lancées en administrateur. | `cmd.exe` est lancé avec l'option `/d`, qui désactive *AutoRun*. |
| 4 | Élevée | **Fichier de définition de la tâche modifiable.** Le fichier XML de la tâche était écrit dans `%TEMP%`, puis relu par `schtasks` en administrateur : un autre programme pouvait le modifier entre les deux. | Le fichier est écrit dans le dossier protégé, puis supprimé. |
| 5 | Élevée | **Faux programmes système.** `schtasks.exe`, `explorer.exe` et `taskmgr.exe` étaient appelés par leur simple nom. Windows cherche d'abord dans le dossier de l'application, où un faux programme du même nom pouvait être déposé. | Appel par le chemin complet (`C:\Windows\System32\…`). |
| 6 | Moyenne | **Navigateur ouvert en administrateur.** Les liens (site de PawnIO, pages Intel et AMD) ouvraient le navigateur avec les droits de Lux Monitor. | En administrateur, les liens passent par l'Explorateur, qui ouvre le navigateur sans droits. |
| 7 | Moyenne | **Écriture détournée par un lien.** En administrateur, l'écriture des réglages et du diagnostic dans un dossier de l'utilisateur pouvait être redirigée vers un fichier système par un lien symbolique ou une jonction. | Lux Monitor refuse d'écrire à travers un lien quand il est administrateur. Le diagnostic est écrit dans `%LocalAppData%\LuxMonitor`. |

### Vérifié, sans problème

- **Arguments de relance** (`--admin-startup=on/off`) : sans l'écran de confirmation administrateur (UAC), ils n'ont aucun effet.
- **Fichier de réglages** (`%AppData%\LuxMonitor\settings.json`) : un fichier abîmé ou des valeurs absurdes donnent les réglages par défaut, sans plantage. Ce fichier ne contient que des données (position, thème, langue), jamais de code.
- **Outils des fabricants** (NVIDIA App, AMD Software, Intel DSA) : cherchés uniquement dans `Program Files`, protégé par Windows.
- **Adresses web** : toutes écrites en dur dans le code, en `https`. Lux Monitor n'ouvre jamais une adresse venue de l'extérieur.
- **Dépendances** : `dotnet list package --vulnerable --include-transitive` ne signale aucune vulnérabilité connue.
- **Dépôt** : aucun secret, mot de passe ni fichier personnel, y compris dans l'historique Git.
- **Thèmes** : intégrés à l'exe. Aucun fichier de thème externe n'est chargé, car un fichier XAML externe pourrait exécuter du code.

### Risques résiduels, connus et assumés

| Risque | Pourquoi il est acceptable |
|---|---|
| Une console noire peut apparaître une fraction de seconde à l'ouverture de session. | Effet visible de la protection n° 2 (passage par `cmd.exe`). Aucun impact sur la sécurité. |
| Un autre programme de votre session peut empêcher Lux Monitor de démarrer en occupant son verrou d'instance unique. | Il ne peut que l'empêcher de démarrer (déni de service), sans en tirer aucun droit. |
| Lors d'une activation manuelle (bouton « Activer » + écran UAC), l'exe extrait encore ses DLL dans le dossier temporaire. | C'est le cas de tout exe « fichier unique », et vous validez l'écran UAC à chaque fois. Les lancements automatiques, eux, sont protégés. |
| Entre l'écran UAC et la copie dans `Program Files`, un programme déjà présent pourrait en théorie remplacer l'exe d'origine. | Fenêtre de temps très courte, qui suppose une machine déjà compromise. |
| Le pilote PawnIO a un accès très profond au système. | Facultatif, installé par vous-même depuis son site officiel. Projet indépendant : voir le README. |
| L'exe n'est pas encore signé numériquement (avertissement SmartScreen). | Signature prévue. En attendant, vérifiez l'empreinte SHA-256 publiée avec chaque version. |

## Bonnes pratiques pour les utilisateurs

1. **Téléchargez Lux Monitor uniquement depuis la page [Releases](../../releases) de ce dépôt.**
2. **Vérifiez l'empreinte** du fichier téléchargé, puis comparez-la à celle publiée dans les notes de version :
   ```powershell
   (Get-FileHash .\LuxMonitor.exe).Hash
   ```
3. **N'activez le démarrage administrateur que si vous voulez la température et les FPS.** Le reste fonctionne sans.
4. **Après une mise à jour**, si le démarrage administrateur est activé : « Température et FPS… » → Désactiver, puis Activer, pour remplacer la copie protégée.
5. **Pour tout désinstaller** : désactivez le démarrage administrateur (ce qui supprime la tâche et `C:\Program Files\LuxMonitor`), puis supprimez l'exe et le dossier `%AppData%\LuxMonitor`.

---

## English summary

Lux Monitor can optionally start **with administrator rights** at sign-in (needed for CPU temperature and FPS). The v0.2 security audit made sure that **no unprivileged program can use Lux Monitor to gain administrator rights**:

- the scheduled task runs a **protected copy** in `C:\Program Files\LuxMonitor`, never the downloaded exe;
- single-file DLL extraction for the elevated instance is redirected to a protected folder (`DOTNET_BUNDLE_EXTRACT_BASE_DIR`), and `cmd.exe /d` blocks registry *AutoRun* commands;
- system tools are called by full path, web links never open an elevated browser, and elevated writes refuse to follow symbolic links or junctions;
- no known vulnerable dependencies, no secrets in the repository.

**v0.1.0 users with administrator startup enabled should update**, then click *Disable* and *Enable* again in "Temperature and FPS…".

**Report vulnerabilities privately**: *Security* tab → *Report a vulnerability*. Please do not open a public issue.
