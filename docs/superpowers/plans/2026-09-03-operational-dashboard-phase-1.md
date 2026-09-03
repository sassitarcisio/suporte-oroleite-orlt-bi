# Operational Dashboard Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver dense, responsive operational views for Dashboard, Analytics, and standard Seller Closing without changing financial calculations.

**Architecture:** Preserve React feature boundaries and existing API contracts. Use a shared CSS visual vocabulary, purpose-specific card layouts, and the current `ClosingSummary` data to form a financial demonstrative.

**Tech Stack:** React 19, TypeScript, Vite, Vitest, Bootstrap, Font Awesome, CSS.

**Spec:** `docs/superpowers/specs/2026-09-03-operational-dashboard-redesign.md`

## Global Constraints

- Use Manrope, mineral ivory, graphite, pine green, burnt gold, and petrol blue.
- Preserve authentication, seller catalog, and every existing API contract.
- Do not create financial values when closing configuration is missing.
- Write and run a failing web test before each component behavior change.

### Task 1: Compact Dashboard KPI Layout

**Files:** `src/OroBI.Web/src/features/dashboard/DashboardPage.tsx`, `src/OroBI.Web/src/App.css`, `src/OroBI.Web/src/App.test.tsx`

- [ ] Add a failing test that expects `data-testid="dashboard-metrics"` after applying a seller filter.
- [ ] Run `npm.cmd test -- --run src/App.test.tsx` and verify the selector is absent.
- [ ] Render `.dashboard-layout` and `.dashboard-metrics`; make gross sales primary, net result petrol, negative movements gold, and quantity neutral. Keep metric values tabular and unbroken.
- [ ] Add responsive CSS for four, two, and one columns.
- [ ] Run `npm.cmd test -- --run && npm.cmd run build`.
- [ ] Commit with `feat: redesign dashboard metric hierarchy`.

### Task 2: Content-Aware Analytics Cards

**Files:** `src/OroBI.Web/src/features/analytics/AnalyticsPage.tsx`, `src/OroBI.Web/src/App.css`, `src/OroBI.Web/src/App.test.tsx`

- [ ] Add a failing test that opens `Trocas` and expects `data-testid="analysis-metrics"` with `analysis-metrics-3`.
- [ ] Run `npm.cmd test -- --run src/App.test.tsx` and verify the test fails.
- [ ] Render the existing response fields in an `.analysis-metrics` grid with count class, one primary card, compact secondary cards, icons, and no unused grid track.
- [ ] Run `npm.cmd test -- --run && npm.cmd run build`.
- [ ] Commit with `feat: refine operational analytics cards`.

### Task 3: Financial Closing Demonstrative

**Files:** `src/OroBI.Web/src/features/closings/ClosingsPage.tsx`, `src/OroBI.Web/src/App.css`, `src/OroBI.Web/src/App.test.tsx`

- [ ] Add a failing test that submits the existing test closing and expects `data-testid="closing-financial-summary"`.
- [ ] Run `npm.cmd test -- --run src/App.test.tsx` and verify the selector is absent.
- [ ] Render financial groups for salary plus commission, prizes, and expected monthly total from `ClosingSummary`; render PPP, revenue, positivity, trade, salary, and commission in a compact detail grid.
- [ ] Retain the configuration-required state when the API returns no closing.
- [ ] Run `npm.cmd test -- --run && npm.cmd run build`.
- [ ] Commit with `feat: present closing financial summary`.

### Task 4: Verify And Publish

**Files:** `src/OroBI.Web/src/App.css`, `src/OroBI.Web/src/App.test.tsx`

- [ ] Run `npm.cmd test -- --run`, `npm.cmd run build`, and `git diff --check`.
- [ ] Push the Phase 1 commits to `main`.
- [ ] Confirm the public bundle contains `dashboard-metrics`, `analysis-metrics`, and `closing-financial-summary`.
