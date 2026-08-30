import { useEffect, useRef, useState, useMemo, useCallback } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useAuditLog, type AuditEntry } from "../../hooks/useAuditLog";
import { useTelemetry, type DecisionExecutedPayload } from "../../context/TelemetryContext";
import { get } from "../../api/apiClient";
import { ExportButtons } from "./ExportButtons";
import {
    FileText,
    Search,
    AlertTriangle,
    AlertCircle,
    CheckCircle,
    Activity,
    Clock,
    Server,
    Database,
    ChevronLeft,
    ChevronRight,
    Loader2,
    Wifi,
    WifiOff,
    Zap,
    ZapOff,
    RefreshCw,
    ListChecks,
    ShieldAlert,
    ShieldCheck,
    Shield,
    Timer,
    Tag,
    Info,
} from "lucide-react";
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
    const loads: LoadDto[] = [];

    function traverse(list: ZoneDto[]) {
        for (const zone of list) {
            if (zone.loads) {
                loads.push(...zone.loads);
            }
            const childZones = zone.children || zone.subZones || [];
            if (childZones.length > 0) {
                traverse(childZones);
            }
        }
    }

    traverse(zones);
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
    const [searchQuery, setSearchQuery] = useState("");
    const [liveRows, setLiveRows] = useState<AuditEntry[]>([]);
    const [loadsMap, setLoadsMap] = useState<Map<number, string>>(new Map());

    const queryClient = useQueryClient();
    const { latestDecision, connected } = useTelemetry();
    const prevConnectedRef = useRef(connected);

    const { data, isLoading, isError, error } = useAuditLog(page, PAGE_SIZE);

    const getBadgeStyleClass = useCallback((eventType: string) => {
        switch (eventType) {
            case "Load Shedding Executed":
                return styles.eventLoadSheddingExecuted;
            case "Load Restored":
                return styles.eventLoadRestored;
            case "Relay Decision Executed":
                return styles.eventRelayDecisionExecuted;
            default:
                return styles.eventDefault;
        }
    }, []);

    const getEventIcon = useCallback((eventType: string) => {
        switch (eventType) {
            case "Load Shedding Executed":
                return <ZapOff size={12} />;
            case "Load Restored":
                return <Zap size={12} />;
            case "Relay Decision Executed":
                return <Shield size={12} />;
            default:
                return <Info size={12} />;
        }
    }, []);

    useEffect(() => {
        let isMounted = true;
        const fetchLoads = async () => {
            try {
                const zones = await get<ZoneDto[]>("/zones");
                if (zones && isMounted) {
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
        return () => {
            isMounted = false;
        };
    }, []);

    useEffect(() => {
        if (latestDecision) {
            const entry = mapDecisionToEntry(latestDecision, loadsMap);
            setLiveRows((current) => [entry, ...current].slice(0, MAX_LIVE_ROWS));
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
        let combined = items;

        if (page === 1) {
            const filteredLiveRows = liveRows.filter(
                (live) => !items.some((item) => item.rationale === live.rationale && item.timestampUtc === live.timestampUtc)
            );
            combined = [...filteredLiveRows, ...items];
        }

        if (!searchQuery.trim()) return combined;

        const q = searchQuery.toLowerCase();
        return combined.filter(
            (row) =>
                row.eventType.toLowerCase().includes(q) ||
                row.rationale.toLowerCase().includes(q) ||
                (row.affectedLoadId && row.affectedLoadId.toLowerCase().includes(q))
        );
    }, [page, liveRows, items, searchQuery]);

    if (isLoading) {
        return (
            <div className={styles.loadingState} role="status">
                <Loader2 size={22} className={styles.spinner} />
                Loading audit log...
            </div>
        );
    }

    if (isError) {
        return (
            <div className={styles.errorState} role="alert">
                <AlertCircle size={20} className={styles.errorIcon} />
                Failed to load audit log: {error instanceof Error ? error.message : "unknown error"}
            </div>
        );
    }

    return (
        <div className={styles.page} data-testid="audit-table">
            <div className={styles.header}>
                <div className={styles.headerLeft}>
                    <div className={styles.headerIconWrapper}>
                        <FileText size={24} className={styles.headerIcon} />
                    </div>
                    <div>
                        <h2 className={styles.title}>Audit Log</h2>
                        <p className={styles.subtitle}>Track all load shedding events and decisions</p>
                    </div>
                </div>
                <div className={styles.headerRight}>
                    <div className={styles.connectionStatus}>
                        {connected ? (
                            <span className={styles.statusConnected}>
                                <Wifi size={14} />
                                Live
                            </span>
                        ) : (
                            <span className={styles.statusDisconnected}>
                                <WifiOff size={14} />
                                Offline
                            </span>
                        )}
                    </div>
                    <div className={styles.actions}>
                        <div className={styles.searchWrapper}>
                            <Search size={16} className={styles.searchIcon} />
                            <input
                                type="text"
                                placeholder="Search logs..."
                                value={searchQuery}
                                onChange={(e) => setSearchQuery(e.target.value)}
                                className={styles.searchInput}
                            />
                        </div>
                        <ExportButtons data={displayRows} />
                    </div>
                </div>
            </div>

            <div className={styles.tableWrapper}>
                <table className={styles.table}>
                    <thead>
                        <tr>
                            <th>
                                <Clock size={14} className={styles.thIcon} />
                                Timestamp
                            </th>
                            <th>
                                <Tag size={14} className={styles.thIcon} />
                                Event
                            </th>
                            <th>
                                <Info size={14} className={styles.thIcon} />
                                Rationale
                            </th>
                            <th>
                                <Server size={14} className={styles.thIcon} />
                                Affected Load
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {displayRows.length === 0 && (
                            <tr>
                                <td colSpan={4} className={styles.empty}>
                                    <Database size={36} className={styles.emptyIcon} />
                                    No audit entries found.
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
                                            <Timer size={12} className={styles.timerIcon} />
                                            {formatTimestamp(entry.timestampUtc)}
                                        </span>
                                    </td>
                                    <td>
                                        <span className={`${styles.eventBadge} ${getBadgeStyleClass(entry.eventType)}`}>
                                            {getEventIcon(entry.eventType)}
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
                    <ChevronLeft size={16} />
                    Previous
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
                    Next
                    <ChevronRight size={16} />
                </button>
            </div>
        </div>
    );
}