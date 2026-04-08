export const APP_NAME = "EduConnect";

export const Roles = {
  Parent: "Parent",
  Teacher: "Teacher",
  Admin: "Admin",
} as const;

export type RoleType = (typeof Roles)[keyof typeof Roles];

export interface NavItem {
  label: string;
  href: string;
  icon: string;
}

export const API_ENDPOINTS = {
  login: "/api/auth/login",
  loginParent: "/api/auth/login-parent",
  setPin: "/api/auth/set-pin",
  refresh: "/api/auth/refresh",
  logout: "/api/auth/logout",
  forgotPassword: "/api/auth/forgot-password",
  resetPassword: "/api/auth/reset-password",
  forgotPin: "/api/auth/forgot-pin",
  resetPin: "/api/auth/reset-pin",
  attendance: "/api/attendance",
  homework: "/api/homework",
  notices: "/api/notices",
  students: "/api/students",
  studentsMyChildren: "/api/students/my-children",
  studentsSearchParents: "/api/students/search-parents",
  classes: "/api/classes",
  teachers: "/api/teachers",
  teachersMyClasses: "/api/teachers/my-classes",
  subjects: "/api/subjects",
  notifications: "/api/notifications",
  notificationsUnreadCount: "/api/notifications/unread-count",
  notificationsReadAll: "/api/notifications/read-all",
  attachmentsRequestUpload: "/api/attachments/request-upload-url",
  attachmentsAttach: "/api/attachments/attach",
  attachments: "/api/attachments",
} as const;

export const navigationByRole: Record<RoleType, NavItem[]> = {
  Parent: [
    { label: "Attendance", href: "/parent/attendance", icon: "CheckCircle" },
    { label: "Homework", href: "/parent/homework", icon: "BookMarked" },
    { label: "Notices", href: "/parent/notices", icon: "Bell" },
  ],
  Teacher: [
    { label: "Homework", href: "/teacher/homework", icon: "BookMarked" },
    { label: "Attendance", href: "/teacher/attendance", icon: "CheckCircle" },
    { label: "Students", href: "/teacher/students", icon: "Users" },
    { label: "Profile", href: "/teacher/profile", icon: "BookOpen" },
  ],
  Admin: [
    { label: "Notices", href: "/admin/notices", icon: "Bell" },
    { label: "Students", href: "/admin/students", icon: "Users" },
    { label: "Teachers", href: "/admin/teachers", icon: "BookOpen" },
  ],
};

export const defaultRouteByRole: Record<RoleType, string> = {
  Parent: "/parent/attendance",
  Teacher: "/teacher/homework",
  Admin: "/admin/notices",
};
