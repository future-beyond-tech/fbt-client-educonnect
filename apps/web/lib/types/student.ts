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
  parentEmail: string;
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
  email: string;
}

export interface CreateParentRequest {
  name: string;
  phone: string;
  email: string;
  pin: string;
}

export interface EnrollStudentParentRequest extends CreateParentRequest {
  relationship: string;
}

export interface EnrollStudentExistingParentRequest {
  parentId: string;
  relationship: string;
}

export interface EnrollStudentRequest {
  name: string;
  rollNumber: string;
  classId: string;
  dateOfBirth: string | null;
  parent?: EnrollStudentParentRequest;
  existingParent?: EnrollStudentExistingParentRequest;
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

export interface ParentMutationResponse {
  parentId?: string;
  message: string;
  /**
   * Plain-text temporary PIN echoed back from the API when a parent account is
   * first created. Shown once to the admin so they can relay it to the new
   * parent. Do NOT persist or log this value.
   */
  temporaryPin?: string;
}

export interface CreateClassRequest {
  name: string;
  section: string;
  academicYear: string;
}

export interface UpdateClassRequest {
  name: string;
  section: string;
  academicYear: string;
}

export interface ClassMutationResponse {
  classId?: string;
  message: string;
}

export interface MutationResponse {
  studentId?: string;
  linkId?: string;
  message: string;
  /**
   * Plain-text temporary PIN echoed back when enrolling a student with an
   * inline parent account. Empty when linking an existing parent. Do NOT
   * persist or log this value.
   */
  temporaryPin?: string;
}
