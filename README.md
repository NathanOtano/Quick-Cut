# Quick Cut

Canal public de distribution de Quick Cut.

Ce dépôt ne contient pas le code source privé, les données locales, les journaux, les exports de travail ni les binaires internes. Il sert à publier des versions installables quand elles ont passé les contrôles de publication.

## État

Aucune version publique n’est publiée pour l’instant.

## Ce qui sera publié ici

- notes de version publiques ;
- installateurs ou archives finalisés ;
- manifeste de build public ;
- empreinte SHA-256 des fichiers distribués.

## Contrôles avant publication

Chaque version doit respecter `RELEASE_GATES.md`. En résumé : exporter depuis un état propre, supprimer toute donnée personnelle ou locale, scanner l’arbre public, reconstruire l’installateur depuis une configuration vierge, puis vérifier l’installation sur un profil utilisateur propre.
