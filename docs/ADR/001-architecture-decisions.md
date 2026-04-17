# ADR-001: EduConnect Architecture Decisions

**Date:** 2026-04-03
**Status:** CONFIRMED
**Product:** EduConnect
**Company:** Future Beyond Technology (FBT) / FIROSE Enterprises, Chennai, India

---

## Context

EduConnect is a school communication platform replacing WhatsApp groups, paper circulars, and verbal messages with a single trusted digital system for attendance, homework, and notices. This ADR captures all foundational architecture decisions made during Product Genesis.

Implementation status note: the checked-in repo currently contains the Next.js web app and the .NET API only. The earlier Expo/mobile track remains deferred, the generated TypeScript API schema now lives under `packages/api-client/src/generated/`, and current setup/deployment details live in `docs/SETUP.md` and `docs/RAILWAY_DEPLOYMENT.md`.

**Stack:** Next.js 15 web app | .NET 8 (Minimal API) | PostgreSQL (Railway)
**Team:** Solo developer
**Timeline:** 6–8 weeks to MVP

---

## ADR-01: Monolith Strategy

**Decision:** Modular Monolith
**Options Considered:** Monolith, Modular Monolith, Microservices
**Reason:** Solo developer needs deployment velocity, but EduConnect has 3 distinct bounded contexts (Attendance, Homework, Notices) that benefit from clean internal module boundaries. Single deployable, clean separation.
**Reversal Cost:** Moderate — well-bounded modules can be extracted to services later.

## ADR-02: Architecture Pattern (Backend)

**Decision:** Vertical Slice Architecture + CQRS + MediatR
**Options Considered:** VSA, Clean Architecture + CQRS, Layered
**Reason:** Feature folders own everything (handler, validator, request/response, mapping). No layered ceremony. CQRS separates queries from commands inside each slice. MediatR dispatches. Speed of VSA with read/write separation of CQRS.
**Reversal Cost:** Low — slices can be refactored into Clean Arch layers additively.

## ADR-03: Database Strategy

**Decision:** PostgreSQL on Railway
**Options Considered:** PostgreSQL, MongoDB, SQLite
**Reason:** Deeply relational data (students → classes → teachers → attendance). ACID guarantees for immutable records. Append-only audit trails. Railway provides managed zero-ops hosting.
**Reversal Cost:** VERY HIGH — one-way door. Database migration on live data is a quarter-long project.

## ADR-04: Multi-Tenancy Model

**Decision:** Row-level isolation with `school_id` on every table
**Options Considered:** Row-level, Schema-level, Database-level
**Reason:** Simplest and cheapest. PostgreSQL RLS as DB-layer safety net. Schema-per-school and DB-per-school are operationally unjustified at this scale.
**Reversal Cost:** VERY HIGH — changing tenancy model on live multi-school data is extremely painful.

## ADR-05: Auth Strategy

**Decision:** Custom JWT (inline in .NET 8 API)
**Options Considered:** Zentra (FBT), NextAuth.js v5, Clerk, Custom JWT
**Reason:** Parents authenticate via Phone + PIN (4-6 digit). Teachers/Admins authenticate via Phone + Password. This doesn't fit standard providers. Custom JWT gives full control over PIN verification, role claims, and strict token rules (short-lived access token, refresh in HttpOnly Secure cookie with rotation). The current frontend restores access-token state from `localStorage` on page refresh while relying on the refresh cookie for renewal.
**Reversal Cost:** HIGH — auth migration forces all users to re-authenticate.

## ADR-06: Frontend Rendering Strategy

**Decision:** Next.js 15 App Router with client-rendered authenticated views
**Options Considered:** SSR, SSG + ISR, CSR, PPR
**Reason:** The current checked-in app uses App Router layouts plus a client-side auth provider to hydrate role-based dashboard routes after session restore. Public/auth routes stay build-friendly, while authenticated routes favor straightforward client data fetching over explicit PPR configuration.
**Reversal Cost:** Low — rendering strategy can change per-route.

## ADR-07: API Contract Versioning

**Decision:** Extend-only for MVP; add URL versioning (/v1/) when a breaking change is needed
**Options Considered:** URL versioning, Header versioning, Extend-only
**Reason:** We control both client and server. Extend-only discipline (add fields, never rename/remove) is sufficient until the first breaking change. Adding versioning later is additive.
**Reversal Cost:** Low.

---

## Summary Table

| ADR | Decision | Reversal Cost |
|-----|----------|---------------|
| 01 Monolith Strategy | Modular Monolith | Moderate |
| 02 Arch Pattern | VSA + CQRS + MediatR | Low |
| 03 Database | PostgreSQL (Railway) | Very High |
| 04 Multi-Tenancy | Row-level (school_id) | Very High |
| 05 Auth | Custom JWT (inline) | High |
| 06 Rendering | Next.js App Router + client auth context | Low |
| 07 API Versioning | Extend-only for MVP | Low |
