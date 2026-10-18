# Deployment design

A proposal for running MarketData on Azure and delivering it with CI/CD. Nothing here is deployed. The cost and platform research behind the choices is in [research/azure-hosting.md](research/azure-hosting.md).

## Target architecture

```
GitHub Actions ──push image──▶ Azure Container Registry
      │                                │ pull (managed identity)
      └──az containerapp update──▶ Container App (1 replica) ──▶ Azure SQL
                                       │      ▲                    (Entra auth)
                                       │      └── Key Vault (admin API key)
                                       └──▶ Binance WebSocket + REST (outbound)
```

| Component | Choice |
| --- | --- |
| Hosting | Azure Container Apps, Consumption plan, `minReplicas = 1`, `maxReplicas = 1`, external HTTPS ingress |
| Image registry | Azure Container Registry (Basic) |
| Database | Azure SQL Database, DTU Standard S0 |
| Secrets | Key Vault reference for `Admin__ApiKey`; no database password |
| Identity | One user-assigned managed identity for the app (ACR pull, Key Vault read, SQL access) |
| Logs | Log Analytics workspace (Serilog console output is collected automatically) |
| Infrastructure as code | Bicep, in `infra/` |

## Why these decisions

- **Container Apps, not App Service.** The repo already ships a [Dockerfile](../Dockerfile) that ends TLS at the ingress, so the same image runs locally and in Azure. App Service is cheaper (about USD 13 against 47 per month) and would also work; Container Apps wins on image portability and built-in revisions for rollback. If cost matters more, switch the host and keep the rest of this design.
- **One replica, always on.** The host is a 24/7 Binance WebSocket client plus the API. Without `minReplicas >= 1` a Container App scales to zero and ingestion stops. More than one replica would run duplicate WebSocket clients, so `maxReplicas` is capped at 1. During platform upgrades a second replica can briefly appear, which is tolerable only because inserts are expected to be idempotent.
- **Azure SQL on a fixed DTU tier, not serverless.** Continuous ingestion keeps a serverless database awake, so auto-pause never saves money and the 0.5 vCore minimum costs about USD 209 per month. S0 is about USD 14.5 per month, matches the small constant write load, and can be raised in place.
- **Entra authentication to SQL, no password.** The managed identity connects with `Authentication=Active Directory Default` in the connection string, so there is no database credential to store, leak or rotate.
- **Key Vault reference for the admin key.** It is the only remaining secret. A reference keeps it out of the pipeline, the image and the revision template. Pin the secret version, otherwise a rotation silently restarts the app (and so the Binance connection).
- **Migrate on startup, kept.** `Database__MigrateOnStartup=true` is safe because there is exactly one replica, and it removes a separate migration job. Revisit this if the replica count ever goes above one.
- **Bicep.** It is native to Azure, needs no state file or extra tooling, and the whole environment can be recreated from the repository.
- **GitHub Actions with OIDC.** The code and the tickets already live on GitHub. A federated credential means the pipeline holds no Azure secret at all.

## Deploy it (one time)

1. Create a resource group and run `az deployment group create -f infra/main.bicep`. It creates the registry, identity, Key Vault, SQL server and database, Log Analytics workspace, Container Apps environment and the app.
2. Set the Entra admin on the SQL server, then grant the app identity access to the database: `CREATE USER [<identity-name>] FROM EXTERNAL PROVIDER; ALTER ROLE db_owner ADD MEMBER [<identity-name>];`. The role is `db_owner` rather than read/write only because the app applies its own migrations at startup.
3. Store the admin key in Key Vault and grant the identity the `Key Vault Secrets User` role.
4. Create a federated credential for the repository's `main` branch on a deployment identity. Give it `Contributor` on the resource group and `AcrPush` on the registry. Save its client, tenant and subscription IDs as GitHub repository variables (they are identifiers, not secrets).

App settings on the Container App:

| Setting | Value |
| --- | --- |
| `ConnectionStrings__MarketData` | `Server=<server>.database.windows.net;Database=MarketData;Authentication=Active Directory Default;User Id=<identity-client-id>` |
| `Database__MigrateOnStartup` | `true` |
| `Admin__ApiKey` | Key Vault reference |

Probes: liveness and startup on `/health/live`, port 8080. That endpoint checks only that the process is up, so a database outage does not restart the app and drop the Binance connection.

## CI/CD pipeline

| Workflow | Trigger | Steps |
| --- | --- | --- |
| `ci.yml` | Pull request | restore, build, `dotnet test`, build the Docker image without pushing |
| `deploy.yml` | Push to `main` | test, build and push the image tagged with the commit SHA, deploy, smoke check |

`deploy.yml` in outline:

```yaml
permissions: { id-token: write, contents: read }
jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: production
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { global-json-file: global.json }
      - run: dotnet test
      - uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      - run: az acr build -r ${{ vars.ACR_NAME }} -t marketdata:${{ github.sha }} .
      - run: |
          az containerapp update -g ${{ vars.RESOURCE_GROUP }} -n marketdata \
            --image ${{ vars.ACR_NAME }}.azurecr.io/marketdata:${{ github.sha }}
      - run: curl --fail --retry 10 --retry-delay 6 https://${{ vars.APP_FQDN }}/health/live
```

Why it is shaped this way:

- **Tests gate the deploy.** Hosted Ubuntu runners include Docker, so the Testcontainers integration tests run unchanged.
- **Build in Azure with `az acr build`.** No Docker login or registry credential is needed on the runner.
- **Tag with the commit SHA, never `latest`.** Every revision maps to a commit, and a rollback is re-activating the previous revision (`az containerapp revision activate`).
- **`environment: production`.** Lets the repository require a manual approval before the deploy job runs.
- **Smoke check last.** A failed health check fails the run, so a bad revision is noticed immediately instead of by missing candles.
- **Infrastructure is deployed separately** (a manual or path-filtered workflow on `infra/**`), so an application change cannot alter the environment.

## Known limits

- A new revision restarts the host, so the Binance connection drops and reconnects. The gap is backfilled by the existing startup logic, but this is why deploys go through one replica at a time and not blue/green.
- The Binance client is still marked as needing polish for production in [engineering-notes.md](../engineering-notes.md).
- Single region, no database geo-replication, no alerting. Add an alert on "no candles stored in 5 minutes" first.
