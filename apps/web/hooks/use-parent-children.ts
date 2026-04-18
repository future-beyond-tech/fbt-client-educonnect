"use client";

import * as React from "react";
import { ApiError, apiGet } from "@/lib/api-client";
import { API_ENDPOINTS } from "@/lib/constants";
import { ALL_CHILDREN_VALUE } from "@/lib/parent-children";
import type { ParentChildItem } from "@/lib/types/student";

export interface UseParentChildrenReturn {
  children: ParentChildItem[];
  selectedChildId: string;
  selectedChild: ParentChildItem | null;
  hasMultipleChildren: boolean;
  isLoading: boolean;
  error: string;
  setSelectedChildId: React.Dispatch<React.SetStateAction<string>>;
  fetchChildren: () => Promise<void>;
}

export function useParentChildren(): UseParentChildrenReturn {
  const [children, setChildren] = React.useState<ParentChildItem[]>([]);
  const [selectedChildId, setSelectedChildId] = React.useState(ALL_CHILDREN_VALUE);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState("");

  const fetchChildren = React.useCallback(async (): Promise<void> => {
    setIsLoading(true);
    setError("");

    try {
      const data = await apiGet<ParentChildItem[]>(API_ENDPOINTS.studentsMyChildren);
      setChildren(data);
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "Failed to load linked children."
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    void fetchChildren();
  }, [fetchChildren]);

  React.useEffect(() => {
    if (selectedChildId === ALL_CHILDREN_VALUE) {
      return;
    }

    const hasSelectedChild = children.some((child) => child.id === selectedChildId);
    if (!hasSelectedChild) {
      setSelectedChildId(ALL_CHILDREN_VALUE);
    }
  }, [children, selectedChildId]);

  const selectedChild = React.useMemo(
    () => children.find((child) => child.id === selectedChildId) ?? null,
    [children, selectedChildId]
  );

  return {
    children,
    selectedChildId,
    selectedChild,
    hasMultipleChildren: children.length > 1,
    isLoading,
    error,
    setSelectedChildId,
    fetchChildren,
  };
}
