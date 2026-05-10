# Contrôles de publication

Objectif : publier une version utilisable par n’importe qui sans fuite de données personnelles, de configuration locale ou d’historique privé.

## Mode du dépôt

- Ce dépôt est un canal de release public.
- Le code source privé et l’historique de développement restent hors de ce dépôt tant qu’un export source-public n’a pas été explicitement demandé et validé.
- Les binaires sont ajoutés uniquement comme assets de GitHub Release.

## Gate 1 - Export propre

- Partir d’un commit identifié dans le dépôt de travail privé.
- Exporter uniquement les fichiers nécessaires à l’artefact public.
- Exclure les profils utilisateur, chemins locaux, journaux, caches, captures, clés, fichiers de configuration personnels et exports Drive non publics.

## Gate 2 - Scan

- Chercher les chemins locaux Windows et cloud.
- Chercher les noms, courriels, URLs internes et identifiants.
- Chercher les fichiers volumineux ou binaires non prévus.
- Le résultat du scan doit être conservé dans les notes internes de release.

## Gate 3 - Build vierge

- Construire depuis une copie propre ou un environnement nettoyé.
- Ne pas réutiliser un état applicatif local.
- Réinitialiser les calendriers, profils, préférences, chemins, comptes et caches.

## Gate 4 - Installation

- Installer l’artefact sur un profil utilisateur propre.
- Vérifier que le premier lancement ne dépend d’aucun fichier local privé.
- Vérifier que la configuration utilisateur peut être créée depuis zéro.
- Calculer et publier SHA-256 pour chaque asset.

## Gate 5 - Publication

- Créer un tag versionné.
- Attacher les assets à une GitHub Release.
- Vérifier l’URL de release, le nom des assets, leur taille et leur empreinte.
