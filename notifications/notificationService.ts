/**
 * notificationService.ts
 *
 * Cross-platform (iOS + Android) notification service for React Native
 * using expo-notifications. Handles:
 *   - Permission requests
 *   - Android notification channels
 *   - Local notification scheduling
 *   - Push token registration (Expo / FCM / APNs)
 *   - Foreground + background message listeners
 *
 * Install once:
 *   npx expo install expo-notifications expo-device expo-constants
 *
 * Works in Expo Managed, Expo Dev Client, and bare React Native
 * projects that have expo-modules installed.
 */

import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type NotificationPayload = {
  title: string;
  body: string;
  /** Arbitrary JSON delivered alongside the notification (deep-link target, etc.) */
  data?: Record<string, unknown>;
  /** iOS-only: custom sound name bundled with the app. Android uses channel sound. */
  sound?: string;
};

export type ScheduleOptions = {
  /** Fire after N seconds from now. Mutually exclusive with `date`. */
  seconds?: number;
  /** Fire at an absolute Date. Mutually exclusive with `seconds`. */
  date?: Date;
  /** Repeat the notification. Defaults to false. */
  repeats?: boolean;
};

// ---------------------------------------------------------------------------
// Foreground display behavior
// ---------------------------------------------------------------------------

/**
 * Controls how notifications look when the app is in the foreground.
 * By default iOS hides them; we override so users see them regardless.
 */
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: true,
    shouldSetBadge: true,
  }),
});

// ---------------------------------------------------------------------------
// Android channels
// ---------------------------------------------------------------------------

/**
 * Android 8.0+ requires every notification to belong to a channel.
 * Group by user-facing category (messages, promotions, alerts, etc.) so
 * users can mute one type without silencing everything.
 */
export async function configureAndroidChannels(): Promise<void> {
  if (Platform.OS !== 'android') return;

  await Notifications.setNotificationChannelAsync('messages', {
    name: 'Messages',
    description: 'Direct messages and chat notifications',
    importance: Notifications.AndroidImportance.HIGH,
    vibrationPattern: [0, 250, 250, 250],
    lightColor: '#3B82F6',
  });

  await Notifications.setNotificationChannelAsync('alerts', {
    name: 'Alerts',
    description: 'Time-sensitive alerts and reminders',
    importance: Notifications.AndroidImportance.MAX,
    sound: 'default',
  });

  await Notifications.setNotificationChannelAsync('promotions', {
    name: 'Promotions & updates',
    description: 'Non-urgent news, marketing, and product updates',
    importance: Notifications.AndroidImportance.LOW,
  });
}

// ---------------------------------------------------------------------------
// Permissions
// ---------------------------------------------------------------------------

/**
 * Request notification permission on both platforms. Returns true if granted.
 *
 * Call this at a moment when the value is obvious to the user — for example
 * right after they enable a feature that depends on notifications, not on
 * cold app launch.
 */
export async function requestNotificationPermission(): Promise<boolean> {
  if (!Device.isDevice) {
    console.warn('[notifications] Push notifications require a physical device.');
    return false;
  }

  const { status: existing } = await Notifications.getPermissionsAsync();
  let finalStatus = existing;

  if (existing !== 'granted') {
    const { status } = await Notifications.requestPermissionsAsync({
      ios: {
        allowAlert: true,
        allowBadge: true,
        allowSound: true,
      },
    });
    finalStatus = status;
  }

  return finalStatus === 'granted';
}

// ---------------------------------------------------------------------------
// Push token registration
// ---------------------------------------------------------------------------

/**
 * Returns an Expo push token you can store on your server and use with the
 * Expo Push API, or a native FCM/APNs token if you're managing push yourself.
 *
 * Send the returned token to your backend so it can target this device.
 */
export async function registerForPushNotifications(): Promise<string | null> {
  const granted = await requestNotificationPermission();
  if (!granted) return null;

  await configureAndroidChannels();

  try {
    const projectId =
      Constants.expoConfig?.extra?.eas?.projectId ??
      (Constants as unknown as { easConfig?: { projectId?: string } }).easConfig?.projectId;

    const tokenResponse = await Notifications.getExpoPushTokenAsync(
      projectId ? { projectId } : undefined,
    );
    return tokenResponse.data;
  } catch (error) {
    console.warn('[notifications] Failed to get push token:', error);
    return null;
  }
}

// ---------------------------------------------------------------------------
// Local notifications
// ---------------------------------------------------------------------------

/**
 * Schedule a local notification. Pick one of `seconds` or `date`.
 * Returns the identifier so you can cancel it later.
 */
export async function scheduleLocalNotification(
  payload: NotificationPayload,
  options: ScheduleOptions = { seconds: 1 },
  channelId: 'messages' | 'alerts' | 'promotions' = 'alerts',
): Promise<string> {
  const trigger = buildTrigger(options);

  return Notifications.scheduleNotificationAsync({
    content: {
      title: payload.title,
      body: payload.body,
      data: payload.data ?? {},
      sound: payload.sound ?? 'default',
      ...(Platform.OS === 'android' ? { channelId } : {}),
    },
    trigger,
  });
}

function buildTrigger(options: ScheduleOptions): Notifications.NotificationTriggerInput {
  if (options.date) {
    return {
      type: Notifications.SchedulableTriggerInputTypes.DATE,
      date: options.date,
    };
  }
  return {
    type: Notifications.SchedulableTriggerInputTypes.TIME_INTERVAL,
    seconds: Math.max(1, options.seconds ?? 1),
    repeats: options.repeats ?? false,
  };
}

/** Cancel a single scheduled notification by identifier. */
export async function cancelScheduledNotification(identifier: string): Promise<void> {
  await Notifications.cancelScheduledNotificationAsync(identifier);
}

/** Cancel every scheduled local notification for this app. */
export async function cancelAllScheduledNotifications(): Promise<void> {
  await Notifications.cancelAllScheduledNotificationsAsync();
}

// ---------------------------------------------------------------------------
// Listeners (foreground display + user tap)
// ---------------------------------------------------------------------------

type ReceivedHandler = (notification: Notifications.Notification) => void;
type TappedHandler = (response: Notifications.NotificationResponse) => void;

/**
 * Fires when a notification arrives while the app is in the foreground.
 * Returns an unsubscribe function.
 */
export function onNotificationReceived(handler: ReceivedHandler): () => void {
  const subscription = Notifications.addNotificationReceivedListener(handler);
  return () => subscription.remove();
}

/**
 * Fires when the user taps a notification (from foreground, background, or
 * a cold start). Use this for deep-linking into the app.
 * Returns an unsubscribe function.
 */
export function onNotificationTapped(handler: TappedHandler): () => void {
  const subscription = Notifications.addNotificationResponseReceivedListener(handler);
  return () => subscription.remove();
}

// ---------------------------------------------------------------------------
// Badges (iOS + Android where supported)
// ---------------------------------------------------------------------------

export async function setBadgeCount(count: number): Promise<void> {
  await Notifications.setBadgeCountAsync(Math.max(0, count));
}

export async function clearBadge(): Promise<void> {
  await Notifications.setBadgeCountAsync(0);
}
