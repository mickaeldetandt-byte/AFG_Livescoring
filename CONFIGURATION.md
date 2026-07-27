# Configuration locale et variables d'environnement

L'application charge sa configuration avec le mécanisme standard
d'ASP.NET Core. Aucun secret de production ne doit être ajouté aux fichiers
`appsettings*.json` suivis par Git.

## Variable obligatoire

La chaîne de connexion SQL Server doit être fournie avec la variable
d'environnement suivante :

```text
ConnectionStrings__DefaultConnection
```

Exemple fictif pour PowerShell :

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=tcp:sql.example.invalid,1433;Initial Catalog=ExampleDatabase;User ID=example_user;Password=EXAMPLE_ONLY_CHANGE_ME;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
dotnet run
```

Les valeurs `example.invalid`, `ExampleDatabase`, `example_user` et
`EXAMPLE_ONLY_CHANGE_ME` sont uniquement des exemples. Elles ne correspondent
à aucun environnement réel.

## Développement local

`appsettings.Development.json` contient une connexion Windows LocalDB sans mot
de passe. Elle est chargée automatiquement lorsque
`ASPNETCORE_ENVIRONMENT=Development`.

Une surcharge locale peut aussi être placée dans `appsettings.Local.json` ou
`appsettings.Development.Local.json`. Ces fichiers sont exclus par
`.gitignore`. Pour qu'ASP.NET Core les charge, ajoutez-les explicitement à la
configuration au démarrage ou copiez leur valeur dans une variable
d'environnement. La méthode recommandée reste la variable d'environnement.

## Production

Définir `ConnectionStrings__DefaultConnection` dans le gestionnaire de
configuration de la plateforme d'hébergement. Ne pas placer la valeur réelle
dans :

- `appsettings.json` ;
- `appsettings.Development.json` ;
- un profil de publication ;
- un fichier `.env` suivi par Git ;
- la documentation ;
- une commande enregistrée dans le dépôt.

Après retrait d'un secret déjà publié, sa suppression du fichier ne suffit
pas : le secret doit être considéré comme compromis et remplacé dans
l'environnement concerné. Cette rotation est une opération d'exploitation
séparée et n'est pas réalisée par cette modification.

## Vérification avant commit

Avant tout commit, vérifier au minimum :

```powershell
git diff --check
git grep -n -I -E "Password=|AdminPin|ConnectionStrings.*DefaultConnection"
```

Une occurrence dans cet exemple documentaire doit rester manifestement
fictive. Toute autre occurrence doit être examinée avant validation.
