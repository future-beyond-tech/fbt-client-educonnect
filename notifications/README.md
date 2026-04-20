# React Native notifications (iOS + Android)

Drop-in notification module for React Native. Covers both local notifications (scheduled from inside the app) and push notifications (delivered from a server) on iOS and Android with a single API.

Built on [`expo-notifications`](https://docs.expo.dev/versions/latest/sdk/notifications/), which works in Expo managed projects, Expo Dev Client, and bare React Native projects that have `expo-modules` installed.

## Files

- `notificationService.ts` — core module: permissions, Android channels, local scheduling, push token, listeners.
- `useNotifications.ts` — React hook that wires listeners into the component lifecycle.
- `exampleMessages.ts` — sample payloads for messaging, commerce, fitness, news, calendar.

## Install

```bash
npx expo install expo-notifications expo-device expo-constants
```

If you're in a bare React Native project without Expo, first add Expo modules:

```bash
npx install-expo-modules@latest
```

## Configure

### `app.json` / `app.config.js`

```json
{
  "expo": {
    "plugins": [
      [
        "expo-notifications",
        {
          "icon": "./assets/notification-icon.png",
          "color": "#3B82F6",
          "defaultChannel": "alerts"
        }
      ]
    ],
    "ios": {
      "infoPlist": {
        "UIBackgroundModes": ["remote-notification"]
      }
    },
    "android": {
      "googleServicesFile": "./google-services.json"
    }
  }
}
```

### iOS

1. In Xcode, open your project and go to **Signing & Capabilities → + Capability → Push Notifications**.
2. Generate an APNs key in your Apple Developer account and upload it to Expo (`eas credentials`) or to your own push provider.

### Android

1. Create a Firebase project at [console.firebase.google.com](https://console.firebase.google.com).
2. Add an Android app with your package name.
3. Download `google-services.json` and drop it at the project root (path shown in `app.json` above).

## Usage

### Bootstrapping push + handling taps

```tsx
import { useNotifications } from './notifications/useNotifications';

export default function App() {
  const { pushToken, permissionGranted, schedule } = useNotifications({
    onTap: (response) => {
      const data = response.notification.request.content.data as {
        screen?: string;
        [key: string]: unknown;
      };
      if (data?.screen) {
        // navigation.navigate(data.screen, data);
      }
    },
  });

  // Ship `pushToken` to your backend so it can target this device.
  // ...
}
```

### Scheduling a local notification

```ts
import { messagingExample } from './notifications/exampleMessages';

// Fire 5 seconds from now
await schedule(messagingExample, { seconds: 5 }, 'messages');

// Fire at a specific date
await schedule(
  { title: 'Standup starting', body: '10:00 AM sync in the #team channel.' },
  { date: new Date('2026-04-21T09:45:00') },
  'alerts',
);
```

### Sending a push notification

Once your backend has the Expo push token, POST to the Expo Push API:

```bash
curl -H "Content-Type: application/json" \
     -X POST https://exp.host/--/api/v2/push/send \
     -d '{
       "to": "ExponentPushToken[xxxxxxxxxxxxxxxxx]",
       "title": "New Message",
       "body": "Alex: Are we still on for lunch?",
       "data": { "screen": "ChatThread", "threadId": "thread_9821" },
       "channelId": "messages"
     }'
```

The token can also be sent directly to FCM (Android) or APNs (iOS) if you prefer to manage push yourself — the listeners in this module handle both paths.

## Best practices

- **Ask at the right moment.** Don't prompt for permission on cold launch. Ask when the user enables a feature that depends on notifications (e.g., "Remind me" or "Notify on reply").
- **Use channels.** Group notifications by user-facing category so people can mute one type without silencing everything. This module pre-configures `messages`, `alerts`, and `promotions`.
- **Keep it short.** Titles under 50 characters, body under 150 characters, so the full message fits on the lock screen.
- **Be conservative.** Fewer than a handful of notifications per day unless the user has explicitly opted into high-frequency updates.
- **Respect local time.** Schedule non-urgent notifications for reasonable hours in the user's time zone.
- **Always deep link.** Tapping a notification should jump straight to the relevant screen, not drop the user on your home page. That's what the `data.screen` convention in `exampleMessages.ts` is for.
- **Make opting out painless.** Provide an in-app settings screen that maps to your channels so users can turn off marketing without losing transactional alerts.

## Testing

- **Local notifications** work on the iOS simulator and Android emulator.
- **Push notifications** require a physical device (both platforms). On iOS they additionally require a real provisioning profile with push entitlement.
- Use [Expo's push tool](https://expo.dev/notifications) to send a test push from a web UI once you have a token.
