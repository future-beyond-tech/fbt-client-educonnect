export interface StudentListItem {
  id: string;
  name: string;
  rollNumber: string;
  classId: string;
  className: string;
  section: string;
  isActive: boolean;
  dateOfBirth: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface StudentDetail {
  id: string;
  name: string;
  rollNumber: string;
  classId: string;
  className: string;
  section: string;
  academicYear: string;
  dateOfBirth: string | null;
  isActive: boolean;
  createdAt: string;
  parentLinks: ParentLink[];
}

export interface ParentLink {
  linkId: string;
  parentId: string;
  parentName: string;
  parentPhone: string;
  relationship: string;
  linkedAt: string;
}

export interface ParentChildItem {
  id: string;
  name: string;
  rollNumber: string;
  classId: string;
  className: string;
  section: string;
  relationship: string;
  isActive: boolean;
}

export interface ClassItem {
  id: string;
  name: string;
  section: string;
  academicYear: string;
  studentCount: number;
}

export interface ParentSearchResult {
  id: string;
  name: string;
  phone: string;
}

export interface EnrollStudentRequest {
  name: string;
  rollNumber: string;
  classId: string;
  dateOfBirth: string | null;
}

export interface UpdateStudentRequest {
  name: string;
  classId: string;
  dateOfBirth: string | null;
}

export interface LinkParentRequest {
  parentId: string;
  relationship: string;
}

export interface MutationResponse {
  studentId?: string;
  linkId?: string;
  message: string;
}
