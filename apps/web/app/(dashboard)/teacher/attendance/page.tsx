"use client";

import * as React from "react";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { CalendarDays, Plus } from "lucide-react";

interface AttendanceRecord {
  recordId: string;
  studentId: string;
  date: string;
  status: string;
  reason: string | null;
  enteredByRole: string;
  createdAt: string;
}

interface MarkAbsenceResponse {
  recordId: string;
  status: string;
  message: string;
}

const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

export default function TeacherAttendancePage(): React.ReactElement {
  const [records, setRecords] = React.useState<AttendanceRecord[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [selectedMonth, setSelectedMonth] = React.useState(new Date().getMonth() + 1);
  const [selectedYear, setSelectedYear] = React.useState(new Date().getFullYear());

  // Mark absence form state
  const [showForm, setShowForm] = React.useState(false);
  const [studentId, setStudentId] = React.useState("");
  const [absenceDate, setAbsenceDate] = React.useState(
    new Date().toISOString().split("T")[0]
  );
  const [reason, setReason] = React.useState("");
  const [formError, setFormError] = React.useState("");
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [successMessage, setSuccessMessage] = React.useState("");

  const fetchAttendance = React.useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const data = await apiGet<AttendanceRecord[]>(
        `${API_ENDPOINTS.attendance}?month=${selectedMonth}&year=${selectedYear}`
      );
      setRecords(data);
    } catch {
      setError("Failed to load attendance records.");
    } finally {
      setIsLoading(false);
    }
  }, [selectedMonth, selectedYear]);

  React.useEffect(() => {
    fetchAttendance();
  }, [fetchAttendance]);

  const handleMarkAbsence = async (e: React.FormEvent): Promise<void> => {
    e.preventDefault();
    setFormError("");
    setSuccessMessage("");

    if (!studentId.trim()) {
      setFormError("Student ID is required.");
      return;
    }

    setIsSubmitting(true);
    try {
      const response = await apiPost<MarkAbsenceResponse>(API_ENDPOINTS.attendance, {
        studentId,
        date: absenceDate,
        reason: reason || null,
      });
      setSuccessMessage(response.message);
      setStudentId("");
      setReason("");
      setShowForm(false);
      fetchAttendance();
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.message);
      } else {
        setFormError("Failed to mark absence.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const formatDate = (dateStr: string): string => {
    const date = new Date(dateStr + "T00:00:00");
    return date.toLocaleDateString("en-IN", {
      weekday: "short",
      day: "numeric",
      month: "short",
    });
  };

  return (
    <div className="space-y-4 p-4 md:p-8">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Attendance</h1>
          <p className="text-muted-foreground">
            Mark and manage student attendance.
          </p>
        </div>
        <Button
          onClick={() => {
            setShowForm(!showForm);
            setFormError("");
            setSuccessMessage("");
          }}
          size="sm"
        >
          <Plus className="mr-1 h-4 w-4" />
          Mark Absence
        </Button>
      </div>

      {successMessage && (
        <div className="rounded-md bg-green-50 p-3 text-sm text-green-800 dark:bg-green-950 dark:text-green-200">
          {successMessage}
        </div>
      )}

      {showForm && (
        <Card>
          <CardContent className="p-4">
            <form onSubmit={handleMarkAbsence} className="space-y-3">
              <h3 className="font-semibold">Mark Student Absent</h3>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <label htmlFor="studentId" className="text-sm font-medium">
                    Student ID
                  </label>
                  <Input
                    id="studentId"
                    placeholder="Enter student ID"
                    value={studentId}
                    onChange={(e) => setStudentId(e.target.value)}
                    disabled={isSubmitting}
                  />
                </div>
                <div className="space-y-1">
                  <label htmlFor="absenceDate" className="text-sm font-medium">
                    Date
                  </label>
                  <Input
                    id="absenceDate"
                    type="date"
                    value={absenceDate}
                    onChange={(e) => setAbsenceDate(e.target.value)}
                    disabled={isSubmitting}
                  />
                </div>
              </div>
              <div className="space-y-1">
                <label htmlFor="reason" className="text-sm font-medium">
                  Reason (optional)
                </label>
                <Input
                  id="reason"
                  placeholder="Reason for absence"
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  disabled={isSubmitting}
                />
              </div>
              {formError && (
                <p className="text-sm text-destructive">{formError}</p>
              )}
              <div className="flex gap-2">
                <Button type="submit" size="sm" disabled={isSubmitting}>
                  {isSubmitting ? <Spinner size="sm" /> : "Submit"}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setShowForm(false)}
                  disabled={isSubmitting}
                >
                  Cancel
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      )}

      <div className="flex flex-wrap gap-3">
        <select
          value={selectedMonth}
          onChange={(e) => setSelectedMonth(Number(e.target.value))}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm"
        >
          {MONTHS.map((name, i) => (
            <option key={name} value={i + 1}>
              {name}
            </option>
          ))}
        </select>
        <select
          value={selectedYear}
          onChange={(e) => setSelectedYear(Number(e.target.value))}
          className="rounded-md border border-input bg-background px-3 py-2 text-sm"
        >
          {[selectedYear - 1, selectedYear, selectedYear + 1].map((y) => (
            <option key={y} value={y}>
              {y}
            </option>
          ))}
        </select>
      </div>

      {isLoading ? (
        <div className="flex min-h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : error ? (
        <ErrorState title="Error" message={error} onRetry={fetchAttendance} />
      ) : records.length === 0 ? (
        <EmptyState
          title="No records"
          description={`No attendance records for ${MONTHS[selectedMonth - 1]} ${selectedYear}.`}
          icon={<CalendarDays className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
        />
      ) : (
        <div className="space-y-3">
          {records.map((record) => (
            <Card key={record.recordId}>
              <CardContent className="flex items-center justify-between p-4">
                <div className="space-y-1">
                  <p className="font-medium">{formatDate(record.date)}</p>
                  <p className="text-xs text-muted-foreground">
                    Student: {record.studentId.slice(0, 8)}...
                  </p>
                  {record.reason && (
                    <p className="text-sm text-muted-foreground">{record.reason}</p>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="destructive">{record.status}</Badge>
                  <span className="text-xs text-muted-foreground">
                    by {record.enteredByRole}
                  </span>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
