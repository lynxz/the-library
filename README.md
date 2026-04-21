# The Library

The Library is a Vue frontend with a .NET 8 Azure Functions backend. The frontend is deployed to Azure Static Web Apps and the API is deployed to an Azure Function App.

## Local development

### Frontend

Create a local environment file from [.env.example](.env.example) when you want the Vite dev server to call the local Functions host.

```bash
cp .env.example .env.local
```

### Backend

Create [api/local.settings.json](api/local.settings.json) from [api/local.settings.example.json](api/local.settings.example.json).

Required local settings:

- `AzureWebJobsStorage`
- `FUNCTIONS_WORKER_RUNTIME`
- `StorageConnectionString`
- `JwtSigningKey`

For local development this project is set up to use Azurite. Keep [api/local.settings.json](api/local.settings.json) local only and never commit it.

### Run locally

```bash
npm ci
npm run dev
```

In a second terminal:

```bash
cd api
dotnet build
func host start
```

## Secret handling

Do not commit secrets to the repository.

Local-only files:

- [api/local.settings.json](api/local.settings.json)
- `.env.local`
- `.env.*.local`

Safe to commit:

- [.env.production](.env.production) when it only contains public build-time values such as `VITE_API_BASE`
- [api/local.settings.example.json](api/local.settings.example.json)
- [.env.example](.env.example)

Production runtime secrets used by the API come from [api/Program.cs](api/Program.cs):

- `StorageConnectionString`
- `JwtSigningKey`

Store those in Azure Function App application settings or, preferably, as Azure Key Vault references. The GitHub workflow does not set those values and does not need to know them.

## GitHub deployment

Production deployment is defined in [.github/workflows/deploy-production.yml](.github/workflows/deploy-production.yml). It runs on pushes to `main` and can also be triggered manually.

The workflow:

1. Builds the Vue frontend.
2. Builds and publishes the .NET Functions app.
3. Logs in to Azure with GitHub OpenID Connect.
4. Deploys the Function App package.
5. Fetches the Static Web App deployment token from Azure at runtime.
6. Deploys the frontend from `dist`.

### GitHub configuration

Create a GitHub environment named `production` and add these environment variables:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

These values are identifiers, not secrets. The workflow reads them through GitHub Actions `vars`.

### Azure configuration for OIDC

Create an Entra application or service principal for GitHub Actions and add a federated credential for this repository and branch.

Recommended subject format for production deploys:

```text
repo:<owner>/<repo>:ref:refs/heads/main
```

Grant the identity access to the resource group that contains:

- `func-the-library`
- `swa-the-library`

`Contributor` at the resource group scope is the simplest starting point. Tighten roles later if you want a narrower permission set.

### Azure runtime configuration

Before the first GitHub deployment, make sure the Function App already has these app settings configured in Azure:

- `StorageConnectionString`
- `JwtSigningKey`

If you use Key Vault references, also enable a managed identity on the Function App and grant it `get` access to the required secrets.

## Production seeding

Production seeding stays manual and is intentionally separate from continuous deployment.

For local Azurite seeding, the script still falls back to development credentials. For non-development storage you must supply both passwords explicitly.

Example:

```powershell
$env:STORAGE_CONNECTION_STRING = "<production-storage-connection-string>"
$env:SEED_ADMIN_PASSWORD = "<strong-admin-password>"
$env:SEED_READER_PASSWORD = "<strong-reader-password>"
node tools/seed-data.mjs
```

If you use [deploy.ps1](deploy.ps1) with `-SeedData`, set `SEED_ADMIN_PASSWORD` and `SEED_READER_PASSWORD` in the shell first.

## Before first push

1. Verify [api/local.settings.json](api/local.settings.json) has never been committed.
2. Rotate any secret that may already have been exposed locally or in Git history.
3. Keep the repository private until the first secret audit is complete.
4. Enable GitHub secret scanning and push protection after the repository is created.
