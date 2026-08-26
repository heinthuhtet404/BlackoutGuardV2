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

    useEffect(() => {
        if (latestDecision) {
            setLiveRows((current) =>
                [mapDecisionToEntry(latestDecision, loadsMap), ...current].slice(0, MAX_LIVE_ROWS)
            );
        }
    }, [latestDecision, loadsMap]);

    useEffect(() => {
        if (!prevConnectedRef.current && connected) {
            setLiveRows([]);
            setPage(1);
            void queryClient.invalidateQueries({ queryKey: ["audit"] });
        }
        prevConnectedRef.current = connected;
    }, [connected, queryClient]);

    const items = data?.items ?? [];
    const totalCount = data?.totalCount ?? items.length;
    const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

    const displayRows = useMemo(() => {
        if (page > 1) return items;

        const filteredLiveRows = liveRows.filter(
            (live) => !items.some((item) => item.rationale === live.rationale && item.timestampUtc === live.timestampUtc)
        );

        return [...filteredLiveRows, ...items];
    }, [page, liveRows, items]);

    if (isLoading) {
        return (
            <div className={styles.loadingState} role="status">
                <span className={styles.spinner}></span>
                Loading audit log...
            </div>
        );
    }

    if (isError) {
        return (
            <div className={styles.errorState} role="alert">
                <span className={styles.errorIcon}>⚠</span>
                Failed to load audit log: {error instanceof Error ? error.message : "unknown error"}
            </div>
        );
    }

    return (
        <div className={styles.page} data-testid="audit-table">
            <div className={styles.header}>
                <div>
                    <h2 className={styles.title}>📋 Audit Log</h2>
                    <p className={styles.subtitle}>Track all load shedding events and decisions</p>
                </div>
                <ExportButtons />
            </div>

            <div className={styles.tableWrapper}>
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
                                    <span className={styles.emptyIcon}>📭</span>
                                    No audit entries yet.
                                </td>
                            </tr>
                        )}
                        {displayRows.map((entry) => {
                            const isLive = typeof entry.id === "string" && entry.id.startsWith("live-");
                            return (
                                <tr
                                    key={entry.id}
                                    className={`${styles.row} ${isLive ? styles.liveRow : ""}`}
                                    data-testid={`audit-row-${entry.id}`}
                                >
                                    <td className={styles.timestamp}>
                                        <span className={isLive ? styles.liveDot : ""}>
                                            {isLive && <span className={styles.dot}></span>}
                                            {formatTimestamp(entry.timestampUtc)}
                                        </span>
                                    </td>
                                    <td>
                                        <span className={`${styles.eventBadge} ${styles[`event${entry.eventType.replace(/\s/g, '')}`] || ''}`}>
                                            {entry.eventType}
                                        </span>
                                    </td>
                                    <td className={styles.rationale}>{entry.rationale}</td>
                                    <td className={styles.affectedLoad}>{entry.affectedLoadId ?? "—"}</td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>

            <div className={styles.pagination}>
                <button
                    type="button"
                    className={styles.pageBtn}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page <= 1}
                    data-testid="prev-page"
                >
                    ← Previous
                </button>
                <span className={styles.pageInfo} data-testid="page-indicator">
                    Page <strong>{page}</strong> of <strong>{totalPages}</strong>
                </span>
                <button
                    type="button"
                    className={styles.pageBtn}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages}
                    data-testid="next-page"
                >
                    Next →
                </button>
            </div>
        </div>
    );
}