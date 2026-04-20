# Push Notifications — Integration Guide

EduConnect now delivers every Parent/Staff/Admin notification straight to the user's phone (and desktop browser) via the Web Push standard. This doc explains how the pieces fit together and what you need to do to turn it on in each environment.

## Architecture

```
┌──────────────┐     1. subscribe + VAPID key        ┌───────────────┐
│   apps/web   │ ◄──────────────────────────────────►│   apps/api    │
│  (Next.js)   │                                     │   (.NET 8)    │
│              │     2. POST /api/push/subscriptions │               │
│  sw.js       │ ───────────────────────────────────►│ user_push_    │
│  pushClient  │                                     │  subscriptions│
└──────┬───────┘                                     └──────┬────────┘
       │                                                    │
       │                                                    │ 3. NotificationService.SendAsync(...)
       │                                                    │    saves DB row + calls IPushSender
       │                                                    ▼
       │                                             ┌──────────────┐
       │       4. push delivered over HTTPS          │ WebPushSender│
       │    ◄────────────────────────────────────────│ (WebPush lib)│
       ▼                                             └──────────────┘
┌────────────────┐
│ FCM / APNs /   │
│ Mozilla autopush│
└────────────────┘
```

Every existing call to `NotificationService.SendAsync` / `SendBatchAsync` now also fans out a push to every active subscription the user has. No handler code changed — leave-approved, notice-published, homework-assigned, attendance-marked all pick up push automatically.

## One-time setup

### 1. Generate VAPID keys

VAPID keys are a single keypair used for every push. Generate them once per environment (dev, staging, prod).

Easiest option — Node one-liner:

```bash
npx web-push generate-vapid-keys
```

You'll get:

```
Public Key:  BKd...
Private Key: 0fT...
```

### 2. Configure the API

Either set them in `apps/api/src/EduConnect.Api/appsettings.json`:

```json
"WebPush": {
  "Enabled": true,
  "Subject": "mailto:support@educonnect.app",
  "PublicKey": "BKd...",
  "PrivateKey": "0fT..."
}
```

…or (recommended for prod) leave appsettings blank and set env vars:

```bash
VAPID_PUBLIC_KEY=BKd...
VAPID_PRIVATE_KEY=0fT...
VAPID_SUBJECT=mailto:support@educonnect.app
```

If the private key is missing, the API boots with `NullPushSender` — the in-app bell still works, push silently no-ops.

### 3. Run the database migration

The new entity `UserPushSubscriptionEntity` needs a table. From the api folder:

```bash
cd apps/api/src/EduConnect.Api
dotnet ef migrations add AddUserPushSubscriptions
dotnet ef database update
```

### 4. Restore + build

```bash
cd apps/api
dotnet restore
dotnet build
```

`WebPush 1.0.12` pulls in automatically via the csproj change.

## Wiring the web client

Two small additions to `apps/web`:

1. In the **authenticated layout** (e.g. `apps/web/app/(dashboard)/layout.tsx`), mount `<PushAutoSubscriber />` so logged-in users silently re-register their subscription on every visit:

    ```tsx
    import { PushAutoSubscriber } from "@/components/push/PushAutoSubscriber";

    export default function DashboardLayout({ children }: { children: React.ReactNode }) {
      return (
        <>
          <PushAutoSubscriber />
          {children}
        </>
      );
    }
    ```

2. On a **profile/settings page**, drop in `<EnableNotificationsButton />` so users can opt-in the first time and opt-out later:

    ```tsx
    import { EnableNotificationsButton } from "@/components/push/EnableNotificationsButton";

    export default function SettingsPage() {
      return <EnableNotificationsButton />;
    }
    ```

That's it — no changes needed to `sw.js` registration (the existing `ServiceWorkerRegistrar` already handles that).

## How it flows end-to-end

1. User logs in → `PushAutoSubscriber` mounts.
2. If permission is already granted, it calls `ensurePushSubscription()` which subscribes silently.
3. If permission is `default`, nothing happens until the user clicks `EnableNotificationsButton`, which triggers `Notification.requestPermission()` and subscribes.
4. The subscription (endpoint URL + keys) is POSTed to `/api/push/subscriptions` and stored against the user.
5. Any time anywhere in the backend calls `INotificationService.SendAsync(...)`, `WebPushSender` fans out a signed payload to every subscription that user has.
6. The browser's push service (FCM / APNs / Mozilla autopush) wakes up `sw.js`, which calls `showNotification(...)`.
7. User taps the notification → `notificationclick` focuses or opens a tab at the deep-link URL (`/notices/{id}`, `/leaves/{id}`, etc.).

## iOS (Safari) caveats

iOS 16.4+ supports Web Push, but **only if the PWA is added to the Home Screen first**. The `EnableNotificationsButton` component detects this and shows a friendly install hint instead of the enable button. You may want to add a one-time banner on iOS prompting parents/staff to install, or include the instruction in your onboarding.

Users on iOS 16.3 or older will simply see the install hint with no enable option — consistent with what Apple supports.

## Security notes

- The VAPID **private key is a secret** — never commit it, never expose it to the browser. It lives only in server env vars / appsettings.
- The VAPID public key is served by `GET /api/push/vapid-public-key` (anonymous) — it's designed to be public.
- Every subscription is scoped to one `SchoolId` + `UserId`. The query filter on `AppDbContext` prevents cross-tenant reads. `WebPushSender` bypasses it intentionally (`IgnoreQueryFilters`) because the sender may run in non-user contexts (batch notifications, background ops), but only after filtering by explicit user IDs passed in.
- Stale subscriptions (HTTP 410/404 from the push service) are deleted on every send, so revoked browsers don't pile up.

## Files at a glance

**Backend (new):**
- `Infrastructure/Database/Entities/UserPushSubscriptionEntity.cs`
- `Infrastructure/Database/Configurations/UserPushSubscriptionConfiguration.cs`
- `Infrastructure/Services/IPushSender.cs`
- `Infrastructure/Services/WebPushSender.cs` (+ `NullPushSender`)
- `Infrastructure/Services/WebPushOptions.cs`
- `Features/Push/GetVapidPublicKey/GetVapidPublicKeyEndpoint.cs`
- `Features/Push/RegisterPushSubscription/*` (command, handler, validator, endpoint)
- `Features/Push/UnregisterPushSubscription/*` (command, handler, endpoint)

**Backend (edited):**
- `EduConnect.Api.csproj` — added `WebPush` package
- `Program.cs` — DI for `IPushSender`, `WebPushOptions`
- `Common/Extensions/EndpointRouteBuilderExtensions.cs` — `MapPushEndpoints()`
- `Infrastructure/Database/AppDbContext.cs` — new DbSet + query filter
- `Infrastructure/Services/NotificationService.cs` — fan-out push after DB write
- `appsettings.json` — `WebPush` section

**Web (new):**
- `apps/web/lib/push/pushClient.ts`
- `apps/web/components/push/EnableNotificationsButton.tsx`
- `apps/web/components/push/PushAutoSubscriber.tsx`

**Web (edited):**
- `apps/web/public/sw.js` — appended `push`, `notificationclick`, `pushsubscriptionchange` handlers

## Rollback

If anything goes sideways, set `WebPush:Enabled = false` in appsettings (or leave `VAPID_PRIVATE_KEY` empty). The server will use `NullPushSender`, the DB rows still get written, the in-app bell keeps working. No code redeploy required.
