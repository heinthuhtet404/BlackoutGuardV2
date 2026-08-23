import { useTelemetry } from "../context/TelemetryContext";
import styles from "./LiveOverviewPage.module.css";

export function LiveOverviewPage() {
    const { telemetry, connected } = useTelemetry();

    const isUnderFrequency = telemetry ? telemetry.frequency < 49.5 : false;

    return (
        <div className={styles.page}>
            <h1 className={styles.heading}>Live Overview</h1>

            <div className={styles.statusRow}>
                <span
                    className={connected ? styles.statusConnected : styles.statusDisconnected}
                >
                    {connected ? "● Live Stream Connected" : "○ Offline / Reconnecting..."}
                </span>
            </div>

            <div className={styles.grid}>
                <div className={`${styles.card} ${isUnderFrequency ? styles.cardDanger : ""}`}>
                    <h3 className={styles.cardLabel}>Frequency</h3>
                    <p className={`${styles.cardValue} ${isUnderFrequency ? styles.valueDanger : styles.valueSuccess}`}>
                        {telemetry ? `${telemetry.frequency.toFixed(2)} Hz` : "—"}
                    </p>
                    {isUnderFrequency && (
                        <span className={styles.warning}>⚠️ Under-Frequency Threshold</span>
                    )}
                </div>

                <div className={styles.card}>
                    <h3 className={styles.cardLabel}>Voltage</h3>
                    <p className={`${styles.cardValue} ${styles.valueBlue}`}>
                        {telemetry ? `${telemetry.voltage.toFixed(1)} V` : "—"}
                    </p>
                </div>

                <div className={styles.card}>
                    <h3 className={styles.cardLabel}>Total Active Load</h3>
                    <p className={`${styles.cardValue} ${styles.valueAmber}`}>
                        {telemetry ? `${telemetry.totalLoadKw.toFixed(1)} kW` : "—"}
                    </p>
                </div>

                <div className={styles.card}>
                    <h3 className={styles.cardLabel}>Generator Status</h3>
                    <p
                        className={`${styles.cardValue} ${telemetry?.generatorOn ? styles.valueSuccess : styles.valueMuted
                            }`}
                    >
                        {telemetry ? (telemetry.generatorOn ? "ON" : "OFF") : "—"}
                    </p>
                </div>
            </div>
        </div>
    );
}