/// <reference lib="webworker" />

const CACHE_NAME = "educonnect-v1";

// Static assets to pre-cache on install
const PRECACHE_ASSETS = ["/login", "/offline"];

// Install: pre-cache shell
self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll(PRECACHE_ASSETS))
      .then(() => self.skipWaiting())
  );
});

// Activate: clean old caches
self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(
          keys
            .filter((key) => key !== CACHE_NAME)
            .map((key) => caches.delete(key))
        )
      )
      .then(() => self.clients.claim())
  );
});

// Fetch: network-first for API, cache-first for static assets
self.addEventListener("fetch", (event) => {
  const { request } = event;
  const url = new URL(request.url);

  // Skip non-GET requests
  if (request.method !== "GET") return;

  // Skip API calls and auth endpoints — always go to network
  if (url.pathname.startsWith("/api/")) return;

  // For navigation requests: network-first with offline fallback
  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request)
        .then((response) => {
          const clone = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
          return response;
        })
        .catch(() =>
          caches
            .match(request)
            .then((cached) => cached || caches.match("/offline"))
        )
    );
    return;
  }

  // For static assets: cache-first
  // /_next/static/ is intentionally excluded — Next.js serves those with
  // content-hashed filenames and immutable Cache-Control headers, so the
  // browser HTTP cache handles them correctly. Caching them in the SW
  // caused stale JS chunks to be served after a new deploy, producing
  // "Failed to find Server Action" errors in production.
  if (
    url.pathname.match(/\.(png|jpg|jpeg|svg|ico|woff2?)$/) &&
    !url.pathname.startsWith("/_next/")
  ) {
    event.respondWith(
      caches.match(request).then(
        (cached) =>
          cached ||
          fetch(request).then((response) => {
            const clone = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
            return response;
          })
      )
    );
    return;
  }
});

// --- Web Push -------------------------------------------------------------
//
// Server sends a JSON payload shaped as PushPayload on the API side:
//   { title, body, url, type, entityId, entityType }
// We render it via the Notifications API and, on tap, focus or open a tab at
// the payload's `url` so the user lands on the relevant screen.

const PUSH_FALLBACK_ICON = "/icon-192x192.png";
const PUSH_FALLBACK_BADGE = "/icon-72x72.png";

self.addEventListener("push", (event) => {
  let payload = {};
  try {
    payload = event.data ? event.data.json() : {};
  } catch {
    payload = {
      title: "EduConnect",
      body: event.data ? event.data.text() : "",
    };
  }

  const title = payload.title || "EduConnect";
  const options = {
    body: payload.body || "",
    icon: PUSH_FALLBACK_ICON,
    badge: PUSH_FALLBACK_BADGE,
    tag: payload.type || "educonnect-general",
    renotify: true,
    data: {
      url: payload.url || "/notifications",
      type: payload.type,
      entityId: payload.entityId,
      entityType: payload.entityType,
    },
  };

  event.waitUntil(
    Promise.all([
      self.registration.showNotification(title, options),
      // Nudge any open tabs so they can refresh their inbox/badge without a
      // full reload. Pages opt-in by listening for navigator.serviceWorker.
      self.clients
        .matchAll({ type: "window", includeUncontrolled: true })
        .then((clients) => {
          for (const client of clients) {
            client.postMessage({ type: "push-received", payload });
          }
        }),
    ])
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const target =
    (event.notification.data && event.notification.data.url) || "/";

  event.waitUntil(
    self.clients
      .matchAll({ type: "window", includeUncontrolled: true })
      .then((clientList) => {
        for (const client of clientList) {
          const clientUrl = new URL(client.url);
          const targetUrl = new URL(target, self.registration.scope);
          if (clientUrl.origin === targetUrl.origin && "focus" in client) {
            client.navigate(targetUrl.toString()).catch(() => {});
            return client.focus();
          }
        }
        return self.clients.openWindow(target);
      })
  );
});

// Browser-initiated resubscribe (VAPID rotation etc.). Page code should
// listen for this and POST the new subscription to the server.
self.addEventListener("pushsubscriptionchange", (event) => {
  event.waitUntil(
    self.clients
      .matchAll({ type: "window", includeUncontrolled: true })
      .then((clients) => {
        for (const client of clients) {
          client.postMessage({ type: "pushsubscriptionchange" });
        }
      })
  );
});
