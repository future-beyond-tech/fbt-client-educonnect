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

export interface ApplyLeaveRequest {
  studentId: string;
  startDate: string; // "YYYY-MM-DD"
  endDate: string;   // "YYYY-MM-DD"
  reason: string;
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
