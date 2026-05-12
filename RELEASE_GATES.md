# Contrôles de publication publique

Objectif : publier une version utilisable sans fuite de données personnelles, de configuration locale ou de fichiers de développement non nécessaires.

## Gates obligatoires

1. Export allowlist uniquement : solution, source applicatif, tests, assets publics et docs publiques.
2. Aucun fichier runtime local dans le dépôt public.
3. Scan des secrets, chemins locaux, données personnelles et fichiers non publics avant push.
4. Build et tests depuis l’arbre public.
5. Asset construit depuis l’arbre public sans symboles de build locaux, puis hash SHA-256 publié.
6. Tag versionné et GitHub Release avec notes publiques.

## Surfaces exclues

- captures, bases SQLite, journaux et caches ;
- notes de développement non publiques et fichiers machine-locaux ;
- chemins locaux, profils utilisateur, secrets, tokens et configurations machine.
