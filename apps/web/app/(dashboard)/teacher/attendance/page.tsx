"use client";

import * as React from "react";
import { ApiError, apiGet, apiPost } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { PageHeader, PageSection, PageShell } from "@/components/shared/page-shell";
import { StatusBanner } from "@/components/shared/status-banner";
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
  const [rollNumber, setRollNumber] = React.useState("");
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

    if (!rollNumber.trim()) {
      setFormError("Roll number is required.");
      return;
    }

    setIsSubmitting(true);
    try {
      const response = await apiPost<MarkAbsenceResponse>(API_ENDPOINTS.attendance, {
        rollNumber,
        date: absenceDate,
        reason: reason || null,
      });
      setSuccessMessage(response.message);
      setRollNumber("");
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
    <PageShell>
      <PageHeader
        eyebrow="Teacher tools"
        title="Attendance"
        description="Track recorded absences and quickly log a new absence for a student."
        icon={<CalendarDays className="h-6 w-6" aria-hidden="true" />}
        actions={(
          <Button
            onClick={() => {
              setShowForm(!showForm);
              setFormError("");
              setSuccessMessage("");
            }}
            size="sm"
          >
            <Plus className="h-4 w-4" />
            Mark Absence
          </Button>
        )}
        stats={[
          { label: "Records", value: records.length.toString() },
          { label: "Period", value: `${MONTHS[selectedMonth - 1]} ${selectedYear}` },
        ]}
      />

      {successMessage && (
        <StatusBanner variant="success">{successMessage}</StatusBanner>
      )}

      {showForm && (
        <PageSection>
          <form onSubmit={handleMarkAbsence} className="space-y-4">
            <h3 className="text-lg font-semibold">Mark Student Absent</h3>
            <div className="grid gap-3 md:grid-cols-2">
              <Input
                id="rollNumber"
                label="Roll Number"
                placeholder="e.g. 5A-001"
                value={rollNumber}
                onChange={(e) => setRollNumber(e.target.value)}
                disabled={isSubmitting}
              />
              <Input
                id="absenceDate"
                label="Date"
                type="date"
                value={absenceDate}
                onChange={(e) => setAbsenceDate(e.target.value)}
                disabled={isSubmitting}
              />
            </div>
            <Input
              id="reason"
              label="Reason (optional)"
              placeholder="Reason for absence"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={isSubmitting}
            />
            {formError && (
              <StatusBanner variant="error">{formError}</StatusBanner>
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
        </PageSection>
      )}

      <PageSection className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:max-w-xl">
          <Select
            value={selectedMonth.toString()}
            onChange={(e) => setSelectedMonth(Number(e.target.value))}
            label="Month"
          >
            {MONTHS.map((name, i) => (
              <option key={name} value={i + 1}>
                {name}
              </option>
            ))}
          </Select>
          <Select
            value={selectedYear.toString()}
            onChange={(e) => setSelectedYear(Number(e.target.value))}
            label="Year"
          >
            {[selectedYear - 1, selectedYear, selectedYear + 1].map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </Select>
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
                <CardContent className="flex items-center justify-between gap-4 p-4">
                  <div className="space-y-1">
                    <p className="font-medium">{formatDate(record.date)}</p>
                    <p className="text-xs text-muted-foreground">
                      Student: {record.studentId.slice(0, 8)}...
                    </p>
                    {record.reason && (
                      <p className="text-sm text-muted-foreground">{record.reason}</p>
                    )}
                  </div>
                  <div className="flex flex-wrap items-center justify-end gap-2">
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
      </PageSection>
    </PageShell>
  );
}
