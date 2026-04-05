import type { PagedResult } from "./student";

export interface TeacherListItem {
  id: string;
  name: string;
  phone: string;
  isActive: boolean;
  assignedClassCount: number;
  subjects: string[];
}

export type TeacherPagedResult = PagedResult<TeacherListItem>;

export interface TeacherProfile {
  id: string;
  name: string;
  phone: string;
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
  assignedAt: string;
}

export interface TeacherClassItem {
  classId: string;
  className: string;
  section: string;
  subject: string;
}

export interface SubjectItem {
  id: string;
  name: string;
}

export interface AssignClassRequest {
  classId: string;
  subject: string;
}

export interface CreateSubjectRequest {
  name: string;
}
