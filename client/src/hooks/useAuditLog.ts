import { useQuery } from "@tanstack/react-query";
import { get } from "../api/apiClient";

export interface AuditEntry {
  id: number;
  timestampUtc: string;
  eventType: string;
  rationale: string;
  affectedLoadId: string | null;
}

export interface AuditPage {
  items: AuditEntry[];
  totalCount: number;
}

export function useAuditLog(page: number, pageSize: number) {
  return useQuery({
    queryKey: ["audit", page, pageSize],
    queryFn: () => get<AuditPage>(`/audit?page=${page}&pageSize=${pageSize}`),
    placeholderData: (previous) => previous,
  });
}
