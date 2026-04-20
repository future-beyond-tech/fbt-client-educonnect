/**
 * exampleMessages.ts
 *
 * Sample notification payloads covering common app types. Pass any of these
 * directly to `schedule(...)` from useNotifications or to
 * `scheduleLocalNotification(...)` from notificationService.
 *
 * Each payload carries a `data` object — that's where you put deep-link
 * targets, entity IDs, and anything else your app needs to route the tap.
 */

import type { NotificationPayload } from './notificationService';

export const messagingExample: NotificationPayload = {
  title: 'New Message',
  body: 'Alex: Are we still on for lunch?',
  data: { screen: 'ChatThread', threadId: 'thread_9821' },
};

export const ecommerceExample: NotificationPayload = {
  title: 'Your order has shipped',
  body: 'Order #4429 is on its way. Tap to track your package.',
  data: { screen: 'OrderDetail', orderId: '4429' },
};

export const fitnessExample: NotificationPayload = {
  title: 'Almost there',
  body: "You're 500 steps away from today's goal. Keep going!",
  data: { screen: 'ActivityDashboard' },
};

export const newsExample: NotificationPayload = {
  title: 'Breaking news',
  body: 'Markets rally on unexpected earnings reports.',
  data: { screen: 'Article', articleId: 'a_20260420_markets' },
};

export const calendarExample: NotificationPayload = {
  title: 'Team standup in 15 minutes',
  body: "Don't forget today's 10:00 AM sync.",
  data: { screen: 'Event', eventId: 'evt_standup_0420' },
};

/**
 * Grouped by Android channel so you can pick the right one for importance.
 * On iOS the channel is ignored — tone is controlled by the system +
 * `sound` field on the payload.
 */
export const examplesByChannel = {
  messages: [messagingExample],
  alerts: [ecommerceExample, calendarExample],
  promotions: [fitnessExample, newsExample],
} as const;
