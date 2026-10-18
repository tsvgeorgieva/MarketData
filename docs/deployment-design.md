# Deployment design

A proposal for running MarketData on Azure with CI/CD. Nothing here is deployed. The cost research is tracked in [issue #12](https://github.com/tsvgeorgieva/MarketData/issues/12).

## Architecture

```
GitHub Actions ── build ──▶ Azure Container Registry
       │                            │ pull
       └─ deploy ─▶ Container App (1 replica) ──▶ Azure SQL
                        │        └─ Key Vault (admin API key)
                        └─▶ Binance (outbound)
```

| Component | Choice | Why |
| --- | --- | --- |
| Host | Container Apps, `minReplicas = maxReplicas = 1` | The repo already has a [Dockerfile](../Dockerfile). The app is a 24/7 WebSocket client, so it must never scale to zero, and a second replica would duplicate ingestion |
| Database | Azure SQL, DTU S0 (about USD 14.5/month) | Constant ingestion keeps serverless awake, so it would cost about USD 209/month for no benefit |
| Database login | Managed identity (Entra), no password | Nothing to store, leak or rotate |
| Admin API key | Key Vault reference, pinned version | The only secret left. An unpinned reference restarts the app on rotation and drops the Binance connection |
| Registry | Azure Container Registry (Basic) | Native pull with the managed identity |
| Infrastructure | Bicep in `infra/` | Native to Azure, no state file |
| Migrations | `Database__MigrateOnStartup=true` | Safe with one replica and needs no extra job. Revisit if replicas go above one |

App Service would be cheaper (about USD 13 against 47/month) and also works. Container Apps was chosen because:

- **Same artifact everywhere.** The image tested locally with `docker compose` and in CI is the one that runs in Azure. Nothing is rebuilt or repackaged per environment.
- **Rollback is built in.** Every deploy is a revision, so reverting means re-activating the previous one.
- **Safer secret failure.** An App Service Key Vault reference that cannot be resolved is passed to the app as the literal reference text. That text is longer than the 16-character minimum, so it would be accepted as the admin API key. The Container Apps docs do not describe this failure mode, so confirm on the first deploy that a missing secret stops the revision.
- **Explicit always-on.** `minReplicas = 1` is a declared setting in the template. On App Service, Always On is off by default and the app unloads after 20 minutes without requests, which silently stops ingestion.

## Set up once

1. `az deployment group create -f infra/main.bicep` creates all the resources.
2. Grant the app's managed identity access to the database (`CREATE USER ... FROM EXTERNAL PROVIDER`, `db_owner`, because the app runs its own migrations) and `Key Vault Secrets User` on the vault.
3. Add a federated credential for `main` to a deployment identity (`Contributor` on the resource group, `AcrPush` on the registry). Store its IDs as GitHub repository variables.

Connection string: `Server=<server>.database.windows.net;Database=MarketData;Authentication=Active Directory Default;User Id=<identity-client-id>`. Health probes use `/health/live`, which does not touch the database, so a database outage does not restart the app.

## Pipelines

Two workflows, so a build can be repeated or promoted without rebuilding, and a rollback needs no build.

| | `build.yml` | `deploy.yml` |
| --- | --- | --- |
| Trigger | Pull request and push to `main` | Successful `build.yml` run on `main`, or manual with an image tag |
| Does | `dotnet test`, then build the image. On `main` also push it as `marketdata:<commit SHA>` | Point the Container App at the given tag, then check `/health/live` |
| Azure access | Only `AcrPush`, and only on `main` | `Contributor` on the resource group |

- **Tests gate the image.** Hosted Ubuntu runners have Docker, so the Testcontainers integration tests run unchanged.
- **SHA tags, never `latest`.** Every revision maps to a commit. Rollback is running `deploy.yml` manually with an older tag.
- **OIDC login.** The pipelines hold no Azure secret.
- **`production` environment** on `deploy.yml` lets the repository require a manual approval.
- **Infrastructure** changes go through a separate manual workflow, so an app change cannot alter the environment.

## Known limits

- Every deploy restarts the host. The Binance connection drops and the existing startup logic backfills the gap.
- The Binance client still needs polish before production (see [engineering-notes.md](engineering-notes.md)).
- Single region, no alerting. First alert to add: no candles stored in 5 minutes.
