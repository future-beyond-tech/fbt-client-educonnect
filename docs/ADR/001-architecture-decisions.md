# ADR-001: EduConnect Architecture Decisions

**Date:** 2026-04-03
**Status:** CONFIRMED
**Product:** EduConnect
**Company:** Future Beyond Technology (FBT) / FIROSE Enterprises, Chennai, India

---

## Context

EduConnect is a school communication platform replacing WhatsApp groups, paper circulars, and verbal messages with a single trusted digital system for attendance, homework, and notices. This ADR captures all foundational architecture decisions made during Product Genesis.

**Stack:** Next.js 15 + Expo | .NET 8 (Minimal API) | PostgreSQL (Railway)
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
**Reason:** Parents authenticate via Phone + PIN (4-6 digit). Teachers/Admins authenticate via Phone + Password. This doesn't fit standard providers. Custom JWT gives full control over PIN verification, role claims, and strict token rules (access ≤ 15min, refresh in HttpOnly Secure cookie, rotation on refresh).
**Reversal Cost:** HIGH — auth migration forces all users to re-authenticate.

## ADR-06: Frontend Rendering Strategy

**Decision:** PPR (Partial Prerendering, Next.js 15)
**Options Considered:** SSR, SSG + ISR, CSR, PPR
**Reason:** Static shell renders instantly (FCP < 1.5s), dynamic islands hydrate with authenticated data. The static shell IS the skeleton screen — eliminates CLS and hits Doherty Threshold without extra work.
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
| 06 Rendering | PPR (Next.js 15) | Low |
| 07 API Versioning | Extend-only for MVP | Low |
