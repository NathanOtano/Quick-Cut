# Sécurité

## Signaler un problème

Pour signaler une vulnérabilité ou un comportement dangereux, ouvrez une issue GitHub en décrivant :

- la version concernée ;
- les étapes de reproduction ;
- l’impact attendu ;
- les fichiers ou captures utiles, sans partager de données sensibles.

## Données locales

Quick Cut est conçu comme un outil local-first. Les captures, journaux, bases SQLite et préférences utilisateur doivent rester sur la machine de l’utilisateur et ne doivent pas être committés.

## Publication

Chaque release publique doit être construite depuis l’arbre public, accompagnée d’une empreinte SHA-256 et vérifiée par les contrôles de `RELEASE_GATES.md`.
