# Deployment design

A proposal for running MarketData on Azure with CI/CD. Nothing here is deployed. The cost research is tracked in [issue #12](https://github.com/tsvgeorgieva/MarketData/issues/12).

## 1. Azure components

```
GitHub Actions ── build ──▶ Azure Container Registry
       │                            │ pull
       └─ deploy ─▶ Container App (1 replica) ──▶ Azure SQL
                        │        └─ Key Vault (secrets)
                        └─▶ Binance (outbound)
```

| Component | Why |
| --- | --- |
| Container Apps environment and app, `minReplicas = maxReplicas = 1` | The repo already has a [Dockerfile](../Dockerfile), and the image tested locally and in CI is the one that runs in Azure. The app is a 24/7 WebSocket client, so it must never scale to zero, and a second replica would duplicate ingestion. Every deploy is a revision that can be re-activated for rollback. App Service would be cheaper (about USD 13 against 47/month) and also works |
| Azure Container Registry (Basic) | Holds the images, one per commit. The app pulls with its managed identity, so there is no registry password |
| Azure SQL Database, DTU S0 (about USD 14.5/month) | Constant ingestion keeps serverless awake, so serverless would cost about USD 209/month for no benefit |
| User-assigned managed identity | One identity for registry pull, Key Vault read and database login. The database uses Entra authentication, so there is no password to store, leak or rotate |
| Key Vault | Keeps secrets out of the pipeline, the image and the app configuration. References are pinned to a version, because an unpinned one restarts the app on rotation and drops the Binance connection |
| Log Analytics workspace | Collects the console logs of the app. Needed to see why a revision failed |

Migrations run at startup (`Database__MigrateOnStartup=true`). That is safe with one replica and needs no extra job. Revisit it if replicas ever go above one.

Health probes use `/health/live`, which does not touch the database, so a database outage does not restart the app.

## 2. Infrastructure as code

All of the above is defined in **Bicep**, in `infra/`, and applied with `az deployment group create`.

- **Why IaC at all:** the environment can be recreated from the repository, and a change to it is reviewed like code.
- **Why Bicep:** it is native to Azure, has no state file to store or lock, and needs no tool beyond the Azure CLI.
- **Deployed separately from the app:** a manual workflow applies `infra/`, so an application change cannot alter the environment.

After the first deployment, two things are done once by hand: grant the managed identity access to the database (`CREATE USER ... FROM EXTERNAL PROVIDER`, `db_owner`, because the app runs its own migrations), and add the secrets to Key Vault.

## 3. CI/CD

Two workflows on GitHub Actions. They are separate so a build can be reused without rebuilding, and a rollback needs no build.

| | `build.yml` | `deploy.yml` |
| --- | --- | --- |
| Trigger | Pull request and push to `main` | Successful `build.yml` run on `main`, or manual with an image tag |
| Does | `dotnet test`, then build the image. On `main` also push it as `marketdata:<commit SHA>` | Point the Container App at the given tag, then check `/health/live` |
| Azure access | `AcrPush`, and only on `main` | `Contributor` on the resource group |

- **Tests gate the image.** Hosted Ubuntu runners have Docker, so the Testcontainers integration tests run unchanged.
- **SHA tags, never `latest`.** Every revision maps to a commit. Rollback is running `deploy.yml` manually with an older tag.
- **OIDC login.** A federated credential for `main` means the pipelines hold no Azure secret.
- **`production` environment** on `deploy.yml` lets the repository require a manual approval.

## Known limits

- Every deploy restarts the host. The Binance connection drops and the existing startup logic backfills the gap.
- The Binance client still needs polish before production (see [engineering-notes.md](engineering-notes.md)).
- Single region, no alerting. First alert to add: no candles stored in 5 minutes.
