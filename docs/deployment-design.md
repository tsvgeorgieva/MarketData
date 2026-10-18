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

Four GitHub Actions pipelines:

| Pipeline | When | What it does |
| --- | --- | --- |
| **PR check** | Pull request | Runs the tests and builds the image, but pushes nothing |
| **Build** | Push to `main` | Runs the tests, builds the image and pushes it to the registry, tagged with the commit |
| **Deploy** | After a successful build on `main`, or manually with an image tag | Updates the Container App to that image and checks that it is healthy |
| **Infrastructure** | Manually, and on changes to the Bicep files on `main` | Previews the changes to the Azure environment, then applies them after approval |

- **Why a separate PR check:** a pull request is validated without registry access, so unreviewed code never reaches the registry.
- **Why build and deploy are separate:** an image is built once and deployed many times. Rolling back means redeploying an older tag, with no rebuild.
- **Why tests run before the image is pushed:** only tested code ever reaches the registry.
- **Why infrastructure has its own pipeline:** the environment changes rarely and for different reasons than the app, so an app deploy never touches it. The preview step shows what will change before it does.
- **Why an approval gate on deploy and infrastructure:** production changes need a deliberate go-ahead.

## Known limits

- Each deploy restarts the service and drops the Binance connection. Candles closed during the restart are not backfilled yet; this is a future improvement.
- Multi-region deployment and reliability are out of scope for now.