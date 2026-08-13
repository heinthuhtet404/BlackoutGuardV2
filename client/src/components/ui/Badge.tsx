import type { ReactNode } from "react";
import styles from "./Badge.module.css";

type Priority = "P1" | "P2" | "P3";

interface BadgeProps {
  priority: Priority | string;
  children?: ReactNode;
}

function priorityClass(priority: string): string {
  switch (priority) {
    case "P1":
      return styles.p1;
    case "P2":
      return styles.p2;
    case "P3":
      return styles.p3;
    default:
      return styles.unknown;
  }
}

export function Badge({ priority, children }: BadgeProps) {
  return (
    <span className={`${styles.badge} ${priorityClass(priority)}`} data-testid="priority-badge">
      {children ?? priority}
    </span>
  );
}
