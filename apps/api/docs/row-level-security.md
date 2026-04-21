# Row-level security (Phase 4)

Every tenanted table in the EduConnect database carries a PostgreSQL
row-level-security policy of the form:

```sql
USING (
  current_app_school_id() IS NULL
  OR school_id = current_app_school_id()
)
WITH CHECK (
  current_app_school_id() IS NULL
  OR school_id = current_app_school_id()
)
```

`current_app_school_id()` is a `STABLE SQL` function that reads the session
GUC `app.current_school_id`. When the GUC is unset or empty the function
returns `NULL`, which the policy treats as a bypass — matching the pre-Phase 4
EF global-query-filter semantics (unauthenticated requests can see
everything, tenant scoping applies only when a tenant is known). Every
authenticated request path sets the GUC via `TenantConnectionInterceptor`.

## Enforcing RLS in a deployed environment

The migration enables `ROW LEVEL SECURITY` and `FORCE ROW LEVEL SECURITY`
on every tenanted table. `FORCE` subjects the table owner to policies too —
**but** roles with the `BYPASSRLS` attribute (and all superusers) still
bypass RLS at the PostgreSQL level. That is a Postgres invariant, not
something the application can override.

For RLS to actually isolate tenants in production you must connect the
application to a runtime role that has **neither** `BYPASSRLS` nor
`SUPERUSER`:

```sql
-- Run once as a superuser on the target database.
CREATE ROLE app_runtime LOGIN PASSWORD :'runtime_password' NOSUPERUSER NOBYPASSRLS;
GRANT CONNECT ON DATABASE educonnect TO app_runtime;
GRANT USAGE ON SCHEMA public TO app_runtime;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_runtime;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO app_runtime;
-- Future tables created by migrations inherit these grants:
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_runtime;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO app_runtime;
```

Then point `DATABASE_URL` at the `app_runtime` user for the running API
process. Keep migrations running as the owner/superuser so DDL continues
to succeed.

## Verifying RLS is effective

Integration tests in `TenantIsolationRlsTests.cs` validate the policies
against a real PostgreSQL. They auto-skip when:

- `EDUCONNECT_TEST_DB_URL` is unset, **or**
- the connection role has `BYPASSRLS` / `SUPERUSER` (RLS cannot be
  enforced in that configuration).

Run locally once you have a restricted role in place:

```bash
EDUCONNECT_TEST_DB_URL="Host=...;Port=...;Database=educonnect;Username=app_runtime;Password=..." \
  dotnet test --filter FullyQualifiedName~TenantIsolationRls
```

A green run proves all three invariants:

1. `SELECT` with no `WHERE school_id` returns only the current tenant's rows.
2. `UPDATE` by primary key against a foreign tenant's row affects 0 rows.
3. `INSERT` with a foreign `school_id` is rejected with `SQLSTATE 42501`.

## Anonymous / cross-tenant endpoints

Today's anonymous request paths (login, login-parent, refresh, forgot/reset
password) legitimately need to look users up by phone without a known
tenant. The `TenantConnectionInterceptor` clears `app.current_school_id`
to the empty string on those connections, which makes
`current_app_school_id()` return `NULL` and therefore makes the RLS
policy admit every row — the same behaviour as the existing EF global
query filter's `!IsAuthenticated` branch.

This is safe because these endpoints are a hand-curated surface, each of
which already performs its own narrow lookup (`WHERE phone = @p`,
`WHERE token_hash = @h`, …). If an endpoint ever legitimately needs to
serve cross-tenant **authenticated** reads (e.g. a super-admin dashboard),
it should open its queries under a connection where the GUC has been
explicitly cleared, gated behind an explicit `[RequireSuperAdmin]`
authorisation check. Never the default path.

## App-level filtering remains

`AppDbContext.OnModelCreating` still applies per-entity global query
filters on `SchoolId`. RLS is defence in depth — the two layers must
agree. Do not remove the EF filters as part of Phase 4 or later.
