import { useState, useEffect, useMemo } from "react";
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

function getLoadPriorityNum(load: LoadDto): number {
    if (typeof load.priorityLevel === "number") return load.priorityLevel;
    if (typeof load.priority === "number") return load.priority;
    if (typeof load.priority === "string") {
        const parsed = parseInt(load.priority.replace(/\D/g, ""), 10);
        if (!isNaN(parsed)) return parsed;
    }
    return 1;
}

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

    const [zones, setZones] = useState<ZoneDto[]>([]);
    const [loadingDbData, setLoadingDbData] = useState<boolean>(true);

    const [freqHistory, setFreqHistory] = useState<number[]>([]);
    const [alarms, setAlarms] = useState<AlarmLog[]>([]);

    const GENERATOR_CAPACITY_KW = 500.0;

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

    useEffect(() => {
        if (!telemetry) return;

        setFreqHistory((prev) => [...prev.slice(-19), telemetry.frequency]);

        if (telemetry.frequency < 49.5) {
            const timeStr = new Date().toLocaleTimeString();
            const alarmMsg = `⚠️ Low Frequency Warning: ${telemetry.frequency.toFixed(2)} Hz`;

            setAlarms((prev) => {
                if (prev[0]?.message === alarmMsg) return prev;
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

    const allLoads = useMemo(() => {
        return zones.flatMap((z) => getAllZoneLoads(z));
    }, [zones]);

    const totalConfiguredKw = useMemo(() => {
        return allLoads.reduce((sum, l) => sum + (l.powerRatingKw || 0), 0);
    }, [allLoads]);

    // Create a Set of Shedded Relay Addresses from WebSocket decision
    const sheddedRelayAddresses = useMemo(() => {
        const set = new Set<number>();
        if (latestDecision?.relayDecisions) {
            latestDecision.relayDecisions.forEach((r: RelayDecision) => {
                if (!r.energize && r.relayAddress !== undefined) {
                    set.add(r.relayAddress);
                }
            });
        }
        return set;
    }, [latestDecision]);

    // Calculate actual active load considering shedding decisions
    const realTimeLoadKw = useMemo(() => {
        if (telemetry?.totalLoadKw !== undefined) {
            return telemetry.totalLoadKw;
        }
        return allLoads.reduce((sum, load) => {
            const isShed = load.relayAddress !== undefined && sheddedRelayAddresses.has(load.relayAddress);
            return isShed ? sum : sum + (load.powerRatingKw || 0);
        }, 0);
    }, [telemetry, allLoads, sheddedRelayAddresses]);

    const capacityUsagePct = Math.round((realTimeLoadKw / GENERATOR_CAPACITY_KW) * 100);

    const { p1Kw, p2Kw, p3Kw, p1Pct, p2Pct, p3Pct } = useMemo(() => {
        let p1 = 0, p2 = 0, p3 = 0;

        allLoads.forEach((load) => {
            const priority = getLoadPriorityNum(load);
            const kw = load.powerRatingKw || 0;
            if (priority === 1) p1 += kw;
            else if (priority === 2) p2 += kw;
            else if (priority === 3) p3 += kw;
        });

        const pct1 = totalConfiguredKw > 0 ? Math.round((p1 / totalConfiguredKw) * 100) : 0;
        const pct2 = totalConfiguredKw > 0 ? Math.round((p2 / totalConfiguredKw) * 100) : 0;
        const pct3 = totalConfiguredKw > 0 ? Math.round((p3 / totalConfiguredKw) * 100) : 0;

        return { p1Kw: p1, p2Kw: p2, p3Kw: p3, p1Pct: pct1, p2Pct: pct2, p3Pct: pct3 };
    }, [allLoads, totalConfiguredKw]);

    // Map of individual load statuses determined in a single deterministic pass
    const loadStatusMap = useMemo(() => {
        const statusMap = new Map<string, "Normal" | "Shedded">();

        // 1. Mark status via direct backend Relay Decisions
        allLoads.forEach((load) => {
            if (load.relayAddress !== undefined && sheddedRelayAddresses.has(load.relayAddress)) {
                statusMap.set(load.id, "Shedded");
            } else {
                statusMap.set(load.id, "Normal");
            }
        });

        // 2. Fallback Overload Simulation if telemetry is exceeding capacity
        if (realTimeLoadKw > GENERATOR_CAPACITY_KW) {
            let excess = realTimeLoadKw - GENERATOR_CAPACITY_KW;

            // Sort loads from lowest priority (P3 -> P2 -> P1) for sequential shedding
            const sortedLoads = [...allLoads].sort(
                (a, b) => getLoadPriorityNum(b) - getLoadPriorityNum(a)
            );

            for (const load of sortedLoads) {
                if (excess <= 0) break;
                if (statusMap.get(load.id) === "Normal") {
                    statusMap.set(load.id, "Shedded");
                    excess -= load.powerRatingKw || 0;
                }
            }
        }

        return statusMap;
    }, [allLoads, sheddedRelayAddresses, realTimeLoadKw, GENERATOR_CAPACITY_KW]);

    const getLoadStatus = (load: LoadDto) => {
        return loadStatusMap.get(load.id) || "Normal";
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
                <line x1="0" y1="25" x2={width} y2="25" stroke="var(--border-soft)" strokeDasharray="2,2" opacity="0.3" />
                <line x1="0" y1="50" x2={width} y2="50" stroke="var(--border-soft)" strokeDasharray="4,4" opacity="0.3" />
                <line x1="0" y1="75" x2={width} y2="75" stroke="var(--border-soft)" strokeDasharray="2,2" opacity="0.3" />
                <polyline
                    fill="none"
                    stroke={isUnderFrequency ? "var(--accent-red)" : "var(--success)"}
                    strokeWidth="2.5"
                    points={points}
                />
                <polyline
                    fill="none"
                    stroke={isUnderFrequency ? "var(--accent-red)" : "var(--success)"}
                    strokeWidth="2.5"
                    points={points}
                    opacity="0.15"
                    strokeLinecap="round"
                />
            </svg>
        );
    };

    return (
        <div className={styles.page}>
            {/* Header */}
            <div className={styles.headerRow}>
                <div>
                    <h1 className={styles.heading}>Live Overview</h1>
                    <p className={styles.headingSub}>Real-time monitoring & control</p>
                </div>
                <div className={`${styles.systemModeBanner} ${systemMode.className}`}>
                    <span className={styles.modeDot}></span>
                    {systemMode.text}
                </div>
            </div>

            <div className={styles.statusRow}>
                <span className={connected ? styles.statusConnected : styles.statusDisconnected}>
                    {connected ? "● Live Stream Connected" : "○ Offline / Reconnecting..."}
                </span>
            </div>

            {/* KPI Cards */}
            <div className={styles.grid}>
                <div className={`${styles.card} ${isUnderFrequency ? styles.cardDanger : ""}`}>
                    <div className={styles.cardIcon}>⚡</div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Grid Voltage</h3>
                        <p className={`${styles.cardValue} ${styles.valueBlue}`}>
                            {telemetry ? `${telemetry.voltage.toFixed(1)} V` : "—"}
                        </p>
                    </div>
                </div>

                <div className={styles.card}>
                    <div className={styles.cardIcon}>📊</div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Active Real-Time Load</h3>
                        <p className={`${styles.cardValue} ${styles.valueAmber}`}>
                            {loadingDbData ? "..." : `${realTimeLoadKw.toFixed(1)} kW`}
                        </p>
                    </div>
                </div>

                <div className={styles.card}>
                    <div className={styles.cardIcon}>🏭</div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Gen Capacity</h3>
                        <p className={`${styles.cardValue} ${styles.valueBlue}`}>
                            {`${GENERATOR_CAPACITY_KW.toFixed(0)} kW`}
                        </p>
                        <span className={styles.cardSubText}>{capacityUsagePct}% Load Ratio</span>
                    </div>
                </div>

                <div className={`${styles.card} ${isUnderFrequency ? styles.cardDanger : ""}`}>
                    <div className={styles.cardIcon}>📈</div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>System Frequency</h3>
                        <p className={`${styles.cardValue} ${isUnderFrequency ? styles.valueDanger : styles.valueSuccess}`}>
                            {telemetry ? `${telemetry.frequency.toFixed(2)} Hz` : "—"}
                        </p>
                        {isUnderFrequency && <span className={styles.warning}>⚠️ Under-Frequency</span>}
                    </div>
                </div>

                <div className={styles.card}>
                    <div className={styles.cardIcon}>🔌</div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Generator Status</h3>
                        <p className={`${styles.cardValue} ${telemetry?.generatorOn ? styles.valueSuccess : styles.valueMuted}`}>
                            {telemetry ? (telemetry.generatorOn ? "ON" : "OFF") : "—"}
                        </p>
                    </div>
                </div>

                <div className={styles.card}>
                    <div className={styles.cardIcon}>🌡️</div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Engine Temp</h3>
                        <p className={`${styles.cardValue} ${styles.valueAmber}`}>
                            {telemetry?.engineTemp !== undefined ? `${telemetry.engineTemp.toFixed(1)} °C` : "—"}
                        </p>
                    </div>
                </div>

                <div className={styles.card}>
                    <div className={styles.cardIcon}>⛽</div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Fuel Level</h3>
                        <p className={`${styles.cardValue} ${styles.valueBlue}`}>
                            {telemetry?.fuelLevel !== undefined ? `${telemetry.fuelLevel.toFixed(0)} %` : "—"}
                        </p>
                    </div>
                </div>

                <div className={styles.card}>
                    <div className={styles.cardIcon}>⏱️</div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Est. Runtime</h3>
                        <p className={`${styles.cardValue} ${styles.valueSuccess}`}>
                            {telemetry?.runtimeRemaining !== undefined ? `${telemetry.runtimeRemaining.toFixed(1)} hrs` : "—"}
                        </p>
                    </div>
                </div>
            </div>

            {/* System Status Map */}
            <div className={styles.sectionCard}>
                <div className={styles.sectionHeader}>
                    <h2 className={styles.sectionTitle}>🗺️ System Status Map</h2>
                    <span className={styles.sectionBadge}>{zones.length} Zones</span>
                </div>
                {loadingDbData ? (
                    <div className={styles.loadingState}>
                        <span className={styles.spinner}></span>
                        Loading database hierarchy...
                    </div>
                ) : zones.length === 0 ? (
                    <div className={styles.emptyState}>
                        <p>No zones found in database. Create a Zone to view telemetry mapping.</p>
                    </div>
                ) : (
                    <div className={styles.zoneList}>
                        {zones.map((zone) => {
                            const zoneLoads = getAllZoneLoads(zone);
                            return (
                                <div key={zone.id} className={styles.zoneCard}>
                                    <div className={styles.zoneHeader}>
                                        <div className={styles.zoneTitle}>
                                            <span className={styles.zoneIcon}>🏢</span>
                                            <h3>{zone.name}</h3>
                                        </div>
                                        <span className={styles.zoneLoadCount}>{zoneLoads.length} loads</span>
                                    </div>

                                    <div className={styles.nodeGrid}>
                                        {zoneLoads.length > 0 ? (
                                            zoneLoads.map((load) => {
                                                const status = getLoadStatus(load);
                                                const isShedded = status === "Shedded";
                                                const currentPriority = getLoadPriorityNum(load);
                                                return (
                                                    <div
                                                        key={load.id}
                                                        className={`${styles.nodeCard} ${isShedded ? styles.nodeShedded : styles.nodeNormal}`}
                                                    >
                                                        <div className={styles.nodeIcon}>⚡</div>
                                                        <div className={styles.nodeInfo}>
                                                            <div className={styles.nodeName}>{load.name}</div>
                                                            <div className={styles.nodeMeta}>
                                                                {load.powerRatingKw} kW · P{currentPriority}
                                                            </div>
                                                        </div>
                                                        <div className={`${styles.nodeBadge} ${isShedded ? styles.badgeShedded : styles.badgeNormal}`}>
                                                            {isShedded ? "⛔ Shedded" : "✅ Normal"}
                                                        </div>
                                                    </div>
                                                );
                                            })
                                        ) : (
                                            <p className={styles.noLoads}>No loads registered under this zone</p>
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
                <div className={styles.chartCard}>
                    <h3 className={styles.chartTitle}>📈 Real-Time Frequency</h3>
                    <div className={styles.chartContainer}>{renderFrequencyChart()}</div>
                    <div className={styles.chartFooter}>
                        <span>48.0 Hz</span>
                        <span className={styles.chartTarget}>Target: 50.0 Hz</span>
                        <span>52.0 Hz</span>
                    </div>
                </div>

                <div className={styles.chartCard}>
                    <h3 className={styles.chartTitle}>⚡ Priority Breakdown</h3>
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

            {/* Alarms */}
            <div className={styles.sectionCard}>
                <div className={styles.sectionHeader}>
                    <h2 className={styles.sectionTitle}>🔔 Recent Alarms & Events</h2>
                    <span className={styles.alarmCount}>{alarms.length} events</span>
                </div>
                {alarms.length === 0 ? (
                    <div className={styles.noAlarms}>
                        <span className={styles.noAlarmsIcon}>✅</span>
                        System operating normally. No active alarms.
                    </div>
                ) : (
                    <div className={styles.alarmList}>
                        {alarms.map((alarm) => (
                            <div
                                key={alarm.id}
                                className={`${styles.alarmItem} ${alarm.type === "critical"
                                    ? styles.alarmCritical
                                    : alarm.type === "warning"
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