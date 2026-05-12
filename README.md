# Quick Cut

Quick Cut est un outil Windows local-first pour capturer l’écran, annoter rapidement une image et garder un flux de capture léger.

## Télécharger

La version publique est distribuée via les releases GitHub :

- dépôt : https://github.com/NathanOtano/Quick-Cut
- assets : archive Windows `QuickCut-win-x64-<version>.zip`
- intégrité : fichier `SHA256SUMS.txt` joint à chaque release

## Lancer depuis l’archive

1. Télécharger l’archive de la dernière release.
2. Décompresser le dossier.
3. Lancer `QuickCut.Capture.exe`.

Si Windows demande un runtime, installez le .NET Desktop Runtime 10 x64.

Quick Cut crée ses données utilisateur localement au premier lancement. Aucune capture, base locale, préférence personnelle ou journal d’exécution n’est versionné dans ce dépôt.

## Compiler depuis le source

Pré-requis :

- Windows 10 ou plus récent ;
- SDK .NET 10.

Commandes utiles :

```powershell
dotnet restore QuickCut.sln
dotnet build QuickCut.sln -c Release
dotnet test QuickCut.sln -c Release
```

Pour produire un dossier publiable :

```powershell
dotnet publish .\src\QuickCut.Capture\QuickCut.Capture.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\QuickCut-win-x64 -p:DebugType=None -p:DebugSymbols=false -p:IncludeSourceRevisionInInformationalVersion=false
```

## Périmètre du dépôt

Ce dépôt contient le source applicatif, les tests, les assets publics et la documentation publique. Les captures, journaux, caches et données runtime générées par l’utilisateur restent hors versionnement.
