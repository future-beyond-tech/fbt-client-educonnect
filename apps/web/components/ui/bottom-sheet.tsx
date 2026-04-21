"use client";

import * as React from "react";
import { Dialog } from "@/components/ui/dialog";

export interface BottomSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
}

/**
 * Filter-panel variant of Dialog. Visually identical on desktop, but the
 * name communicates intent (a dismissable sheet used for composable filter
 * controls rather than a form submission dialog).
 */
export function BottomSheet({
  open,
  onOpenChange,
  title,
  description,
  children,
  footer,
}: BottomSheetProps): React.ReactElement {
  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={title}
      description={description}
      size="md"
      footer={footer}
    >
      {children}
    </Dialog>
  );
}
