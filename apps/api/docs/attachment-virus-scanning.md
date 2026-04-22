# Attachment virus scanning (Phase 5)

Every file that reaches the download surface has been streamed through a
virus scanner first. The pipeline has four moving parts:

1. **`attachments.status`** — Pending / Available / Infected / ScanFailed.
   Enforced by a check constraint; indexed on the non-`Available` subset
   so operator queries for "what's stuck" are cheap.
2. **`IAttachmentScanner`** — pluggable interface with two implementations:
   `ClamAvAttachmentScanner` (production) and `NoOpAttachmentScanner`
   (dev/CI). The NoOp implementation logs at Warning on every call so
   production can't accidentally ship with it.
3. **`ChannelAttachmentScanQueue`** — bounded in-memory channel (capacity
   256). Single-instance; horizontal scaling requires moving to a durable
   queue (Redis LIST / PG LISTEN/NOTIFY).
4. **`AttachmentScanWorker`** — `BackgroundService` that drains the queue,
   streams each row through the scanner, and updates status. Infected
   objects are also deleted from storage; `ScanFailed` objects are kept
   for operator review.

Enqueueing today happens inside `AttachFileToEntity` — i.e. the moment
the client links the uploaded file to a homework or notice. A standalone
`/mark-uploaded` endpoint is a planned follow-up; it would let clients
fire scans right after the S3/R2 PUT completes (rather than at attach
time).

## Flow for a new upload

```
Client                          API                            Storage
  │── POST request-upload-url-v2 ▶│
  │                              │── INSERT attachments (Pending)
  │◀── { uploadUrl, attachmentId }│   (no storageKey — client PUTs directly)
  │── PUT file ──────────────────────────────────────────────▶│
  │── POST attach ───────────────▶│
  │                              │── HeadObject(key) — verify size/type
  │                              │── UPDATE attachments (linked)
  │                              │── ChannelAttachmentScanQueue.Enqueue(id)
  │◀── 200 OK ───────────────────│
                                  │                             │
                                  │       (worker)              │
                                  │── GetObject(key) ──────────▶│
                                  │◀── stream ──────────────────│
                                  │── magic-byte pre-check
                                  │── INSTREAM to clamd ──▶ reply
                                  │── UPDATE attachments (Available|Infected|ScanFailed)
                                  │── (if Infected) DeleteObject(key) ─▶│
```

`GetAttachmentsForEntity` only mints a presigned download URL for rows
with `status=Available` — so the read path can never point at an
unscanned (or infected) byte. From Phase 4 (frontend UX parity), admins
and teachers also see Pending and ScanFailed rows in the response (with
a `null` `downloadUrl`) so the UI can render "scanning…" / "blocked"
badges; parents and other roles still see only Available rows.

### V2 upload response shape

`POST /api/attachments/request-upload-url-v2` returns:

```json
{ "uploadUrl": "https://…", "attachmentId": "<guid>" }
```

There is no `storageKey` field — the client uses the presigned URL
directly to PUT the file, then references the attachment by
`attachmentId` when calling `/attach`. The storage key is an
implementation detail of the API.

## Deploying ClamAV

1. Add a `clamav/clamav` service to Railway (or your orchestrator of
   choice). Mount a named volume at `/var/lib/clamav` so the virus
   database survives restarts; cron `freshclam` every 6h.
2. Expose port `3310` on the private network only — it's a trust
   boundary inside the app, not a public endpoint.
3. Set the following env vars on the API service:
   ```
   CLAMAV_ENABLED=true
   CLAMAV_HOST=clamav.railway.internal
   CLAMAV_PORT=3310
   CLAMAV_TIMEOUT_SECONDS=30
   ```
4. Restart the API. `AttachmentScanWorker` starts immediately and begins
   draining the queue. Uploads that pre-date the clamd deployment remain
   Pending until the worker picks them up; the backfill in the scan
   migration marked every **existing** attachment `Available`, so only
   new uploads (after deploy) will sit in Pending.

## Smoke test

Drop an EICAR test file via the upload URL, call `attach`, then poll
`attachments.status` in the database:

```bash
echo -n 'X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*' > eicar.com
curl -X PUT --data-binary @eicar.com "$PRESIGNED_URL"
# …call /attach…
psql "$DATABASE_URL" -c "SELECT id, status, threat_name FROM attachments ORDER BY uploaded_at DESC LIMIT 1;"
# Expected: status = 'Infected', threat_name starts with 'Win.Test.EICAR'
```

## Operator actions

- If a scan stalls (worker logs "scan timed out"), check that
  `clamd` is reachable on the configured host:port and that the
  virus database is fresh (`freshclam` output in the clamav container).
- `ScanFailed` rows retain the storage object. Operator can requeue by
  setting `status='Pending'` and invoking the enqueue path, or delete
  via the existing attachment delete endpoint.
- Surfacing infections to tenant admins (email, in-app notification) is
  a follow-up; today the Warning log line is the only signal.
