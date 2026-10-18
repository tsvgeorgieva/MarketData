# Deployment design

A proposal for running MarketData on Azure with CI/CD. Nothing here is deployed.

## 1. Azure components

| Component | Why |
| --- | --- |
| Azure Container Apps, one replica, always running | The repo already builds a container image, and the same image runs locally, in CI and in Azure. The service is a 24/7 Binance client, so it must never scale to zero, and a second replica would duplicate ingestion. Every deploy is a revision, which gives rollback |
| Azure Container Registry | Stores one image per commit, so any version can be redeployed |
| Azure SQL Database, small fixed tier | Ingestion is constant, so serverless would never pause and would cost far more than a fixed tier |
| Managed identity | The app reaches the registry, the database and Key Vault without passwords |
| Key Vault | Keeps secrets out of the pipeline, the image and the configuration |
| Log Analytics | Collects the application logs |

## 2. Infrastructure as code

The environment is defined in **Bicep**. It can be recreated from the repository, and changes to it are reviewed like code. Bicep is native to Azure and needs no state file or extra tooling. It is applied by its own workflow, separate from the application, so an app change cannot alter the infrastructure.

## 3. CI/CD

Two GitHub Actions pipelines:

| Pipeline | When | What it does |
| --- | --- | --- |
| **Build** | Pull request and push to `main` | Runs the tests and builds the image. On `main` it pushes the image to the registry, tagged with the commit |
| **Deploy** | After a successful build on `main`, or manually with an image tag | Updates the Container App to that image and checks that it is healthy |

- **Why two pipelines:** an image is built once and deployed many times. Rolling back means redeploying an older tag, with no rebuild.
- **Why tests run before the image is pushed:** only tested code ever reaches the registry.
- **Why no stored Azure credentials:** the pipelines sign in with a federated identity, so there is no secret to leak or rotate.
- **Why an approval gate on deploy:** production changes need a deliberate go-ahead.

## Known limits

- Each deploy restarts the service. It reconnects to Binance and backfills the gap.
- Single region and no alerting. First alert to add: no candles stored for 5 minutes.
