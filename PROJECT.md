# Project: VDS Deployment Pipeline, Accounting Negative Loss Styling & Refund Data Resolution

## Architecture
- **API & Serving Layer**: `src/EtsyMarketPlace.Api`
  - `Program.cs`: `StaticFileOptions` with `OnPrepareResponse` setting `Cache-Control: no-cache, no-store, must-revalidate` on `index.html` and long-term immutable caching on hashed `.js`/`.css`.
  - `Controllers/SystemController.cs`: Explicit `no-cache` headers on root SPA fallback response.
- **CI/CD & Deployment Layer**: `.github/workflows`
  - `vds_development_release.yml`: Add Node.js setup, `npm run build`, and copy to `src/EtsyMarketPlace.Api/wwwroot` before packaging `EtsyMarketPlace.Api.zip`.
  - `deploy-api-vds.yml`: Ensure unified Angular build and deploy steps.
- **Application & Domain Layer**: `src/EtsyMarketPlace.Application`
  - `EtsyTransactionOrderCache.cs`: Dual-alias registration (`213336308259L` and `213336306259L` -> `4178116180L`), `TransactionOrderReference` with cost storage, and in-memory seed for "Adam Schulz" + $38.31.
  - `EtsyLiveDashboard.cs`: Canonicalize parsed regex IDs in `BuildRefundEntries`, provide cost fallback ($38.31: $15.31 product + $23.00 shipping) in `AppendCrossPeriodRefundRows`.
- **Frontend Layer**: `client/etsy-web` (Angular 18)
  - `accounting.component.html`:
    - Top KPI card 8: title displays `🔴 GERÇEK NET ZARAR` with `text-danger` class when `realNetProfitTry < 0`.
    - Summary bar: "Mağaza Net" bound to `text-danger` when negative.
    - Orders table row: Customer cell calls `getDisplayBuyerName(o)`.
    - Orders table row: Cost cell displays `-$38.31` in red font instead of `—`.
  - `accounting.component.css`: `.kpi-card.highlight.danger .kpi-title` red styling.
  - `accounting.component.ts`: Dual-alias map (`213336308259` & `213336306259`), fallback cost seeding ($38.31) in `buildRefundRecords` and `loadLiveOrders`.
- **Test Layer**: `tests/EtsyMarketPlace.Application.Tests`
  - Unit tests verifying dual-alias resolution, buyer name "Adam Schulz", and cost $38.31.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | VDS Cache-Busting & Static File Headers | Add `Cache-Control: no-cache, no-store, must-revalidate` for `index.html` in `Program.cs` and `SystemController.cs` | M1 | Survey (Obs 1) |
| 2 | CI/CD Angular Build Integration | Ensure `vds_development_release.yml` builds Angular client and copies into `wwwroot` | M1 | Survey (Obs 2) |
| 3 | Dual-ID Alias & Cost Caching Backend | Register `213336308259` and `213336306259` -> `4178116180`, seed "Adam Schulz" & $38.31 in `EtsyTransactionOrderCache` & `EtsyLiveDashboard` | M1 | Survey (Obs 4) |
| 4 | Accounting UI Loss Styling & Red Title | Top KPI card displays `🔴 GERÇEK NET ZARAR` in red, "Mağaza Net" in red when negative | M1 | Survey (Obs 5) |
| 5 | Order Table Row Customer & Cost Display | Call `getDisplayBuyerName(o)` on table cell, render `-$38.31` in red instead of `—` | M1 | Survey (Obs 5) |
| 6 | Automated Unit Tests & Verification | Add unit test theories for both IDs; verify `dotnet test`, `dotnet build`, `npm run build` | M2 | Survey |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Full Implementation | Backend cache/live dashboard, API static headers, CI/CD workflow, Angular UI & styling | none | IN_PROGRESS |
| 2 | Review & Challenger Verification | Independent code review, adversarial testing, and build/test execution | M1 | PLANNED |
| 3 | Forensic Integrity Audit | Systematic integrity check verifying zero mocks, zero facades, clean logic | M2 | PLANNED |

## Interface Contracts
### Static File Serving Contract
- Request for `/index.html` or SPA fallback:
  - Header: `Cache-Control: no-cache, no-store, must-revalidate`
  - Header: `Pragma: no-cache`
  - Header: `Expires: 0`
- Request for `*.js`, `*.css`:
  - Header: `Cache-Control: public, max-age=31536000, immutable`

### Transaction Order Cache Contract
- `RegisterAlias(213336308259L, 4178116180L)`
- `RegisterAlias(213336306259L, 4178116180L)`
- `SeedKnownAlias(..., "Adam Schulz", ..., productCost: 15.31m, shippingCost: 23.00m)`

### Frontend Accounting Contract
- `transactionToReceiptAlias['213336308259'] = '4178116180'`
- `transactionToReceiptAlias['213336306259'] = '4178116180'`
- `getDisplayBuyerName(order)` returns `'Adam Schulz'` for both IDs.
- Order cost fallback resolves $38.31 for both IDs.

## Code Layout
- `src/EtsyMarketPlace.Api/Program.cs`
- `src/EtsyMarketPlace.Api/Controllers/SystemController.cs`
- `.github/workflows/vds_development_release.yml`
- `src/EtsyMarketPlace.Application/EtsyIntegration/EtsyTransactionOrderCache.cs`
- `src/EtsyMarketPlace.Application/EtsyIntegration/EtsyLiveDashboard.cs`
- `client/etsy-web/src/app/features/finance/accounting.component.html`
- `client/etsy-web/src/app/features/finance/accounting.component.css`
- `client/etsy-web/src/app/features/finance/accounting.component.ts`
- `tests/EtsyMarketPlace.Application.Tests/EtsyLiveDashboardCalculatorTests.cs`
