"use client";

import * as React from "react";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import type {
  ClassItem,
  PagedResult,
  StudentListItem,
} from "@/lib/types/student";

const DEFAULT_PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;

export interface UseStudentListReturn {
  students: StudentListItem[];
  totalCount: number;
  totalPages: number;
  page: number;
  classes: ClassItem[];
  selectedClassId: string;
  search: string;
  hasActiveFilters: boolean;
  isLoading: boolean;
  error: string;
  setSearch: (value: string) => void;
  setPage: React.Dispatch<React.SetStateAction<number>>;
  handleClassChange: (classId: string) => void;
  fetchStudents: () => Promise<void>;
}

export function useStudentList(
  pageSize = DEFAULT_PAGE_SIZE
): UseStudentListReturn {
  const [students, setStudents] = React.useState<StudentListItem[]>([]);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [classes, setClasses] = React.useState<ClassItem[]>([]);
  const [selectedClassId, setSelectedClassId] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [debouncedSearch, setDebouncedSearch] = React.useState("");
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  React.useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, SEARCH_DEBOUNCE_MS);

    return () => clearTimeout(timer);
  }, [search]);

  React.useEffect(() => {
    const fetchClasses = async (): Promise<void> => {
      try {
        const data = await apiGet<ClassItem[]>(API_ENDPOINTS.classes);
        setClasses(data);
      } catch {
        // Silently fail — class filtering is helpful but not required to view students.
      }
    };

    void fetchClasses();
  }, []);

  const fetchStudents = React.useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setError("");

    try {
      const params = new URLSearchParams();
      if (selectedClassId) params.set("classId", selectedClassId);
      if (debouncedSearch) params.set("search", debouncedSearch);
      params.set("page", page.toString());
      params.set("pageSize", pageSize.toString());

      const data = await apiGet<PagedResult<StudentListItem>>(
        `${API_ENDPOINTS.students}?${params.toString()}`
      );

      setStudents(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load students."
      );
    } finally {
      setIsLoading(false);
    }
  }, [debouncedSearch, page, pageSize, selectedClassId]);

  React.useEffect(() => {
    void fetchStudents();
  }, [fetchStudents]);

  const handleClassChange = React.useCallback((classId: string): void => {
    setSelectedClassId(classId);
    setPage(1);
  }, []);

  return {
    students,
    totalCount,
    totalPages,
    page,
    classes,
    selectedClassId,
    search,
    hasActiveFilters: Boolean(debouncedSearch || selectedClassId),
    isLoading,
    error,
    setSearch,
    setPage,
    handleClassChange,
    fetchStudents,
  };
}
