"use client";

import * as React from "react";
import { apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { EmptyState } from "@/components/shared/empty-state";
import { ErrorState } from "@/components/shared/error-state";
import { CalendarDays } from "lucide-react";

interface AttendanceRecord {
  recordId: string;
  studentId: string;
  date: string;
  status: string;
  reason: string | null;
  enteredByRole: string;
  createdAt: string;
}

const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

export default function ParentAttendancePage(): React.ReactElement {
  const [records, setRecords] = React.useState<AttendanceRecord[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");
  const [selectedMonth, setSelectedMonth] = React.useState(new Date().getMonth() + 1);
  const [selectedYear, setSelectedYear] = React.useState(new Date().getFullYear());

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
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Attendance</h1>
        <p className="text-muted-foreground">
          View your child&apos;s attendance records.
        </p>
      </div>

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
          title="No absences recorded"
          description={`No absence records for ${MONTHS[selectedMonth - 1]} ${selectedYear}.`}
          icon={<CalendarDays className="h-8 w-8 text-muted-foreground" aria-hidden="true" />}
        />
      ) : (
        <div className="space-y-3">
          {records.map((record) => (
            <Card key={record.recordId}>
              <CardContent className="flex items-center justify-between p-4">
                <div className="space-y-1">
                  <p className="font-medium">{formatDate(record.date)}</p>
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
