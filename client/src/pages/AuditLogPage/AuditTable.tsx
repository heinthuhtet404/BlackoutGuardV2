import { useEffect, useRef, useState, useMemo } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useAuditLog, type AuditEntry } from "../../hooks/useAuditLog";
import { useTelemetry, type DecisionExecutedPayload } from "../../context/TelemetryContext";
import { get } from "../../api/apiClient";
import { ExportButtons } from "./ExportButtons";
import styles from "./AuditTable.module.css";

const PAGE_SIZE = 20;
const MAX_LIVE_ROWS = 50;

interface LoadDto {
    id: string;
    name: string;
    relayAddress?: number;
}

interface ZoneDto {
    id: string;
    loads?: LoadDto[];
    subZones?: ZoneDto[];
    children?: ZoneDto[];
}

// Helper: Recursively extract all loads from zone hierarchy
function getAllLoadsFromZones(zones: ZoneDto[]): LoadDto[] {
    let loads: LoadDto[] = [];
    zones.forEach((zone) => {
        if (zone.loads) loads = [...loads, ...zone.loads];
        const childZones = zone.children || zone.subZones || [];
        if (childZones.length > 0) {
            loads = [...loads, ...getAllLoadsFromZones(childZones)];
        }
    });
    return loads;
}

function mapDecisionToEntry(payload: DecisionExecutedPayload, loadMap: Map<number, string>): AuditEntry {
    const hasShed = payload.relayDecisions.some((d) => !d.energize);
    const hasRestore = payload.relayDecisions.some((d) => d.energize);

    const eventType =
        hasShed && hasRestore
            ? "Relay Decision Executed"
            : hasRestore
                ? "Load Restored"
                : "Load Shedding Executed";

    const affectedLoad =
        payload.relayDecisions.length > 0
            ? payload.relayDecisions
                .map((d) => loadMap.get(d.relayAddress) || `Relay #${d.relayAddress}`)
                .join(", ")
            : null;

    return {
        id: `live-${Date.now()}-${Math.random()}`,
        timestampUtc: new Date().toISOString(),
        eventType,
        rationale: payload.rationale,
        affectedLoadId: affectedLoad,
    };
}

function formatTimestamp(iso: string): string {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return iso;
    return date.toLocaleString();
}

export function AuditTable() {
    const [page, setPage] = useState(1);
    const [liveRows, setLiveRows] = useState<AuditEntry[]>([]);
    const [loadsMap, setLoadsMap] = useState<Map<number, string>>(new Map());

    const queryClient = useQueryClient();
    const { latestDecision, connected } = useTelemetry();
    const prevConnectedRef = useRef(connected);

    const { data, isLoading, isError, error } = useAuditLog(page, PAGE_SIZE);

    // 0. Fetch Loads to map Relay Address to Load Names
    useEffect(() => {
        const fetchLoads = async () => {
            try {
                const zones = await get<ZoneDto[]>("/zones");
                if (zones) {
                    const allLoads = getAllLoadsFromZones(zones);
                    const map = new Map<number, string>();
                    allLoads.forEach((l) => {
                        if (l.relayAddress !== undefined) {
                            map.set(l.relayAddress, l.name);
                        }
                    });
                    setLoadsMap(map);
                }
            } catch (err) {
                console.error("Failed to load zones for AuditTable mapping:", err);
            }
        };

        fetchLoads();
    }, []);

    // 1. Listen for Live Decision Events from Telemetry Context
    useEffect(() => {
        if (latestDecision) {
            setLiveRows((current) =>
                [mapDecisionToEntry(latestDecision, loadsMap), ...current].slice(0, MAX_LIVE_ROWS)
            );
        }
    }, [latestDecision, loadsMap]);

    // 2. Handle Reconnection / Gap-fill Logic
    useEffect(() => {
        if (!prevConnectedRef.current && connected) {
            setLiveRows([]);
            setPage(1);
            void queryClient.invalidateQueries({ queryKey: ["audit"] });
        }
        prevConnectedRef.current = connected;
    }, [connected, queryClient]);

    // 3. Compute Display Rows & Avoid Duplicates across Page 1 and Live Rows
    const items = data?.items ?? [];
    const totalCount = data?.totalCount ?? items.length;
    const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

    const displayRows = useMemo(() => {
        // Show live rows only on page 1 to prevent pagination shift glitches
        if (page > 1) return items;

        // Filter out any liveRows that have already been persisted to backend API items
        const filteredLiveRows = liveRows.filter(
            (live) => !items.some((item) => item.rationale === live.rationale && item.timestampUtc === live.timestampUtc)
        );

        return [...filteredLiveRows, ...items];
    }, [page, liveRows, items]);

    if (isLoading) {
        return <div role="status" style={{ padding: "1rem", color: "#94a3b8" }}>Loading audit log...</div>;
    }

    if (isError) {
        return (
            <div role="alert" style={{ padding: "1rem", color: "#ef4444" }}>
                Failed to load audit log: {error instanceof Error ? error.message : "unknown error"}
            </div>
        );
    }

    return (
        <div className={styles.page} data-testid="audit-table">
            <ExportButtons />

            <table className={styles.table}>
                <thead>
                    <tr>
                        <th>Timestamp</th>
                        <th>Event</th>
                        <th>Rationale</th>
                        <th>Affected Load</th>
                    </tr>
                </thead>
                <tbody>
                    {displayRows.length === 0 && (
                        <tr>
                            <td colSpan={4} className={styles.empty}>
                                No audit entries yet.
                            </td>
                        </tr>
                    )}
                    {displayRows.map((entry) => {
                        const isLive = typeof entry.id === "string" && entry.id.startsWith("live-");
                        return (
                            <tr
                                key={entry.id}
                                className={isLive ? styles.liveRow : undefined}
                                data-testid={`audit-row-${entry.id}`}
                            >
                                <td className={styles.timestamp}>{formatTimestamp(entry.timestampUtc)}</td>
                                <td>{entry.eventType}</td>
                                <td className={styles.rationale}>{entry.rationale}</td>
                                <td>{entry.affectedLoadId ?? "—"}</td>
                            </tr>
                        );
                    })}
                </tbody>
            </table>

            <div className={styles.pagination}>
                <button
                    type="button"
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page <= 1}
                    data-testid="prev-page"
                >
                    Previous
                </button>
                <span data-testid="page-indicator">
                    Page {page} of {totalPages}
                </span>
                <button
                    type="button"
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages}
                    data-testid="next-page"
                >
                    Next
                </button>
            </div>
        </div>
    );
}