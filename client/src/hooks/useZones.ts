import { useQuery } from "@tanstack/react-query";
import { get } from "../api/apiClient";
import type { ZoneTree } from "../types/zone";
import { getAccessToken, initializeTokens } from "../auth/tokenStore";

const ZONES_QUERY_KEY = ["zones"] as const;

export function useZones() {
    initializeTokens();
    const token = getAccessToken();

    return useQuery<ZoneTree[]>({
        queryKey: ZONES_QUERY_KEY,
        queryFn: () => get<ZoneTree[]>("/zones"),
        enabled: !!token, // Token ရှိမှသာ Request ပို့မည်
    });
}

export function useInvalidateZones() {
    return ZONES_QUERY_KEY;
}