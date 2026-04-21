export interface ExamSubjectInput {
  subject: string;
  examDate: string; // yyyy-MM-dd
  startTime: string; // HH:mm
  endTime: string; // HH:mm
  maxMarks: number;
  room?: string | null;
}

export interface CreateExamRequest {
  classId: string;
  name: string;
  academicYear: string;
  subjects: ExamSubjectInput[];
}

export interface CreateExamResponse {
  examId: string;
  message: string;
}

export interface UpdateExamRequest {
  name: string;
  academicYear: string;
  subjects: ExamSubjectInput[];
}

export interface UpdateExamResponse {
  message: string;
}

export interface ExamListItem {
  examId: string;
  classId: string;
  className: string;
  section: string;
  name: string;
  academicYear: string;
  isSchedulePublished: boolean;
  schedulePublishedAt: string | null;
  isResultsFinalized: boolean;
  resultsFinalizedAt: string | null;
  subjectCount: number;
  firstExamDate: string | null;
  lastExamDate: string | null;
  createdAt: string;
}

export interface ExamSubjectDetail {
  id: string;
  subject: string;
  examDate: string;
  startTime: string;
  endTime: string;
  maxMarks: number;
  room: string | null;
}

export interface ExamDetail {
  examId: string;
  classId: string;
  className: string;
  section: string;
  name: string;
  academicYear: string;
  isSchedulePublished: boolean;
  schedulePublishedAt: string | null;
  isResultsFinalized: boolean;
  resultsFinalizedAt: string | null;
  canEditSchedule: boolean;
  canEditResults: boolean;
  subjects: ExamSubjectDetail[];
}

export interface PublishExamScheduleResponse {
  message: string;
  notifiedParentCount: number;
}

// ----- results -----

export interface ExamResultRowInput {
  studentId: string;
  examSubjectId: string;
  marksObtained: number | null;
  grade: string | null;
  remarks: string | null;
  isAbsent: boolean;
}

export interface UpsertExamResultsRequest {
  rows: ExamResultRowInput[];
}

export interface UpsertExamResultsResponse {
  insertedCount: number;
  updatedCount: number;
  skippedCount: number;
  warnings: string[];
}

export interface ExamResultsSubjectColumn {
  examSubjectId: string;
  subject: string;
  maxMarks: number;
}

export interface ExamResultsCell {
  examSubjectId: string;
  marksObtained: number | null;
  grade: string | null;
  remarks: string | null;
  isAbsent: boolean;
}

export interface ExamResultsStudentRow {
  studentId: string;
  rollNumber: string;
  name: string;
  cells: ExamResultsCell[];
}

export interface ExamResultsGrid {
  examId: string;
  examName: string;
  className: string;
  section: string;
  isResultsFinalized: boolean;
  resultsFinalizedAt: string | null;
  canEditResults: boolean;
  subjects: ExamResultsSubjectColumn[];
  students: ExamResultsStudentRow[];
}

export interface ExamResultStudentLine {
  examSubjectId: string;
  subject: string;
  maxMarks: number;
  marksObtained: number | null;
  grade: string | null;
  remarks: string | null;
  isAbsent: boolean;
}

export interface ExamResultStudent {
  examId: string;
  examName: string;
  className: string;
  section: string;
  studentName: string;
  rollNumber: string;
  isResultsFinalized: boolean;
  resultsFinalizedAt: string | null;
  totalObtained: number;
  totalMax: number;
  percentage: number;
  lines: ExamResultStudentLine[];
}

export interface FinalizeExamResultsResponse {
  message: string;
  studentCount: number;
  notifiedParentCount: number;
}

export interface UploadExamResultsCsvResponse {
  inserted: number;
  updated: number;
  skipped: number;
  warnings: string[];
}
