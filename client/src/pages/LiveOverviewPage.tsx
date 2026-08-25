import { useState, useEffect } from "react";
import { useTelemetry, type RelayDecision } from "../context/TelemetryContext";
import { get } from "../api/apiClient";
import styles from "./LiveOverviewPage.module.css";

interface AlarmLog {
    id: string;
    time: string;
    message: string;
    type: "warning" | "critical" | "success";
}

interface LoadDto {
    id: string;
    zoneId: string;
    name: string;
    powerRatingKw: number;
    priority?: string | number;
    priorityLevel?: number;
    relayAddress?: number;
    isActive?: boolean;
    isSheddable?: boolean;
}

interface ZoneDto {
    id: string;
    facilityId: string;
    name: string;
    parentZoneId?: string | null;
    loads?: LoadDto[];
    subZones?: ZoneDto[];
    children?: ZoneDto[];
}

// Helper: Extract numeric priority (1, 2, or 3) safely from any backend payload format
function getLoadPriorityNum(load: LoadDto): number {
    if (typeof load.priorityLevel === "number") return load.priorityLevel;
    if (typeof load.priority === "number") return load.priority;
    if (typeof load.priority === "string") {
        const parsed = parseInt(load.priority.replace(/\D/g, ""), 10);
        if (!isNaN(parsed)) return parsed;
    }
    return 1; // Default fallback
}

// Helper: Recursively aggregate loads across zone hierarchy
function getAllZoneLoads(zone: ZoneDto): LoadDto[] {
    let loads: LoadDto[] = Array.isArray(zone.loads) ? [...zone.loads] : [];
    const childZones = zone.children || zone.subZones || [];

    if (childZones.length > 0) {
        childZones.forEach((child) => {
            loads = [...loads, ...getAllZoneLoads(child)];
        });
    }

    return loads;
}

export function LiveOverviewPage() {
    const { telemetry, connected, latestDecision } = useTelemetry();

    // 1. Dynamic Database States
    const [zones, setZones] = useState<ZoneDto[]>([]);
    const [loadingDbData, setLoadingDbData] = useState<boolean>(true);

    // 2. Telemetry History & Alarms
    const [freqHistory, setFreqHistory] = useState<number[]>([]);
    const [alarms, setAlarms] = useState<AlarmLog[]>([]);

    // Fetch Zone Hierarchy from API
    useEffect(() => {
        const fetchZonesAndLoads = async () => {
            try {
                const data = await get<ZoneDto[]>("/zones");
                setZones(data || []);
            } catch (err) {
                console.error("Failed to load zones and loads from API:", err);
            } finally {
                setLoadingDbData(false);
            }
        };

        fetchZonesAndLoads();
    }, []);

    // Track Frequency Updates for Chart & Warning Alarms
    useEffect(() => {
        if (!telemetry) return;

        setFreqHistory((prev) => [...prev.slice(-19), telemetry.frequency]);

        if (telemetry.frequency < 49.5) {
            const timeStr = new Date().toLocaleTimeString();
            const alarmMsg = `⚠️ Low Frequency Warning: ${telemetry.frequency.toFixed(2)} Hz`;

            setAlarms((prev) => {
                if (prev[0]?.message === alarmMsg) return prev; // Avoid duplicate consecutive logs
                return [
                    {
                        id: `${Date.now()}-${Math.random()}`,
                        time: timeStr,
                        message: alarmMsg,
                        type: "warning",
                    },
                    ...prev.slice(0, 4),
                ];
            });
        }
    }, [telemetry]);

    // Track Relay Decisions for Alarms
    useEffect(() => {
        if (!latestDecision) return;

        const timeStr = new Date().toLocaleTimeString();
        const newAlarm: AlarmLog = {
            id: `${Date.now()}-${Math.random()}`,
            time: timeStr,
            message: `⚡ Load Shedding Executed: ${latestDecision.rationale}`,
            type: "critical",
        };

        setAlarms((prev) => [newAlarm, ...prev.slice(0, 4)]);
    }, [latestDecision]);

    const isUnderFrequency = telemetry ? telemetry.frequency < 49.5 : false;

    const getSystemMode = () => {
        if (!connected) return { text: "SYSTEM OFFLINE", className: styles.modeOffline };
        if (isUnderFrequency) return { text: "BLACKOUT PREVENTIVE MODE", className: styles.modeDanger };
        if (telemetry?.generatorOn) return { text: "GENERATOR BACKUP MODE", className: styles.modeWarning };
        return { text: "GRID NORMAL MODE", className: styles.modeSuccess };
    };

    const systemMode = getSystemMode();

    // Aggregated Load Calculations
    const allLoads = zones.flatMap((z) => getAllZoneLoads(z));
    const totalConfiguredKw = allLoads.reduce((sum, l) => sum + (l.powerRatingKw || 0), 0);

    const getPriorityLoadSum = (priorityTarget: number) => {
        return allLoads
            .filter((l) => getLoadPriorityNum(l) === priorityTarget)
            .reduce((sum, l) => sum + (l.powerRatingKw || 0), 0);
    };

    const p1Kw = getPriorityLoadSum(1);
    const p2Kw = getPriorityLoadSum(2);
    const p3Kw = getPriorityLoadSum(3);

    const p1Pct = totalConfiguredKw > 0 ? Math.round((p1Kw / totalConfiguredKw) * 100) : 0;
    const p2Pct = totalConfiguredKw > 0 ? Math.round((p2Kw / totalConfiguredKw) * 100) : 0;
    const p3Pct = totalConfiguredKw > 0 ? Math.round((p3Kw / totalConfiguredKw) * 100) : 0;

    // Relay Status Lookup
    const getLoadStatus = (relayAddress?: number) => {
        if (!relayAddress || !latestDecision?.relayDecisions) return "🟢 Normal";
        const decision = latestDecision.relayDecisions.find((r: RelayDecision) => r.relayAddress === relayAddress);
        if (decision) {
            return decision.energize ? "🟢 Normal" : "🟡 Shedded";
        }
        return "🟢 Normal";
    };

    const renderFrequencyChart = () => {
        if (freqHistory.length < 2) return null;
        const width = 300;
        const height = 100;
        const minFreq = 48.0;
        const maxFreq = 52.0;

        const points = freqHistory
            .map((val, idx) => {
                const x = (idx / (freqHistory.length - 1)) * width;
                const normalizedY = Math.max(0, Math.min(1, (val - minFreq) / (maxFreq - minFreq)));
                const y = height - normalizedY * height;
                return `${x},${y}`;
            })
            .join(" ");

        return (
            <svg width="100%" height="100%" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none">
                <line x1="0" y1="25" x2={width} y2="25" stroke="#334155" strokeDasharray="2,2" />
                <line x1="0" y1="50" x2={width} y2="50" stroke="#475569" strokeDasharray="4,4" />
                <line x1="0" y1="75" x2={width} y2="75" stroke="#334155" strokeDasharray="2,2" />
                <polyline fill="none" stroke={isUnderFrequency ? "#ef4444" : "#10b981"} strokeWidth="2.5" points={points} />
            </svg>
        );
    };

    return (
        <div className={styles.page}>
            {/* Header Banner */}
            <div className={styles.headerRow}>
                <h1 className={styles.heading}>📊 Live Overview</h1>
                <div className={`${styles.systemModeBanner} ${systemMode.className}`}>
                    ● {systemMode.text}
                </div>
            </div>

            <div className={styles.statusRow}>
                <span className={connected ? styles.statusConnected : styles.statusDisconnected}>
                    {connected ? "● Live Stream Connected" : "○ Offline / Reconnecting..."}
                </span>
            </div>

            {/* Telemetry Cards Grid */}
            <div className={styles.grid}>
                <div className={`${styles.card} ${isUnderFrequency ? styles.cardDanger : ""}`}>
                    <h3 className={styles.cardLabel}>⚡ Grid Voltage</h3>
                    <p className={`${styles.cardValue} ${styles.valueBlue}`}>
                        {telemetry ? `${telemetry.voltage.toFixed(1)} V` : "—"}
                    </p>
                </div>

                <div className={styles.card}>
                    <h3 className={styles.cardLabel}>⚡ Active Real-Time Load</h3>
                    <p className={`${styles.cardValue} ${styles.valueAmber}`}>
                        {telemetry ? `${telemetry.totalLoadKw.toFixed(1)} kW` : "—"}
                    </p>
                </div>

                <div className={`${styles.card} ${isUnderFrequency ? styles.cardDanger : ""}`}>
                    <h3 className={styles.cardLabel}>📈 System Frequency</h3>
                    <p className={`${styles.cardValue} ${isUnderFrequency ? styles.valueDanger : styles.valueSuccess}`}>
                        {telemetry ? `${telemetry.frequency.toFixed(2)} Hz` : "—"}
                    </p>
                    {isUnderFrequency && <span className={styles.warning}>⚠️ Under-Frequency Threshold</span>}
                </div>

                <div className={styles.card}>
                    <h3 className={styles.cardLabel}>🔋 Generator Status</h3>
                    <p className={`${styles.cardValue} ${telemetry?.generatorOn ? styles.valueSuccess : styles.valueMuted}`}>
                        {telemetry ? (telemetry.generatorOn ? "ON" : "OFF") : "—"}
                    </p>
                </div>
            </div>

            {/* Dynamic System Status Map */}
            <div className={styles.sectionCard}>
                <h2 className={styles.sectionTitle}>🗺️ System Status Map (Dynamic DB Zones & Loads)</h2>
                {loadingDbData ? (
                    <p>Loading database hierarchy...</p>
                ) : zones.length === 0 ? (
                    <p>No zones found in database. Create a Zone to view telemetry mapping.</p>
                ) : (
                    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
                        {zones.map((zone) => {
                            const zoneLoads = getAllZoneLoads(zone);

                            return (
                                <div key={zone.id} style={{ border: "1px solid #334155", borderRadius: "8px", padding: "1rem" }}>
                                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" }}>
                                        <h3 style={{ margin: 0, color: "#38bdf8" }}>🏢 Zone: {zone.name}</h3>
                                        <span style={{ fontSize: "0.85rem", color: "#94a3b8" }}>
                                            Total Loads: {zoneLoads.length}
                                        </span>
                                    </div>

                                    <div className={styles.nodeGrid}>
                                        {zoneLoads.length > 0 ? (
                                            zoneLoads.map((load) => {
                                                const status = getLoadStatus(load.relayAddress);
                                                const isShedded = status.includes("Shedded");
                                                const currentPriority = getLoadPriorityNum(load);
                                                return (
                                                    <div
                                                        key={load.id}
                                                        className={`${styles.nodeCard} ${isShedded ? styles.nodeShedded : styles.nodeNormal}`}
                                                    >
                                                        <div className={styles.nodeIcon}>⚡</div>
                                                        <div className={styles.nodeName}>{load.name}</div>
                                                        <div className={styles.nodePriority}>
                                                            {load.powerRatingKw} kW | P{currentPriority}
                                                        </div>
                                                        <div className={styles.nodeBadge}>{status}</div>
                                                    </div>
                                                );
                                            })
                                        ) : (
                                            <p style={{ color: "#94a3b8", fontSize: "0.9rem" }}>No loads registered under this zone or its sub-zones.</p>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>

            {/* Charts Row */}
            <div className={styles.chartsRow}>
                {/* Real-Time Frequency Chart */}
                <div className={styles.chartCard}>
                    <h3 className={styles.chartTitle}>📈 Real-Time Frequency Chart (Hz)</h3>
                    <div className={styles.chartContainer}>{renderFrequencyChart()}</div>
                    <div className={styles.chartFooter}>
                        <span>Min: 48.0 Hz</span>
                        <span>Target: 50.0 Hz</span>
                        <span>Max: 52.0 Hz</span>
                    </div>
                </div>

                {/* Dynamic Load Breakdown */}
                <div className={styles.chartCard}>
                    <h3 className={styles.chartTitle}>⚡ Dynamic Priority Breakdown</h3>
                    <div className={styles.loadBreakdownContainer}>
                        <div className={styles.loadBarRow}>
                            <span>P1 (Critical)</span>
                            <span>{p1Kw.toFixed(1)} kW ({p1Pct}%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={styles.progressFillP1} style={{ width: `${p1Pct}%` }}></div>
                        </div>

                        <div className={styles.loadBarRow}>
                            <span>P2 (Essential)</span>
                            <span>{p2Kw.toFixed(1)} kW ({p2Pct}%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={styles.progressFillP2} style={{ width: `${p2Pct}%` }}></div>
                        </div>

                        <div className={styles.loadBarRow}>
                            <span>P3 (Non-Essential)</span>
                            <span>{p3Kw.toFixed(1)} kW ({p3Pct}%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={styles.progressFillP3} style={{ width: `${p3Pct}%` }}></div>
                        </div>
                    </div>
                </div>
            </div>

            {/* Alarms Log */}
            <div className={styles.sectionCard}>
                <h2 className={styles.sectionTitle}>🔔 Recent Alarms & Events Log</h2>
                {alarms.length === 0 ? (
                    <p className={styles.noAlarms}>✅ System operating normally. No active alarms.</p>
                ) : (
                    <div className={styles.alarmList}>
                        {alarms.map((alarm) => (
                            <div
                                key={alarm.id}
                                className={`${styles.alarmItem} ${alarm.type === "warning" || alarm.type === "critical"
                                    ? styles.alarmWarning
                                    : styles.alarmSuccess
                                    }`}
                            >
                                <span className={styles.alarmTime}>{alarm.time}</span>
                                <span className={styles.alarmMessage}>{alarm.message}</span>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}