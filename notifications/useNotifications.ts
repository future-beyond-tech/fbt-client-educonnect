/**
 * useNotifications.ts
 *
 * React hook that wires notification listeners into a component's lifecycle
 * and exposes helper functions. Drop into any screen or your root App.
 *
 *   const {
 *     pushToken,
 *     permissionGranted,
 *     schedule,
 *     cancel,
 *   } = useNotifications({
 *     onTap: (response) => {
 *       const screen = response.notification.request.content.data?.screen;
 *       if (screen) navigation.navigate(screen);
 *     },
 *   });
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import type * as Notifications from 'expo-notifications';

import {
  cancelAllScheduledNotifications,
  cancelScheduledNotification,
  configureAndroidChannels,
  onNotificationReceived,
  onNotificationTapped,
  registerForPushNotifications,
  requestNotificationPermission,
  scheduleLocalNotification,
  type NotificationPayload,
  type ScheduleOptions,
} from './notificationService';

type UseNotificationsOptions = {
  /** Register for push notifications on mount. Defaults to true. */
  registerOnMount?: boolean;
  /** Fires for foreground notifications. */
  onReceive?: (notification: Notifications.Notification) => void;
  /** Fires when the user taps a notification (any app state). */
  onTap?: (response: Notifications.NotificationResponse) => void;
};

export function useNotifications(options: UseNotificationsOptions = {}) {
  const { registerOnMount = true, onReceive, onTap } = options;

  const [pushToken, setPushToken] = useState<string | null>(null);
  const [permissionGranted, setPermissionGranted] = useState<boolean | null>(null);

  // Stash handlers in refs so the listener effect doesn't re-subscribe on
  // every render when parents pass inline functions.
  const receiveRef = useRef(onReceive);
  const tapRef = useRef(onTap);
  receiveRef.current = onReceive;
  tapRef.current = onTap;

  useEffect(() => {
    let cancelled = false;

    async function bootstrap() {
      await configureAndroidChannels();

      if (registerOnMount) {
        const token = await registerForPushNotifications();
        if (cancelled) return;
        setPushToken(token);
        setPermissionGranted(token !== null);
      } else {
        const granted = await requestNotificationPermission();
        if (cancelled) return;
        setPermissionGranted(granted);
      }
    }

    bootstrap();

    const unsubReceive = onNotificationReceived((n) => receiveRef.current?.(n));
    const unsubTap = onNotificationTapped((r) => tapRef.current?.(r));

    return () => {
      cancelled = true;
      unsubReceive();
      unsubTap();
    };
  }, [registerOnMount]);

  const schedule = useCallback(
    (
      payload: NotificationPayload,
      scheduleOptions?: ScheduleOptions,
      channelId?: 'messages' | 'alerts' | 'promotions',
    ) => scheduleLocalNotification(payload, scheduleOptions, channelId),
    [],
  );

  const cancel = useCallback(
    (identifier?: string) =>
      identifier ? cancelScheduledNotification(identifier) : cancelAllScheduledNotifications(),
    [],
  );

  return {
    pushToken,
    permissionGranted,
    schedule,
    cancel,
  };
}
