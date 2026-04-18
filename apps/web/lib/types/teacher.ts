import type { PagedResult } from "./student";

export interface TeacherListItem {
  id: string;
  name: string;
  phone: string;
  role: string;
  isActive: boolean;
  assignedClassCount: number;
  subjects: string[];
}

export type TeacherPagedResult = PagedResult<TeacherListItem>;

export interface TeacherProfile {
  id: string;
  name: string;
  phone: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAt: string;
  assignments: TeacherAssignment[];
}

export interface TeacherAssignment {
  assignmentId: string;
  classId: string;
  className: string;
  section: string;
  subject: string;
  isClassTeacher: boolean;
  assignedAt: string;
}

export interface TeacherClassItem {
  classId: string;
  className: string;
  section: string;
  subject: string;
  isClassTeacher: boolean;
}

export interface SubjectItem {
  id: string;
  name: string;
}

export interface AssignClassRequest {
  classId: string;
  subject: string;
  isClassTeacher: boolean;
}

export interface CreateSubjectRequest {
  name: string;
}

export interface CreateTeacherRequest {
  name: string;
  phone: string;
  email: string;
  password: string;
  role?: string;
  /** When set with `subject`, creates an initial class assignment. */
  classId?: string;
  subject?: string;
  isClassTeacher?: boolean;
}

export interface TeacherMutationResponse {
  teacherId?: string;
  assignmentId?: string;
  message: string;
  /**
   * Plain-text temporary password echoed back from the API when a staff
   * account is first created. Shown once to the admin so they can relay it
   * to the new user. Do NOT persist or log this value.
   */
  temporaryPassword?: string;
}
