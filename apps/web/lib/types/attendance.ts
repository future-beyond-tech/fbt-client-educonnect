export interface AttendanceRecord {
  recordId: string;
  studentId: string;
  date: string;
  status: string;
  reason: string | null;
  enteredByRole: string;
  createdAt: string;
}

export interface LeaveApplication {
  id: string;
  studentId: string;
  studentName: string;
  rollNumber: string;
  className: string;
  startDate: string;
  endDate: string;
  reason: string;
  status: "Pending" | "Approved" | "Rejected";
  reviewNote: string | null;
  createdAt: string;
}

/**
 * Apply leave for one or more children in a single submission.
 *
 * The backend accepts either a `studentIds` array (preferred) or a legacy
 * single `studentId` for backward compatibility. Always send `studentIds`
 * from new clients — even a single-child request should be a one-element array.
 */
export interface ApplyLeaveRequest {
  studentIds: string[];
  startDate: string; // "YYYY-MM-DD"
  endDate: string;   // "YYYY-MM-DD"
  reason: string;
}

export interface ApplyLeaveResponse {
  /** First created leave application ID (for back-compat). */
  leaveApplicationId: string;
  /** All created leave application IDs, one per selected child. */
  leaveApplicationIds: string[];
  /** Number of leave applications created (equals studentIds.length). */
  createdCount: number;
  status: "Pending" | "Approved" | "Rejected";
  message: string;
}

export interface UpdateLeaveRequest {
  startDate: string; // "YYYY-MM-DD"
  endDate: string;   // "YYYY-MM-DD"
  reason: string;
}

export interface GetLeaveApplicationsResponse {
  items: LeaveApplication[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}
