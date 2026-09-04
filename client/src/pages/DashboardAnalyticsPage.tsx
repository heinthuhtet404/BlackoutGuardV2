import { useState, useEffect, useMemo } from "react";
import { useTelemetry } from "../context/TelemetryContext";
import { get } from "../api/apiClient";
import {
    Activity,
    Zap,
    Gauge,
    Clock,
    AlertTriangle,
    CheckCircle,
    TrendingUp,
    TrendingDown,
    BarChart3,
    PieChart,
    LineChart,
    Users,
    Server,
    Battery,
    Sun,
    Factory,
    ArrowUp,
    ArrowDown,
    Loader2,
} from "lucide-react";
import styles from "./DashboardAnalyticsPage.module.css";

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

interface AlarmLog {
    id: string;
    time: string;
    message: string;
    type: "warning" | "critical" | "success";
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

export function DashboardAnalyticsPage() {
    const { telemetry, connected, latestDecision } = useTelemetry();

    const [facility, setFacility] = useState<FacilityDto | null>(null);
    const [loadingFacility, setLoadingFacility] = useState<boolean>(true);
    const [zones, setZones] = useState<ZoneDto[]>([]);
    const [loadingZones, setLoadingZones] = useState<boolean>(true);
    const [freqHistory, setFreqHistory] = useState<number[]>([]);

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
                setZones(data || []);
            } catch (err) {
                console.error("Failed to load zones:", err);
            } finally {
                setLoadingZones(false);
            }
        };

        fetchZones();
    }, []);

    // Frequency History
    useEffect(() => {
        if (!telemetry) return;
        setFreqHistory((prev) => [...prev.slice(-29), telemetry.frequency]);
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

    // Load Status Calculation
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

    // Priority Breakdown
    const priorityData = useMemo(() => {
        let p1 = 0, p2 = 0, p3 = 0;
        allLoads.forEach((load) => {
            const priority = getLoadPriorityNum(load);
            const kw = load.powerRatingKw || 0;
            if (priority === 1) p1 += kw;
            else if (priority === 2) p2 += kw;
            else if (priority === 3) p3 += kw;
        });
        const total = p1 + p2 + p3 || 1;
        return {
            p1: { kw: p1, pct: Math.round((p1 / total) * 100) },
            p2: { kw: p2, pct: Math.round((p2 / total) * 100) },
            p3: { kw: p3, pct: Math.round((p3 / total) * 100) },
        };
    }, [allLoads]);

    // System Efficiency
    const systemEfficiency = useMemo(() => {
        if (totalAvailableCapacity === 0) return 0;
        return Math.round((activeLoadKw / totalAvailableCapacity) * 100);
    }, [activeLoadKw, totalAvailableCapacity]);

    // Active Alarms Count
    const activeAlarms = useMemo(() => {
        let count = 0;
        if (!connected) count++;
        if (telemetry && telemetry.frequency < 49.5) count++;
        if (!isGridOnline && generatorCapacityKw === 0 && solarCapacityKw === 0) count++;
        if (activeLoadKw > totalAvailableCapacity && totalAvailableCapacity > 0) count++;
        return count;
    }, [connected, telemetry, isGridOnline, generatorCapacityKw, solarCapacityKw, activeLoadKw, totalAvailableCapacity]);

    // Power Distribution
    // Power Distribution - ပြင်ဆင်ထားတဲ့အပိုင်း
    const powerDistribution = useMemo(() => {
        // Grid ကို အမြဲတမ်းပြမယ် (Grid On ဆိုရင် totalAvailableCapacity ကိုပြမယ်)
        const grid = isGridOnline ? totalAvailableCapacity : 0;

        // Solar ကို အမြဲတမ်းပြမယ်
        const solar = solarCapacityKw;

        // Generator ကို အမြဲတမ်းပြမယ် (Grid Off မှသာ သုံးတာဖြစ်ပေမယ့် Capacity ကိုတော့ ပြထားမယ်)
        const generator = generatorCapacityKw;

        const total = grid + solar + generator || 1;

        return {
            grid: { kw: grid, pct: Math.round((grid / total) * 100) },
            solar: { kw: solar, pct: Math.round((solar / total) * 100) },
            generator: { kw: generator, pct: Math.round((generator / total) * 100) },
        };
    }, [isGridOnline, solarCapacityKw, generatorCapacityKw, totalAvailableCapacity]);

    // Frequency Stats
    const frequencyStats = useMemo(() => {
        if (freqHistory.length === 0) return { current: 0, min: 0, max: 0, avg: 0 };
        const sorted = [...freqHistory];
        return {
            current: sorted[sorted.length - 1],
            min: Math.min(...sorted),
            max: Math.max(...sorted),
            avg: Math.round(sorted.reduce((a, b) => a + b, 0) / sorted.length * 100) / 100,
        };
    }, [freqHistory]);

    // ============================================
    // Render Helper Functions
    // ============================================
    const renderMetricCard = (title: string, value: string | number, icon: React.ReactNode, color: string, subtext?: string) => (
        <div className={styles.metricCard}>
            <div className={styles.metricHeader}>
                <span className={`${styles.metricIcon} ${styles[color]}`}>{icon}</span>
                <span className={styles.metricTitle}>{title}</span>
            </div>
            <div className={styles.metricValue}>{value}</div>
            {subtext && <div className={styles.metricSubtext}>{subtext}</div>}
        </div>
    );

    return (
        <div className={styles.page}>
            {/* Header */}
            <div className={styles.header}>
                <div className={styles.headerLeft}>
                    <div className={styles.headerIcon}>
                        <BarChart3 size={28} />
                    </div>
                    <div>
                        <h1 className={styles.heading}>Dashboard Analytics</h1>
                        <p className={styles.subHeading}>Real-time system insights and performance metrics</p>
                    </div>
                </div>
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

            {/* Loading State */}
            {(loadingFacility || loadingZones) ? (
                <div className={styles.loadingState}>
                    <Loader2 size={32} className={styles.spinner} />
                    <span>Loading analytics data...</span>
                </div>
            ) : (
                <>
                    {/* KPI Cards */}
                    {/* <div className={styles.kpiGrid}>
                        {renderMetricCard(
                            "Total Capacity",
                            `${totalAvailableCapacity.toFixed(1)} kW`,
                            <Zap size={20} />,
                            "blue",
                            `Solar: ${solarCapacityKw}kW · Gen: ${generatorCapacityKw}kW`
                        )}
                        {renderMetricCard(
                            "Active Load",
                            `${activeLoadKw.toFixed(1)} kW`,
                            <Gauge size={20} />,
                            "amber",
                            `${allLoads.length} loads configured`
                        )}
                        {renderMetricCard(
                            "System Efficiency",
                            `${systemEfficiency}%`,
                            <Activity size={20} />,
                            systemEfficiency > 70 ? "green" : systemEfficiency > 40 ? "amber" : "red",
                            `${totalAvailableCapacity > 0 ? `${(activeLoadKw / totalAvailableCapacity * 100).toFixed(0)}% utilized` : 'No capacity'}`
                        )}
                        {renderMetricCard(
                            "Active Alarms",
                            activeAlarms,
                            <AlertTriangle size={20} />,
                            activeAlarms > 0 ? "red" : "green",
                            activeAlarms > 0 ? `${activeAlarms} issue(s) detected` : "All systems normal"
                        )}
                    </div> */}

                    {/* Charts Grid */}
                    <div className={styles.chartsGrid}>
                        {/* Power Distribution */}
                        <div className={styles.chartCard}>
                            <div className={styles.chartHeader}>
                                <div className={styles.chartTitle}>
                                    <BarChart3 size={18} />
                                    Power Distribution
                                </div>
                                <span className={styles.chartBadge}>Current</span>
                            </div>
                            <div className={styles.powerDistribution}>
                                <div className={styles.distBar}>
                                    <div className={styles.distLabel}>Grid</div>
                                    <div className={styles.distTrack}>
                                        <div
                                            className={`${styles.distFill} ${styles.fillGrid}`}
                                            style={{ width: `${powerDistribution.grid.pct}%` }}
                                        />
                                    </div>
                                    <div className={styles.distValue}>
                                        {powerDistribution.grid.kw > 0 ? `${powerDistribution.grid.kw}kW` : '—'}
                                        <span className={styles.distPct}>({powerDistribution.grid.pct}%)</span>
                                    </div>
                                </div>
                                <div className={styles.distBar}>
                                    <div className={styles.distLabel}>Solar</div>
                                    <div className={styles.distTrack}>
                                        <div
                                            className={`${styles.distFill} ${styles.fillSolar}`}
                                            style={{ width: `${powerDistribution.solar.pct}%` }}
                                        />
                                    </div>
                                    <div className={styles.distValue}>
                                        {powerDistribution.solar.kw > 0 ? `${powerDistribution.solar.kw}kW` : '—'}
                                        <span className={styles.distPct}>({powerDistribution.solar.pct}%)</span>
                                    </div>
                                </div>
                                <div className={styles.distBar}>
                                    <div className={styles.distLabel}>Generator</div>
                                    <div className={styles.distTrack}>
                                        <div
                                            className={`${styles.distFill} ${styles.fillGenerator}`}
                                            style={{ width: `${powerDistribution.generator.pct}%` }}
                                        />
                                    </div>
                                    <div className={styles.distValue}>
                                        {powerDistribution.generator.kw > 0 ? `${powerDistribution.generator.kw}kW` : '—'}
                                        <span className={styles.distPct}>({powerDistribution.generator.pct}%)</span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Priority Breakdown */}
                        <div className={styles.chartCard}>
                            <div className={styles.chartHeader}>
                                <div className={styles.chartTitle}>
                                    <PieChart size={18} />
                                    Load Priority Breakdown
                                </div>
                                <span className={styles.chartBadge}>By kW</span>
                            </div>
                            <div className={styles.priorityBreakdown}>
                                <div className={styles.donutContainer}>
                                    <svg viewBox="0 0 120 120" className={styles.donutSvg}>
                                        <circle cx="60" cy="60" r="45" fill="none" stroke="#e5e7eb" strokeWidth="12" />
                                        <circle
                                            cx="60" cy="60" r="45"
                                            fill="none"
                                            stroke="#ef4444"
                                            strokeWidth="12"
                                            strokeDasharray={`${(priorityData.p1.pct / 100) * 283} 283`}
                                            strokeDashoffset="0"
                                            strokeLinecap="round"
                                            transform="rotate(-90 60 60)"
                                            className={styles.donutSegment}
                                        />
                                        <circle
                                            cx="60" cy="60" r="45"
                                            fill="none"
                                            stroke="#f59e0b"
                                            strokeWidth="12"
                                            strokeDasharray={`${(priorityData.p2.pct / 100) * 283} 283`}
                                            strokeDashoffset={`-${(priorityData.p1.pct / 100) * 283}`}
                                            strokeLinecap="round"
                                            transform="rotate(-90 60 60)"
                                            className={styles.donutSegment}
                                        />
                                        <circle
                                            cx="60" cy="60" r="45"
                                            fill="none"
                                            stroke="#3b82f6"
                                            strokeWidth="12"
                                            strokeDasharray={`${(priorityData.p3.pct / 100) * 283} 283`}
                                            strokeDashoffset={`-${((priorityData.p1.pct + priorityData.p2.pct) / 100) * 283}`}
                                            strokeLinecap="round"
                                            transform="rotate(-90 60 60)"
                                            className={styles.donutSegment}
                                        />
                                        <text x="60" y="55" textAnchor="middle" className={styles.donutCenter}>
                                            {allLoads.length}
                                        </text>
                                        <text x="60" y="72" textAnchor="middle" className={styles.donutLabel}>
                                            Loads
                                        </text>
                                    </svg>
                                </div>
                                <div className={styles.legend}>
                                    <div className={styles.legendItem}>
                                        <span className={`${styles.legendDot} ${styles.dotP1}`} />
                                        <span className={styles.legendLabel}>P1 (Critical)</span>
                                        <span className={styles.legendValue}>{priorityData.p1.kw.toFixed(1)}kW ({priorityData.p1.pct}%)</span>
                                    </div>
                                    <div className={styles.legendItem}>
                                        <span className={`${styles.legendDot} ${styles.dotP2}`} />
                                        <span className={styles.legendLabel}>P2 (Essential)</span>
                                        <span className={styles.legendValue}>{priorityData.p2.kw.toFixed(1)}kW ({priorityData.p2.pct}%)</span>
                                    </div>
                                    <div className={styles.legendItem}>
                                        <span className={`${styles.legendDot} ${styles.dotP3}`} />
                                        <span className={styles.legendLabel}>P3 (Non-Essential)</span>
                                        <span className={styles.legendValue}>{priorityData.p3.kw.toFixed(1)}kW ({priorityData.p3.pct}%)</span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Frequency Chart */}
                        
                    </div>

                    {/* System Status Summary */}
                    <div className={styles.summaryCard}>
                        <div className={styles.summaryHeader}>
                            <Activity size={18} />
                            <h3>System Status Summary</h3>
                        </div>
                        <div className={styles.summaryGrid}>
                            <div className={styles.summaryItem}>
                                <span className={styles.summaryLabel}>Power Source</span>
                                <span className={`${styles.summaryValue} ${isGridOnline ? styles.success : styles.danger}`}>
                                    {isGridOnline ? 'Grid Connected' : 'Grid Disconnected'}
                                </span>
                            </div>
                            <div className={styles.summaryItem}>
                                <span className={styles.summaryLabel}>Total Loads</span>
                                <span className={styles.summaryValue}>{allLoads.length}</span>
                            </div>
                            <div className={styles.summaryItem}>
                                <span className={styles.summaryLabel}>Active Loads</span>
                                <span className={styles.summaryValue}>
                                    {allLoads.filter(l => loadStatusMap.get(l.id) === 'Normal').length}
                                </span>
                            </div>
                            <div className={styles.summaryItem}>
                                <span className={styles.summaryLabel}>Shedded Loads</span>
                                <span className={`${styles.summaryValue} ${styles.danger}`}>
                                    {allLoads.filter(l => loadStatusMap.get(l.id) === 'Shedded').length}
                                </span>
                            </div>
                            <div className={styles.summaryItem}>
                                <span className={styles.summaryLabel}>Connection</span>
                                <span className={`${styles.summaryValue} ${connected ? styles.success : styles.danger}`}>
                                    {connected ? 'Connected' : 'Disconnected'}
                                </span>
                            </div>
                            <div className={styles.summaryItem}>
                                <span className={styles.summaryLabel}>Zones</span>
                                <span className={styles.summaryValue}>{zones.length}</span>
                            </div>
                        </div>
                    </div>
                </>
            )}
        </div>
    );
}