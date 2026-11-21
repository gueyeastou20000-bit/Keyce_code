# KEYCE KODER

**GROUPE 10 – B2A**

**KEYCE KODER** est un projet d'IDE de bureau développé en C# avec WPF et .NET 10. L'objectif est de fournir un environnement de développement intégré inspiré des IDE modernes, avec une architecture modulaire permettant à plusieurs membres de travailler en parallèle.

> **État du projet :** socle technique initial en place. L'application WPF démarre, l'éditeur AvalonEdit est intégré et la coloration syntaxique de plusieurs langages est disponible. Les fonctionnalités Explorer, menus/actions, exécution Python et terminal intégré restent à réaliser ou à intégrer par les membres responsables.

## 1. Objectifs

- Fournir une interface de développement de bureau.
- Permettre l'édition de fichiers de code avec coloration syntaxique.
- Gérer les documents ouverts et les opérations de lecture et d'enregistrement.
- Intégrer progressivement un explorateur de fichiers, des actions d'édition, un exécuteur Python et un terminal.
- Conserver une architecture modulaire, évolutive et testable.
- Faciliter le développement collaboratif grâce à une répartition claire des responsabilités.

## 2. Technologies

| Technologie | Utilisation |
|---|---|
| C# | Langage principal |
| .NET 10 | Plateforme de développement |
| WPF | Interface graphique Windows, construite en C# |
| AvalonEdit 6.3.1.120 | Éditeur de code |
| Git / GitHub | Gestion du code source et collaboration |
| MSTest | Framework prévu pour les tests automatisés |

L'interface principale est développée en C# sans fichiers XAML.

## 3. Architecture du dépôt

```text
KEYCE_CODE/
├── global.json
├── Directory.Build.props
├── KEYCE_KODER.slnx
├── README.md
├── LICENSE
├── SUIVI_EQUIPE_KEYCE_KODER.docx
│
├── src/
│   ├── KEYCE_KODER.App/
│   │   ├── Assets/
│   │   │   ├── logo.png
│   │   │   └── app.ico
│   │   ├── App.cs
│   │   ├── Program.cs
│   │   └── MainWindow.cs
│   │
│   ├── KEYCE_KODER.Core/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   └── Services/
│   │
│   ├── KEYCE_KODER.Editor/
│   │   ├── Controls/
│   │   ├── Highlighting/
│   │   └── Services/
│   │
│   ├── KEYCE_KODER.Runner/
│   ├── KEYCE_KODER.Terminal/
│   ├── KEYCE_KODER.Lsp/
│   └── KEYCE_KODER.Analysis/
│
├── tests/
│   └── KEYCE_KODER.Tests/
│
└── tools/
    └── create_icon.py
```

Certains dossiers et projets constituent des bases de travail prévues pour les développements à venir. Leur présence ne signifie pas que toutes les fonctionnalités correspondantes sont déjà implémentées.

### Rôle des projets

| Projet | Responsabilité |
|---|---|
| `KEYCE_KODER.App` | Application WPF, démarrage et composition des composants |
| `KEYCE_KODER.Core` | Modèles, contrats et services métier indépendants de l'interface |
| `KEYCE_KODER.Editor` | Contrôle d'édition AvalonEdit, coloration syntaxique et service éditeur |
| `KEYCE_KODER.Runner` | Exécution des programmes, notamment Python |
| `KEYCE_KODER.Terminal` | Terminal intégré CMD / PowerShell |
| `KEYCE_KODER.Lsp` | Base prévue pour l'intégration du Language Server Protocol |
| `KEYCE_KODER.Analysis` | Base prévue pour les fonctionnalités d'analyse du code |
| `KEYCE_KODER.Tests` | Tests automatisés |

L'objectif est de limiter les dépendances entre les modules et d'éviter les dépendances circulaires.

## 4. Fonctionnalités déjà en place

### 4.1. Socle de l'application

- Application WPF démarrant depuis `Program.cs`.
- Fenêtre principale construite en C# dans `MainWindow.cs`.
- Configuration commune du compilateur et du langage dans `Directory.Build.props`.
- Ciblage de .NET 10 et SDK fixé par `global.json`.
- Logo PNG conservé dans les ressources de l'application.
- Icône Windows `app.ico` créée et intégrée.

### 4.2. Éditeur de code

- Intégration d'AvalonEdit.
- Numérotation des lignes.
- Police monospace.
- Thème sombre de base.
- Événement de changement du texte exposé par le contrôle `CodeEditor`.
- Coloration syntaxique basée sur l'extension du fichier, avec les définitions intégrées d'AvalonEdit, notamment Python, C#, JavaScript, HTML, CSS, XML et JSON.

### 4.3. Gestion des documents

- Modèle `Document` avec identifiant, chemin, nom, contenu et indicateur de modification.
- `DocumentManager` pour créer, retrouver, ouvrir et fermer les documents.
- `FileService` pour lire et enregistrer les fichiers de manière asynchrone.
- `DocumentService` pour coordonner les opérations sur les documents.
- Contrat `IEditorService` et implémentation `EditorService` pour relier le contrôle d'édition au document courant.

### 4.4. Vérification technique

La compilation de l'application et de ses dépendances a été vérifiée avec :

```powershell
dotnet build src/KEYCE_KODER.App/KEYCE_KODER.App.csproj
```

Le lancement de développement s'effectue avec :

```powershell
dotnet run --project src/KEYCE_KODER.App/KEYCE_KODER.App.csproj
```

Les fonctionnalités Runner, Terminal, Lsp et Analysis disposent de leur structure initiale, mais leur développement reste à effectuer.

## 5. Prérequis et démarrage

### Prérequis

- Windows 10 ou Windows 11.
- SDK .NET 10.
- Visual Studio ou Visual Studio Code.
- Git.

### Installation

1. Installer le SDK .NET 10.
2. Cloner le dépôt :

```powershell
git clone <URL_DU_DEPOT>
```

3. Entrer dans le dossier du projet :

```powershell
cd KEYCE_CODE
```

4. Restaurer les dépendances :

```powershell
dotnet restore
```

5. Compiler l'application :

```powershell
dotnet build src/KEYCE_KODER.App/KEYCE_KODER.App.csproj
```

6. Lancer l'application :

```powershell
dotnet run --project src/KEYCE_KODER.App/KEYCE_KODER.App.csproj
```

Si la solution complète est restaurée et configurée, les commandes `dotnet build` et `dotnet test` peuvent également être utilisées à la racine. Les tests automatisés doivent toutefois être restaurés et validés avant de considérer la suite de tests comme opérationnelle.

## 6. Répartition de l'équipe – GROUPE 10 B2A

Le travail est réparti entre cinq membres afin de permettre le développement parallèle des différentes parties du logiciel.

| Membre | Domaine principal | Responsabilités |
|---|---|---|
| Membre 1 | Dashboard et Explorer | Navigation, Activity Bar, Sidebar, explorateur de fichiers, ouverture des dossiers et navigation dans l'arborescence |
| Membre 2 | Architecture et éditeur | Architecture générale, Core, démarrage WPF, intégration AvalonEdit, gestion des documents et coloration syntaxique |
| Membre 3 | Actions et édition | Menus, onglets, actions sur les fichiers, sauvegarde, recherche et remplacement, raccourcis clavier |
| Membre 4 | Exécution et terminal | Détection de Python, exécution des scripts, gestion des sorties et erreurs, arrêt de l'exécution, terminal CMD / PowerShell |
| Membre 5 | Interface graphique | Design, thème sombre, boutons, menus, onglets, panneaux, composants visuels et icônes |

Le document **`SUIVI_EQUIPE_KEYCE_KODER.docx`**, disponible à la racine du dépôt, contient le suivi détaillé des responsabilités, de l'avancement connu et des tâches restantes.

## 7. Organisation du travail avec Git

Le travail commun de base est préparé sur la branche `main`. Une fois cette base validée et poussée sur le dépôt distant, chaque membre crée sa branche à partir du dernier `main`.

### Branches prévues

- `feature/explorer-dashboard`
- `feature/core-editor` (pour les évolutions ultérieures du socle éditeur)
- `feature/ui-actions`
- `feature/runner-terminal`
- `feature/ui-design`

### Règles de collaboration

1. Récupérer les derniers changements de `main`.
2. Créer ou mettre à jour sa branche de fonctionnalité.
3. Développer les fonctionnalités attribuées.
4. Effectuer des commits avec des messages explicites.
5. Compiler et tester les modifications avant de les proposer.
6. Soumettre une Pull Request vers `main`.
7. Faire vérifier les changements avant leur intégration.

La branche `main` constitue la base commune du projet. Il faut éviter de modifier les mêmes fichiers sans coordination entre les membres.

## 8. Règles de contribution

- Respecter les responsabilités définies dans le document de suivi.
- Garder `KEYCE_KODER.Core` indépendant de WPF et des composants d'interface.
- Éviter les dépendances circulaires entre projets.
- Ne pas présenter une fonctionnalité inachevée comme terminée.
- Compiler le projet concerné avant de proposer une intégration.
- Décrire clairement les changements dans les commits et Pull Requests.
- Ajouter des tests lorsque cela est pertinent.
- Communiquer avec les autres membres avant de modifier un contrat ou un fichier partagé.

## 9. État du projet et prochaines étapes

Les prochaines priorités sont :

1. Finaliser et valider le socle commun, puis le pousser sur `main`.
2. Faire créer les branches de travail à partir de cette base.
3. Développer et intégrer l'explorateur de fichiers et le dashboard.
4. Développer les menus, les onglets, la sauvegarde et les actions d'édition.
5. Développer le Runner Python et le terminal intégré.
6. Construire les composants visuels et harmoniser le thème.
7. Tester les parcours complets et corriger les problèmes d'intégration.

---

**KEYCE KODER – GROUPE 10 B2A**  
*Projet collaboratif de développement d'un environnement de développement intégré.*
