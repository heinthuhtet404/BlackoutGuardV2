import { useQuery } from "@tanstack/react-query";
import { get } from "../api/apiClient";
import type { ZoneTree } from "../types/zone";

const ZONES_QUERY_KEY = ["zones"] as const;

export function useZones() {
  return useQuery<ZoneTree[]>({
    queryKey: ZONES_QUERY_KEY,
    queryFn: () => get<ZoneTree[]>("/api/v1/zones"),
  });
}

export function useInvalidateZones() {
  return ZONES_QUERY_KEY;
}
