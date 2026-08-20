import { useQuery } from "@tanstack/react-query";
import { get } from "../api/apiClient";
import type { ZoneTree } from "../types/zone";
import { getAccessToken } from "../auth/tokenStore";

const ZONES_QUERY_KEY = ["zones"] as const;

export function useZones() {
    const token = getAccessToken();

    return useQuery<ZoneTree[]>({
        queryKey: ZONES_QUERY_KEY,
        queryFn: () => get<ZoneTree[]>("/zones"),
        enabled: !!token,
    });
}

export function useZone(id: string | null) {
    const token = getAccessToken();

    return useQuery<ZoneTree>({
        queryKey: [...ZONES_QUERY_KEY, id] as const,
        queryFn: () => get<ZoneTree>(`/zones/${id}`),
        enabled: !!token && !!id,
    });
}

export function useInvalidateZones() {
    return ZONES_QUERY_KEY;
}