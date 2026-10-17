# Azure hosting options for the MarketData deployment proposal

Issue: tsvgeorgieva/MarketData#12. Researched 2026-10-17. Primary sources only (Microsoft Learn, Azure pricing pages, Azure Retail Prices API).

Workload: one ASP.NET Core 10 host = 24/7 Binance WebSocket client `BackgroundService` + HTTP API, backed by SQL Server (Azure SQL in the proposal). The proposal is a document; nothing is deployed.

## Summary / recommendation

- The workload is a continuously running process, so any "scale to zero" feature is a hazard, not a saving. Both Container Apps and App Service can host it; both need an explicit "always running" setting.
- Container Apps (Consumption): set `minReplicas` >= 1. Roughly USD 47/month for 0.5 vCPU / 1 GiB always active (calculation below). Closest fit for a container-first proposal.
- App Service (Linux): turn **Always On** on (needs a non-Free tier). B1 is about USD 13/month at list price; P1v3 about USD 130/month. Cheapest and simplest for one always-on host.
- Azure SQL: serverless auto-pause will never trigger with a continuous ingester, and serverless cost at the 0.5 vCore minimum is high for a constant load. For a proposal, DTU Basic (about USD 4.8/month) or Standard S0 (about USD 14.5/month) is the low-cost option; vCore General Purpose provisioned is the step up.
- Secrets: managed identity + Key Vault (`Key Vault Secrets User` role) on either platform; for Azure SQL prefer Entra (managed identity) authentication over a password.

## 1. Container Apps vs App Service

### Container Apps

- Default scale limits: min replicas 0, max 10. [scale-app]
- Key hazard: "If ingress is disabled and you don't define a `minReplicas` or a custom scale rule, your container app scales to zero and has no way of starting back up." Set `minReplicas` to 1 or more to ensure an instance is always running. [scale-app]
- Scale rules are HTTP (concurrent requests), TCP (concurrent connections) or custom KEDA (CPU, memory, queues...). These measure inbound requests/connections to the revision; an outbound WebSocket client (our Binance connection) is not inbound traffic and creates no scale signal. This is an inference from the rule definitions in [scale-app], not an explicit statement in the docs.
- Single-instance concern (inference): scaling the revision beyond one replica would run multiple Binance WebSocket clients and duplicate ingestion. Cap `maxReplicas` at 1, or separate the ingester from the API later.
- Billing (Consumption plan): per-second vCPU-seconds and GiB-seconds, plus HTTP requests. Free each calendar month per subscription: 180,000 vCPU-seconds, 360,000 GiB-seconds, 2 million requests. No charge while scaled to zero. [billing]
- Idle rate applies only when the revision is at the minimum replica count (> 0) and a replica has started containers, no HTTP requests, < 0.01 vCPU and < 1,000 bytes/s network. A live market-data stream will normally exceed these thresholds, so assume the active rate (inference from the thresholds). [billing]
- Rates (Azure Retail Prices API, West Europe, USD, Consumption): vCPU active 0.000034/s, vCPU idle 0.000004/s, memory active 0.000004 per GiB-s, memory idle 0.000004 per GiB-s, requests 0.56 per million. Dedicated plan management 0.10/hour (only with Dedicated workload profiles). Environment-level meters of 0.143/hour also exist; [billing] ties private endpoints and planned maintenance to the "Dedicated Plan Management" charge, so check the calculator for your network setup. [retail-api]
- All .NET apps on Container Apps need ASP.NET Core data protection enabled (relevant if the API ever scales out). [scale-app]

### App Service

- An App Service plan defines OS, region, VM count/size and tier; you pay per VM instance for dedicated tiers (Basic, Standard, Premium...). Free/Shared tiers share VMs and use CPU quotas. [hosting-plans]
- **Always On**: off by default; "the app is unloaded after 20 minutes without any incoming requests". When on, the front end pings the app root every five minutes. Required for continuous WebJobs. [configure-common]
- A `BackgroundService` runs inside the app process, so it is only reliable while the process is loaded; Always On is therefore needed for the ingester (inference from the unload behaviour). The pricing page states Free tier has no SLA and is not supported for production workloads. [pricing-app-service]
- A WebSockets setting exists in general settings (relevant to inbound sockets such as SignalR; the outbound Binance connection does not need it). [configure-common]
- The app runs on all VM instances in the plan, so scale-out would duplicate the ingester; keep the plan at 1 instance. [hosting-plans]
- List prices (Azure Retail Prices API, West Europe, USD, Linux, per hour): B1 0.018, P1v3 0.178; about 13.1 and 129.9 per month at 730 h. P0v3 was not returned by the query; confirm on the pricing page. [retail-api]

### Fit comparison

| Aspect | Container Apps (Consumption) | App Service (Linux, Basic+) |
|---|---|---|
| Always-running setting | `minReplicas >= 1` | Always On (not on Free/Shared) |
| Default if misconfigured | Scales to zero, may never restart (no ingress) | Unloads after 20 min without requests |
| Billing | per-second vCPU/GiB, free grants | per VM instance per hour |
| Approx. monthly (0.5 vCPU/1 GiB vs B1/P1v3) | ~USD 47 | ~USD 13 (B1), ~USD 130 (P1v3) |
| Needs container image | Yes | Optional |
| Key Vault integration | Key Vault reference in app secrets | Key Vault reference in app settings |

Container Apps worked example (30 days = 2,592,000 s, 0.5 vCPU, 1 GiB, always active, West Europe list price, free grants applied): vCPU (1,296,000 - 180,000) x 0.000034 = 37.95; memory (2,592,000 - 360,000) x 0.000004 = 8.93; total about USD 46.9. Estimate only; requests and egress excluded.

## 2. Azure SQL tier options and approximate cost

- vCore model: provisioned compute (billed per hour for allocated capacity) or serverless (billed per second for compute used). Tiers: General Purpose, Business Critical, Hyperscale. General Purpose is the budget-oriented tier; Business Critical costs roughly 2.7x General Purpose. [service-tiers-vcore]
- Serverless: General Purpose and Hyperscale only, Gen5 hardware only, not available in DTU. Min vCores default 0.5. Auto-pause delay default 60 min (General Purpose minimum 15 min); when paused only storage is billed. Resume is delayed and apps need retry logic. When usage is below the configured minimum, compute is billed at the minimum vCores. [serverless]
- Implication: a 24/7 ingester writing continuously keeps the database active, so auto-pause never triggers and you pay at least the minimum vCores continuously. At 0.573934 USD per vCore-hour (West Europe list, Gen5), 0.5 vCore always on is about USD 209/month before storage (calculation from [retail-api] and [serverless]). Serverless suits intermittent workloads, not this one. Provisioned General Purpose prices were not retrievable with the query used; check the pricing page.
- DTU tiers (list price, West Europe, USD per day, [retail-api]): Basic 0.161 (about 4.8/month), S0 0.4839 (about 14.5/month), S1 0.9677 (about 29/month). Cheapest options for a proposal. Reservations are vCore-only. [service-tiers-vcore]
- Free offer: per database 100,000 vCore-seconds, 32 GB data and 32 GB backup per month, up to 10 General Purpose databases per subscription. 100,000 vCore-seconds is about 28 hours of one vCore per month, so it does not cover a 24/7 ingester. After the limit the database either auto-pauses until next month or is billed at standard serverless rates. Fine for a demo only. [free-offer]

## 3. Key Vault and managed identity

- Managed identities remove credential management and are usable at no extra cost. Two kinds: system-assigned (tied to the resource lifecycle) and user-assigned (standalone, reusable). [managed-identities]
- Container Apps: a secret can be a Key Vault reference resolved with the app's managed identity, which needs the Azure RBAC role Key Vault Secrets User. Unversioned references pick up new versions within 30 minutes and active revisions referencing the secret in env vars restart automatically; pin a version for full control. With CLI `create` a system-assigned identity cannot be used (it does not exist yet), so use a user-assigned identity. Docs advise Key Vault references over inline secret values in production. [manage-secrets]
- App Service: app settings/connection strings can be Key Vault references (`@Microsoft.KeyVault(SecretUri=...)`); uses the system-assigned identity by default (user-assigned via `keyVaultReferenceIdentity`); grant Key Vault Secrets User (RBAC) or Get permission (access policy). Without a pinned version the value refreshes within 24 hours (cached, refetched every 24 h; any config change restarts the app and refetches). An unresolved reference is passed through as the literal string. [kv-references]
- Both platforms: network-restricted vaults require the app to reach the vault through a virtual network. [kv-references]
- Managed identities can replace passwords for any Entra-authenticated service, which includes Azure SQL, so the SQL password can be removed entirely; the Binance API key (if any) remains a Key Vault secret. [managed-identities]

## 4. Always-on / scale-to-zero implications for a 24/7 ingester

- Container Apps: set `minReplicas = 1` (and `maxReplicas = 1` to avoid duplicate WebSocket clients). Without it, no-ingress apps scale to zero and cannot restart; zero replicas means no charge but also no ingestion. During platform upgrades extra replicas can appear temporarily (pre-warm), so the ingester should tolerate brief duplication (dedupe on insert). [scale-app]
- App Service: enable Always On; Free/Shared tiers are unsuitable. Keep instance count at 1 unless ingestion is idempotent or leader-elected. [configure-common, hosting-plans]
- Azure SQL: do not rely on serverless auto-pause for the ingest database (it will not pause and bills the minimum). Use connection retry logic regardless, since auto-resume and failovers cause transient errors. [serverless]
- Either platform restarts the host on config/secret changes (App Service config change; Container Apps unversioned Key Vault secret rotation), so the WebSocket client must reconnect and backfill gaps on startup. [kv-references, manage-secrets]

## Open items / not verified

- Azure pricing pages render prices client-side; exact prices were taken from the Azure Retail Prices API (list price, USD, West Europe, queried 2026-10-17) and may differ by region, agreement and currency.
- App Service P0v3 and provisioned SQL General Purpose prices were not retrieved.
- Points marked "inference" are conclusions drawn from documented behaviour, not explicit doc statements.

## Sources

- [scale-app] https://learn.microsoft.com/en-us/azure/container-apps/scale-app
- [billing] https://learn.microsoft.com/en-us/azure/container-apps/billing
- [manage-secrets] https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets
- [configure-common] https://learn.microsoft.com/en-us/azure/app-service/configure-common
- [hosting-plans] https://learn.microsoft.com/en-us/azure/app-service/overview-hosting-plans
- [kv-references] https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references
- [managed-identities] https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview
- [service-tiers-vcore] https://learn.microsoft.com/en-us/azure/azure-sql/database/service-tiers-sql-database-vcore?view=azuresql
- [serverless] https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-overview?view=azuresql
- [free-offer] https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer?view=azuresql
- [pricing-app-service] https://azure.microsoft.com/en-us/pricing/details/app-service/windows/
- [retail-api] https://prices.azure.com/api/retail/prices (Azure Retail Prices API; serviceName 'Azure Container Apps' / 'Azure App Service' / 'SQL Database', armRegionName westeurope)
