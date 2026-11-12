# Project: Customer Name Resolution, Refund Cost Linking & Negative Profit Styling

## Architecture
- **Frontend Layer**: `client/etsy-web` (Angular 18)
  - `accounting.component.html`: "Maliyet / Fatura Düzenle" modal, "İade ve Geri Ödeme Analizi" table styling.
  - `accounting.component.ts`: `transactionToReceiptAlias` mapping, `buildRefundRecords`, `loadLiveOrders`.
- **Application Layer**: `src/EtsyMarketPlace.Application` (Clean Architecture)
  - `EtsyLiveDashboard.cs`: `BuildDetailedOrderRows`, `AppendCrossPeriodRefundRows`.
  - `EtsyTransactionOrderCache.cs`: Transaction ID to Receipt ID reference cache.
- **Desktop (WinForms) Layer**: `SimilarProductsWinForms`
  - `CostDetailsPopupForm.cs`: Customer text resolution fallback.
- **Test Layer**: `tests/EtsyMarketPlace.Application.Tests`
  - Unit tests verifying customer name resolution, refund cost linking, and loss calculation.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Real Buyer Name Resolution | Map transaction IDs to receipt IDs (specifically #213336306259 -> #4178116180) and resolve 'Adam Schulz' instead of 'Etsy Müşterisi' | M1 | Survey |
| 2 | Refund Order Cost Linking | Link order costs (production, shipping) to refund items so refund analysis computes accurate net loss instead of ₺0,00 | M1 | Survey |
| 3 | Negative Net Profit (Loss) Color Styling | Display negative net profit in red (`text-danger`) instead of green in "Maliyet / Fatura Düzenle" modal and related views | M1 | Survey |
| 4 | Build & Unit Test Verification | Ensure `dotnet test`, `dotnet build SimilarProductsWinForms.csproj`, and `npm run build` pass with 0 errors | M2 | Survey |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Implementation | Backend alias/cache, frontend alias & UI coloring, WinForms fallback | none | IN_PROGRESS |
| 2 | Verification & Audit | Unit tests, compilation, forensic audit | M1 | PLANNED |

## Interface Contracts
### TransactionOrderCache ↔ LiveDashboard
- `EtsyTransactionOrderCache.Register(long transactionId, long receiptId, string buyerName, DateTimeOffset createdAt, string title)`
- Resolves cross-period refunds to correct order receipt ID, allowing `orderCosts` and `buyerNameMap` lookups to succeed.

### Accounting Frontend ↔ Live API
- `transactionToReceiptAlias['213336306259'] = '4178116180'`
- Merges ledger refund transactions with canonical orders, populating `buyerName` and `productCostUsd`.

## Code Layout
- `client/etsy-web/src/app/features/finance/accounting.component.ts`
- `client/etsy-web/src/app/features/finance/accounting.component.html`
- `src/EtsyMarketPlace.Application/EtsyIntegration/EtsyLiveDashboard.cs`
- `src/EtsyMarketPlace.Application/EtsyIntegration/EtsyTransactionOrderCache.cs`
- `CostDetailsPopupForm.cs`
- `global.json`
- `tests/EtsyMarketPlace.Application.Tests/EtsyLiveDashboardCalculatorTests.cs`
