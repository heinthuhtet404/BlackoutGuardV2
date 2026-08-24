import { useState, useEffect } from "react";
import { useTelemetry, type RelayDecision } from "../context/TelemetryContext";
import styles from "./LiveOverviewPage.module.css";

interface AlarmLog {
    id: string;
    time: string;
    message: string;
    type: "warning" | "critical" | "success";
}

// Fixed Node Definitions mapped to Relay Addresses
const NODE_DEFINITIONS = [
    { address: 1, name: "ICU Ward", priority: "P1 (Critical)", icon: "🏥" },
    { address: 2, name: "Operation Theatre", priority: "P1 (Critical)", icon: "🩺" },
    { address: 3, name: "Main Lab", priority: "P2 (Essential)", icon: "🏢" },
    { address: 4, name: "General HVAC / AC", priority: "P3 (Non-Essential)", icon: "❄️" },
];

export function LiveOverviewPage() {
    const { telemetry, connected, latestDecision } = useTelemetry();

    // 1. History state for Frequency Line Chart (Max 20 data points)
    const [freqHistory, setFreqHistory] = useState<number[]>([]);

    // 2. Local State for Alarms
    const [alarms, setAlarms] = useState<AlarmLog[]>([]);

    // 3. Track frequency updates for line chart & under-frequency alarms
    useEffect(() => {
        if (!telemetry) return;

        // Push new frequency to history array
        setFreqHistory((prev) => [...prev.slice(-19), telemetry.frequency]);

        // Check for Under-Frequency Alarm
        if (telemetry.frequency < 49.5) {
            const timeStr = new Date().toLocaleTimeString();
            setAlarms((prev) => {
                if (prev[0]?.message.includes("Low Frequency Warning")) return prev;
                return [
                    {
                        id: Date.now().toString(),
                        time: timeStr,
                        message: `⚠️ Low Frequency Warning: ${telemetry.frequency.toFixed(2)} Hz`,
                        type: "warning",
                    },
                    ...prev.slice(0, 4),
                ];
            });
        }
    }, [telemetry]);

    // 4. Track Relay Decisions for Alarms & Node Status
    useEffect(() => {
        if (!latestDecision) return;

        const timeStr = new Date().toLocaleTimeString();
        const newAlarm: AlarmLog = {
            id: Date.now().toString(),
            time: timeStr,
            message: `⚡ Load Shedding Executed: ${latestDecision.rationale}`,
            type: "critical",
        };

        setAlarms((prev) => [newAlarm, ...prev.slice(0, 4)]);
    }, [latestDecision]);

    // Calculations
    const isUnderFrequency = telemetry ? telemetry.frequency < 49.5 : false;

    // System Mode Banner status logic
    const getSystemMode = () => {
        if (!connected) return { text: "SYSTEM OFFLINE", className: styles.modeOffline };
        if (isUnderFrequency) return { text: "BLACKOUT PREVENTIVE MODE", className: styles.modeDanger };
        if (telemetry?.generatorOn) return { text: "GENERATOR BACKUP MODE", className: styles.modeWarning };
        return { text: "GRID NORMAL MODE", className: styles.modeSuccess };
    };

    const systemMode = getSystemMode();

    // Priority Load Breakdown (P1: 50%, P2: 30%, P3: 20% estimated distribution)
    const totalLoad = telemetry?.totalLoadKw ?? 0;
    const p1Load = (totalLoad * 0.5).toFixed(1);
    const p2Load = (totalLoad * 0.3).toFixed(1);
    const p3Load = (totalLoad * 0.2).toFixed(1);

    // SVG Line Chart Coordinate Generator
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
                {/* Background Grid Lines */}
                <line x1="0" y1="25" x2={width} y2="25" stroke="#334155" strokeDasharray="2,2" />
                <line x1="0" y1="50" x2={width} y2="50" stroke="#475569" strokeDasharray="4,4" />
                <line x1="0" y1="75" x2={width} y2="75" stroke="#334155" strokeDasharray="2,2" />

                {/* Dynamic Line */}
                <polyline fill="none" stroke={isUnderFrequency ? "#ef4444" : "#10b981"} strokeWidth="2.5" points={points} />
            </svg>
        );
    };

    // Node Relay Status lookup
    const getNodeStatus = (address: number) => {
        if (!latestDecision?.relayDecisions) return "🟢 Normal";
        const decision = latestDecision.relayDecisions.find((r: RelayDecision) => r.relayAddress === address);
        if (decision) {
            return decision.energize ? "🟢 Normal" : "🟡 Shedded";
        }
        return "🟢 Normal";
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

            {/* 1. Telemetry Cards Grid */}
            <div className={styles.grid}>
                <div className={`${styles.card} ${isUnderFrequency ? styles.cardDanger : ""}`}>
                    <h3 className={styles.cardLabel}>⚡ Grid Voltage</h3>
                    <p className={`${styles.cardValue} ${styles.valueBlue}`}>
                        {telemetry ? `${telemetry.voltage.toFixed(1)} V` : "—"}
                    </p>
                </div>

                <div className={styles.card}>
                    <h3 className={styles.cardLabel}>⚡ Total Active Load</h3>
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

            {/* 2. System Status Map */}
            <div className={styles.sectionCard}>
                <h2 className={styles.sectionTitle}>🗺️ System Status Map (Zones & Nodes)</h2>
                <div className={styles.nodeGrid}>
                    {NODE_DEFINITIONS.map((node) => {
                        const status = getNodeStatus(node.address);
                        const isShedded = status.includes("Shedded");
                        return (
                            <div
                                key={node.address}
                                className={`${styles.nodeCard} ${isShedded ? styles.nodeShedded : styles.nodeNormal}`}
                            >
                                <div className={styles.nodeIcon}>{node.icon}</div>
                                <div className={styles.nodeName}>{node.name}</div>
                                <div className={styles.nodePriority}>{node.priority}</div>
                                <div className={styles.nodeBadge}>{status}</div>
                            </div>
                        );
                    })}
                </div>
            </div>

            {/* 3. Charts Row */}
            <div className={styles.chartsRow}>
                {/* Frequency Line Chart */}
                <div className={styles.chartCard}>
                    <h3 className={styles.chartTitle}>📈 Real-Time Frequency Chart (Hz)</h3>
                    <div className={styles.chartContainer}>{renderFrequencyChart()}</div>
                    <div className={styles.chartFooter}>
                        <span>Min: 48.0 Hz</span>
                        <span>Target: 50.0 Hz</span>
                        <span>Max: 52.0 Hz</span>
                    </div>
                </div>

                {/* Load Distribution */}
                <div className={styles.chartCard}>
                    <h3 className={styles.chartTitle}>⚡ Load Distribution Breakdown</h3>
                    <div className={styles.loadBreakdownContainer}>
                        <div className={styles.loadBarRow}>
                            <span>P1 (Critical)</span>
                            <span>{p1Load} kW (50%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={styles.progressFillP1} style={{ width: "50%" }}></div>
                        </div>

                        <div className={styles.loadBarRow}>
                            <span>P2 (Essential)</span>
                            <span>{p2Load} kW (30%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={styles.progressFillP2} style={{ width: "30%" }}></div>
                        </div>

                        <div className={styles.loadBarRow}>
                            <span>P3 (Non-Essential)</span>
                            <span>{p3Load} kW (20%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={styles.progressFillP3} style={{ width: "20%" }}></div>
                        </div>
                    </div>
                </div>
            </div>

            {/* 4. Recent Alarms Log */}
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