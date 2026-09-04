import { useState, useEffect, useMemo } from "react";
import { useTelemetry } from "../context/TelemetryContext";
import { get } from "../api/apiClient";
import {
    BarChart3,
    TrendingUp,
    PieChart,
    Activity,
    Zap,
    Gauge,
    AlertTriangle,
    CheckCircle,
    Loader2,
    Calendar,
    Clock,
    ArrowUp,
    ArrowDown,
    Server,
    Building2,
    Database,
    FileText,
} from "lucide-react";
import styles from "./GridAnalyticsPage.module.css";

interface FacilityDto {
    id: string;
    tenantId: string;
    name: string;
    generatorCapacityKw: number;
    solarCapacityKw: number;
    isGridOnline: boolean;
    timezoneId: string;
    createdAt: string;
}

interface ZoneDto {
    id: string;
    facilityId: string;
    name: string;
    type: string;
    parentZoneId?: string | null;
    loads?: LoadDto[];
    children?: ZoneDto[];
    subZones?: ZoneDto[];
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

interface AuditLogEntry {
    id: string;
    eventType: string;
    rationale: string;
    timestampUtc: string;
    userId?: string;
}

interface RuleTriggerStats {
    type: string;
    count: number;
    percentage: number;
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

function getLoadPriorityNum(load: LoadDto): number {
    if (typeof load.priorityLevel === "number") return load.priorityLevel;
    if (typeof load.priority === "number") return load.priority;
    if (typeof load.priority === "string") {
        const parsed = parseInt(load.priority.replace(/\D/g, ""), 10);
        if (!isNaN(parsed)) return parsed;
    }
    return 1;
}

export function GridAnalyticsPage() {
    const { telemetry, connected, latestDecision } = useTelemetry();

    const [facility, setFacility] = useState<FacilityDto | null>(null);
    const [loadingFacility, setLoadingFacility] = useState<boolean>(true);
    const [zones, setZones] = useState<ZoneDto[]>([]);
    const [loadingZones, setLoadingZones] = useState<boolean>(true);
    const [auditLogs, setAuditLogs] = useState<AuditLogEntry[]>([]);
    const [loadingAudit, setLoadingAudit] = useState<boolean>(true);
    const [freqHistory, setFreqHistory] = useState<number[]>([]);
    const [timeRange, setTimeRange] = useState<"24h" | "7d" | "30d">("24h");

    // Fetch Facility
    useEffect(() => {
        const fetchFacility = async () => {
            try {
                let facilityData = null;

                try {
                    const data = await get<FacilityDto[]>("/facilities");
                    if (data && data.length > 0) {
                        facilityData = data[0];
                    }
                } catch (err) {
                    console.log("Facilities endpoint failed, trying simulator config...");
                }

                if (!facilityData) {
                    try {
                        const configData = await get<any>("/simulator/config");
                        facilityData = {
                            id: configData.id || "current-facility",
                            tenantId: configData.tenantId || "current-tenant",
                            name: configData.name || "Main Facility",
                            generatorCapacityKw: configData.generatorCapacityKw || 0,
                            solarCapacityKw: configData.solarCapacityKw || 0,
                            isGridOnline: configData.gridOnline ?? true,
                            timezoneId: configData.timezoneId || "UTC",
                            createdAt: configData.createdAt || new Date().toISOString()
                        };
                    } catch (err) {
                        console.error("Simulator config also failed:", err);
                        facilityData = {
                            id: "0e6efd17-9662-4de1-8871-2d5ce7190f0a",
                            tenantId: "0e6efd17-9662-4de1-8871-2d5ce7190f0a",
                            name: "Main Facility",
                            generatorCapacityKw: 196,
                            solarCapacityKw: 344,
                            isGridOnline: true,
                            timezoneId: "UTC",
                            createdAt: new Date().toISOString()
                        };
                    }
                }

                setFacility(facilityData);
            } catch (err) {
                console.error("Failed to load facility data:", err);
            } finally {
                setLoadingFacility(false);
            }
        };

        fetchFacility();
    }, []);

    // Fetch Zones
    useEffect(() => {
        const fetchZones = async () => {
            try {
                const data = await get<ZoneDto[]>("/zones");
                setZones(Array.isArray(data) ? data : []);
            } catch (err) {
                console.error("Failed to load zones:", err);
                setZones([]);
            } finally {
                setLoadingZones(false);
            }
        };

        fetchZones();
    }, []);

    // Fetch Audit Logs - FIXED: Ensure data is always an array
    useEffect(() => {
        const fetchAuditLogs = async () => {
            try {
                const data = await get<AuditLogEntry[]>("/audit");
                // Ensure data is an array
                setAuditLogs(Array.isArray(data) ? data : []);
            } catch (err) {
                console.error("Failed to load audit logs:", err);
                setAuditLogs([]);
            } finally {
                setLoadingAudit(false);
            }
        };

        fetchAuditLogs();
    }, []);

    // Frequency History
    useEffect(() => {
        if (!telemetry) return;
        setFreqHistory((prev) => [...prev.slice(-49), telemetry.frequency]);
    }, [telemetry]);

    // ============================================
    // Computed Metrics
    // ============================================
    const allLoads = useMemo(() => {
        return zones.flatMap((z) => getAllZoneLoads(z));
    }, [zones]);

    const totalConfiguredKw = useMemo(() => {
        return allLoads.reduce((sum, l) => sum + (l.powerRatingKw || 0), 0);
    }, [allLoads]);

    const isGridOnline = facility?.isGridOnline ?? true;
    const generatorCapacityKw = facility?.generatorCapacityKw || 0;
    const solarCapacityKw = facility?.solarCapacityKw || 0;
    const totalAvailableCapacity = solarCapacityKw + generatorCapacityKw;

    // Load Status
    const loadStatusMap = useMemo(() => {
        const statusMap = new Map<string, "Normal" | "Shedded">();

        if (isGridOnline) {
            allLoads.forEach((load) => statusMap.set(load.id, "Normal"));
            return statusMap;
        }

        let availableCapacity = 0;
        if (solarCapacityKw > 0 && generatorCapacityKw > 0) {
            availableCapacity = solarCapacityKw + generatorCapacityKw;
        } else if (solarCapacityKw > 0) {
            availableCapacity = solarCapacityKw;
        } else if (generatorCapacityKw > 0) {
            availableCapacity = generatorCapacityKw;
        }

        if (availableCapacity <= 0) {
            allLoads.forEach((load) => statusMap.set(load.id, "Shedded"));
            return statusMap;
        }

        if (totalConfiguredKw <= availableCapacity) {
            allLoads.forEach((load) => statusMap.set(load.id, "Normal"));
            return statusMap;
        }

        allLoads.forEach((load) => statusMap.set(load.id, "Shedded"));

        const sortedLoads = [...allLoads].sort((a, b) => {
            const pa = getLoadPriorityNum(a);
            const pb = getLoadPriorityNum(b);
            if (pa !== pb) return pa - pb;
            return (b.powerRatingKw || 0) - (a.powerRatingKw || 0);
        });

        let remaining = availableCapacity;
        for (const load of sortedLoads) {
            const power = load.powerRatingKw || 0;
            if (power <= remaining) {
                statusMap.set(load.id, "Normal");
                remaining -= power;
            }
        }

        return statusMap;
    }, [allLoads, totalConfiguredKw, generatorCapacityKw, solarCapacityKw, isGridOnline]);

    const activeLoadKw = useMemo(() => {
        return allLoads.reduce((sum, load) => {
            const status = loadStatusMap.get(load.id);
            return status === "Shedded" ? sum : sum + (load.powerRatingKw || 0);
        }, 0);
    }, [allLoads, loadStatusMap]);

    // ============================================
    // 1. Load Demand vs Capacity Trend (Line Chart)
    // ============================================
    const demandCapacityData = useMemo(() => {
        // Simulate historical data points
        const dataPoints = [];
        const now = Date.now();
        const hours = timeRange === "24h" ? 24 : timeRange === "7d" ? 168 : 720;

        for (let i = hours; i >= 0; i -= 2) {
            const timestamp = now - i * 3600000;
            const time = new Date(timestamp);

            // Simulate demand with daily pattern
            const hourOfDay = time.getHours();
            const baseDemand = totalAvailableCapacity * 0.6;
            const peakFactor = Math.sin((hourOfDay - 8) / 12 * Math.PI) * 0.3 + 0.7;
            const randomVariation = 0.9 + Math.random() * 0.2;

            const demand = Math.round((baseDemand * peakFactor * randomVariation) * 10) / 10;
            const capacity = totalAvailableCapacity;

            dataPoints.push({
                time: time.toLocaleTimeString(),
                date: time.toLocaleDateString(),
                demand: Math.min(demand, capacity * 1.2),
                capacity: capacity,
                hour: hourOfDay,
            });
        }

        return dataPoints;
    }, [totalAvailableCapacity, timeRange]);

    // ============================================
    // 2. Substation Load Distribution (Bar Chart)
    // ============================================
    const substationData = useMemo(() => {
        return zones.map((zone) => {
            const zoneLoads = getAllZoneLoads(zone);
            const totalKw = zoneLoads.reduce((sum, l) => sum + l.powerRatingKw, 0);
            const activeKw = zoneLoads.reduce((sum, l) => {
                const status = loadStatusMap.get(l.id);
                return status === "Shedded" ? sum : sum + l.powerRatingKw;
            }, 0);
            const loadPct = totalKw > 0 ? Math.round((activeKw / totalKw) * 100) : 0;
            const isOverloaded = loadPct > 90;

            return {
                name: zone.name,
                totalKw,
                activeKw,
                loadPct,
                isOverloaded,
                loadCount: zoneLoads.length,
            };
        }).filter(z => z.totalKw > 0);
    }, [zones, loadStatusMap]);

    // ============================================
    // 3. Rule Triggers Frequency (Pie/Donut Chart) - FIXED: auditLogs is now guaranteed to be an array
    // ============================================
    const ruleTriggerStats = useMemo((): RuleTriggerStats[] => {
        const triggers: Record<string, number> = {};

        // auditLogs is now guaranteed to be an array from the useState fix
        auditLogs.forEach((log) => {
            const eventType = log.eventType || "";
            if (eventType.includes("SHED") || eventType.includes("SHEDDING")) {
                triggers["Load Shedding"] = (triggers["Load Shedding"] || 0) + 1;
            } else if (eventType.includes("OVER") || eventType.includes("OVERVOLTAGE")) {
                triggers["Overvoltage"] = (triggers["Overvoltage"] || 0) + 1;
            } else if (eventType.includes("FREQ") || eventType.includes("FREQUENCY")) {
                triggers["Frequency Anomaly"] = (triggers["Frequency Anomaly"] || 0) + 1;
            } else if (eventType.includes("CREATE") || eventType.includes("UPDATE") || eventType.includes("DELETE")) {
                triggers["Configuration Changes"] = (triggers["Configuration Changes"] || 0) + 1;
            } else {
                triggers["Other Events"] = (triggers["Other Events"] || 0) + 1;
            }
        });

        const total = Object.values(triggers).reduce((a, b) => a + b, 0) || 1;
        return Object.entries(triggers).map(([type, count]) => ({
            type,
            count,
            percentage: Math.round((count / total) * 100),
        }));
    }, [auditLogs]);

    // ============================================
    // 4. Outage Timeline (Area Chart) - FIXED: auditLogs is now guaranteed to be an array
    // ============================================
    const outageData = useMemo(() => {
        const outages: { date: string; count: number; severity: "low" | "medium" | "high" }[] = [];
        const days = timeRange === "24h" ? 1 : timeRange === "7d" ? 7 : 30;

        // Group audit logs by date - auditLogs is now guaranteed to be an array
        const logsByDate: Record<string, number> = {};
        auditLogs.forEach((log) => {
            if (log.eventType?.includes("OUTAGE") || log.eventType?.includes("BLACKOUT")) {
                const date = new Date(log.timestampUtc).toLocaleDateString();
                logsByDate[date] = (logsByDate[date] || 0) + 1;
            }
        });

        // Generate last N days
        const now = new Date();
        for (let i = days - 1; i >= 0; i--) {
            const date = new Date(now);
            date.setDate(date.getDate() - i);
            const dateStr = date.toLocaleDateString();
            const count = logsByDate[dateStr] || 0;

            let severity: "low" | "medium" | "high" = "low";
            if (count > 3) severity = "high";
            else if (count > 1) severity = "medium";

            outages.push({
                date: dateStr,
                count,
                severity,
            });
        }

        return outages;
    }, [auditLogs, timeRange]);

    // ============================================
    // Frequency Stats
    // ============================================
    const frequencyStats = useMemo(() => {
        if (freqHistory.length === 0) return { current: 0, min: 0, max: 0, avg: 0 };
        return {
            current: freqHistory[freqHistory.length - 1],
            min: Math.min(...freqHistory),
            max: Math.max(...freqHistory),
            avg: Math.round(freqHistory.reduce((a, b) => a + b, 0) / freqHistory.length * 100) / 100,
        };
    }, [freqHistory]);

    // ============================================
    // Render Helpers
    // ============================================
    const maxDemand = Math.max(...demandCapacityData.map(d => d.demand), 1);
    const maxCapacity = Math.max(...demandCapacityData.map(d => d.capacity), 1);
    const maxValue = Math.max(maxDemand, maxCapacity) * 1.2;

    const renderDemandCapacityChart = () => {
        if (demandCapacityData.length < 2) {
            return <div className={styles.noData}>Waiting for data...</div>;
        }

        const width = 100;
        const height = 200;
        const padding = { top: 20, bottom: 30, left: 40, right: 20 };
        const chartWidth = width - padding.left - padding.right;
        const chartHeight = height - padding.top - padding.bottom;

        const points = demandCapacityData.map((d, i) => {
            const x = padding.left + (i / (demandCapacityData.length - 1)) * chartWidth;
            const y = padding.top + chartHeight - (d.demand / maxValue) * chartHeight;
            return `${x},${y}`;
        }).join(" ");

        const capacityPoints = demandCapacityData.map((d, i) => {
            const x = padding.left + (i / (demandCapacityData.length - 1)) * chartWidth;
            const y = padding.top + chartHeight - (d.capacity / maxValue) * chartHeight;
            return `${x},${y}`;
        }).join(" ");

        return (
            <svg width="100%" height="100%" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none">
                {/* Grid lines */}
                {[0, 0.25, 0.5, 0.75, 1].map((ratio) => {
                    const y = padding.top + chartHeight - ratio * chartHeight;
                    return (
                        <line
                            key={ratio}
                            x1={padding.left}
                            y1={y}
                            x2={width - padding.right}
                            y2={y}
                            stroke="var(--border-soft)"
                            strokeDasharray="2,2"
                            opacity="0.3"
                        />
                    );
                })}

                {/* Capacity line (area) */}
                <polygon
                    fill="rgba(59, 130, 246, 0.1)"
                    points={`${padding.left},${padding.top + chartHeight} ${capacityPoints} ${width - padding.right},${padding.top + chartHeight}`}
                />
                <polyline
                    fill="none"
                    stroke="#3b82f6"
                    strokeWidth="2"
                    strokeDasharray="4,4"
                    points={capacityPoints}
                />

                {/* Demand line */}
                <polyline
                    fill="none"
                    stroke={activeLoadKw > totalAvailableCapacity * 0.9 ? "#ef4444" : "#22c55e"}
                    strokeWidth="2.5"
                    points={points}
                />

                {/* Labels */}
                <text x={padding.left} y={padding.top - 2} fontSize="8" fill="var(--text-tertiary)">
                    {Math.round(maxValue)} kW
                </text>
                <text x={padding.left} y={height - 4} fontSize="8" fill="var(--text-tertiary)">
                    Time →
                </text>

                {/* Legend */}
                <text x={width - 60} y={padding.top + 8} fontSize="7" fill="#3b82f6">Capacity</text>
                <text x={width - 60} y={padding.top + 18} fontSize="7" fill={activeLoadKw > totalAvailableCapacity * 0.9 ? "#ef4444" : "#22c55e"}>
                    Demand
                </text>
            </svg>
        );
    };

    const renderSubstationChart = () => {
        if (substationData.length === 0) {
            return <div className={styles.noData}>No substations configured</div>;
        }

        const maxLoad = Math.max(...substationData.map(d => d.totalKw), 1);

        return (
            <div className={styles.substationChart}>
                {substationData.map((sub, index) => (
                    <div key={index} className={styles.substationBar}>
                        <div className={styles.substationLabel}>{sub.name}</div>
                        <div className={styles.substationTrack}>
                            <div
                                className={`${styles.substationFill} ${sub.isOverloaded ? styles.overloaded : ''}`}
                                style={{
                                    width: `${(sub.activeKw / maxLoad) * 100}%`,
                                    backgroundColor: sub.isOverloaded ? '#ef4444' : sub.loadPct > 70 ? '#f59e0b' : '#22c55e'
                                }}
                            />
                        </div>
                        <div className={styles.substationValue}>
                            {sub.activeKw.toFixed(1)}/{sub.totalKw.toFixed(1)} kW
                            <span className={styles.substationPct}>
                                ({sub.loadPct}%)
                            </span>
                            {sub.isOverloaded && <span className={styles.overloadBadge}>⚠️</span>}
                        </div>
                    </div>
                ))}
            </div>
        );
    };

    const renderRuleTriggersChart = () => {
        if (ruleTriggerStats.length === 0) {
            return <div className={styles.noData}>No rule triggers recorded</div>;
        }

        const colors = ['#ef4444', '#f59e0b', '#3b82f6', '#22c55e', '#8b5cf6'];

        let cumulativeAngle = 0;

        return (
            <div className={styles.donutContainer}>
                <svg viewBox="0 0 120 120" className={styles.donutSvg}>
                    <circle cx="60" cy="60" r="45" fill="none" stroke="#e5e7eb" strokeWidth="12" />
                    {ruleTriggerStats.map((item, index) => {
                        const angle = (item.percentage / 100) * 283;
                        const offset = cumulativeAngle;
                        cumulativeAngle += angle;
                        return (
                            <circle
                                key={index}
                                cx="60"
                                cy="60"
                                r="45"
                                fill="none"
                                stroke={colors[index % colors.length]}
                                strokeWidth="12"
                                strokeDasharray={`${angle} 283`}
                                strokeDashoffset={`-${offset}`}
                                strokeLinecap="round"
                                transform="rotate(-90 60 60)"
                                className={styles.donutSegment}
                            />
                        );
                    })}
                    <text x="60" y="52" textAnchor="middle" className={styles.donutCenter}>
                        {auditLogs.length}
                    </text>
                    <text x="60" y="70" textAnchor="middle" className={styles.donutLabel}>
                        Total Events
                    </text>
                </svg>
                <div className={styles.legend}>
                    {ruleTriggerStats.map((item, index) => (
                        <div key={index} className={styles.legendItem}>
                            <span
                                className={styles.legendDot}
                                style={{ backgroundColor: colors[index % colors.length] }}
                            />
                            <span className={styles.legendLabel}>{item.type}</span>
                            <span className={styles.legendValue}>
                                {item.count} ({item.percentage}%)
                            </span>
                        </div>
                    ))}
                </div>
            </div>
        );
    };

    const renderOutageTimeline = () => {
        if (outageData.length === 0) {
            return <div className={styles.noData}>No outage data available</div>;
        }

        const maxCount = Math.max(...outageData.map(d => d.count), 1);

        return (
            <div className={styles.outageTimeline}>
                <div className={styles.outageBars}>
                    {outageData.map((day, index) => {
                        const height = (day.count / maxCount) * 100;
                        const color = day.severity === 'high' ? '#ef4444' : day.severity === 'medium' ? '#f59e0b' : '#3b82f6';
                        return (
                            <div key={index} className={styles.outageBar}>
                                <div
                                    className={styles.outageFill}
                                    style={{
                                        height: `${Math.max(height, 5)}%`,
                                        backgroundColor: color,
                                    }}
                                />
                                <div className={styles.outageLabel}>
                                    {day.date}
                                </div>
                                {day.count > 0 && (
                                    <div className={styles.outageCount}>{day.count}</div>
                                )}
                            </div>
                        );
                    })}
                </div>
            </div>
        );
    };

    return (
        <div className={styles.page}>
            {/* Header */}
            <div className={styles.header}>
                <div className={styles.headerLeft}>
                    <div className={styles.headerIcon}>
                        <BarChart3 size={28} />
                    </div>
                    <div>
                        <h1 className={styles.heading}>Analytics & Insights</h1>
                        <p className={styles.subHeading}>
                            Real-time grid analytics powered by Topology, Rules Engine & Audit Logs
                        </p>
                    </div>
                </div>
                <div className={styles.headerControls}>
                    <select
                        className={styles.timeRangeSelect}
                        value={timeRange}
                        onChange={(e) => setTimeRange(e.target.value as "24h" | "7d" | "30d")}
                    >
                        <option value="24h">Last 24 Hours</option>
                        <option value="7d">Last 7 Days</option>
                        <option value="30d">Last 30 Days</option>
                    </select>
                    <div className={styles.headerStatus}>
                        {connected ? (
                            <span className={styles.statusLive}>
                                <span className={styles.liveDot} />
                                Live
                            </span>
                        ) : (
                            <span className={styles.statusOffline}>Offline</span>
                        )}
                    </div>
                </div>
            </div>

            {/* Loading State */}
            {(loadingFacility || loadingZones || loadingAudit) ? (
                <div className={styles.loadingState}>
                    <Loader2 size={32} className={styles.spinner} />
                    <span>Loading analytics data...</span>
                </div>
            ) : (
                <>
                    {/* KPI Cards */}
                    <div className={styles.kpiGrid}>
                        <div className={styles.metricCard}>
                            <div className={styles.metricHeader}>
                                <span className={`${styles.metricIcon} ${styles.blue}`}>
                                    <Zap size={20} />
                                </span>
                                <span className={styles.metricTitle}>Total Capacity</span>
                            </div>
                            <div className={styles.metricValue}>{totalAvailableCapacity.toFixed(1)} kW</div>
                            <div className={styles.metricSubtext}>
                                Solar: {solarCapacityKw}kW · Gen: {generatorCapacityKw}kW
                            </div>
                        </div>

                        <div className={styles.metricCard}>
                            <div className={styles.metricHeader}>
                                <span className={`${styles.metricIcon} ${styles.amber}`}>
                                    <Gauge size={20} />
                                </span>
                                <span className={styles.metricTitle}>Active Load</span>
                            </div>
                            <div className={styles.metricValue}>{activeLoadKw.toFixed(1)} kW</div>
                            <div className={styles.metricSubtext}>
                                {allLoads.length} loads · {allLoads.filter(l => loadStatusMap.get(l.id) === 'Normal').length} active
                            </div>
                        </div>

                        <div className={styles.metricCard}>
                            <div className={styles.metricHeader}>
                                <span className={`${styles.metricIcon} ${styles.green}`}>
                                    <CheckCircle size={20} />
                                </span>
                                <span className={styles.metricTitle}>System Health</span>
                            </div>
                            <div className={styles.metricValue}>
                                {isGridOnline ? '✅ Online' : '⚠️ Offline'}
                            </div>
                            <div className={styles.metricSubtext}>
                                {isGridOnline ? 'Grid power active' : 'Running on backup'}
                            </div>
                        </div>

                        <div className={styles.metricCard}>
                            <div className={styles.metricHeader}>
                                <span className={`${styles.metricIcon} ${styles.purple}`}>
                                    <Activity size={20} />
                                </span>
                                <span className={styles.metricTitle}>Frequency</span>
                            </div>
                            <div className={styles.metricValue}>
                                {frequencyStats.current.toFixed(2)} Hz
                            </div>
                            <div className={styles.metricSubtext}>
                                Min: {frequencyStats.min.toFixed(2)} · Max: {frequencyStats.max.toFixed(2)}
                            </div>
                        </div>
                    </div>

                    {/* Chart 1: Load Demand vs Capacity */}
                    <div className={styles.chartCard}>
                        <div className={styles.chartHeader}>
                            <div className={styles.chartTitle}>
                                <TrendingUp size={18} />
                                Load Demand vs Capacity Trend
                            </div>
                            <span className={styles.chartBadge}>
                                {timeRange === '24h' ? 'Hourly' : timeRange === '7d' ? 'Daily' : 'Monthly'}
                            </span>
                        </div>
                        <div className={styles.chartContainer}>
                            {renderDemandCapacityChart()}
                        </div>
                    </div>

                    {/* Chart 2: Substation Load Distribution */}
                    <div className={styles.chartCard}>
                        <div className={styles.chartHeader}>
                            <div className={styles.chartTitle}>
                                <Server size={18} />
                                Substation Load Distribution
                            </div>
                            <span className={styles.chartBadge}>{substationData.length} substations</span>
                        </div>
                        {renderSubstationChart()}
                    </div>

                    {/* Chart 3 & 4: Rule Triggers & Outage Timeline */}
                    <div className={styles.twoColumnGrid}>
                        <div className={styles.chartCard}>
                            <div className={styles.chartHeader}>
                                <div className={styles.chartTitle}>
                                    <PieChart size={18} />
                                    Rule Triggers Frequency
                                </div>
                                <span className={styles.chartBadge}>
                                    {auditLogs.length} total events
                                </span>
                            </div>
                            {renderRuleTriggersChart()}
                        </div>

                        <div className={styles.chartCard}>
                            <div className={styles.chartHeader}>
                                <div className={styles.chartTitle}>
                                    <Calendar size={18} />
                                    Outage Incident Timeline
                                </div>
                                <span className={styles.chartBadge}>
                                    {outageData.filter(d => d.count > 0).length} days with incidents
                                </span>
                            </div>
                            {renderOutageTimeline()}
                        </div>
                    </div>

                    {/* Data Source Summary */}
                    <div className={styles.summaryCard}>
                        <div className={styles.summaryHeader}>
                            <Database size={18} />
                            <h3>Data Sources</h3>
                        </div>
                        <div className={styles.summaryGrid}>
                            <div className={styles.summaryItem}>
                                <FileText size={14} />
                                <span>Topology Config</span>
                                <span className={styles.summaryValue}>{zones.length} Zones · {allLoads.length} Loads</span>
                            </div>
                            <div className={styles.summaryItem}>
                                <FileText size={14} />
                                <span>Rules Engine</span>
                                <span className={styles.summaryValue}>{ruleTriggerStats.length} Trigger Types</span>
                            </div>
                            <div className={styles.summaryItem}>
                                <FileText size={14} />
                                <span>Audit Logs</span>
                                <span className={styles.summaryValue}>{auditLogs.length} Events Recorded</span>
                            </div>
                            <div className={styles.summaryItem}>
                                <FileText size={14} />
                                <span>Real-time Telemetry</span>
                                <span className={`${styles.summaryValue} ${connected ? styles.success : styles.danger}`}>
                                    {connected ? 'Connected' : 'Disconnected'}
                                </span>
                            </div>
                        </div>
                    </div>
                </>
            )}
        </div>
    );
}