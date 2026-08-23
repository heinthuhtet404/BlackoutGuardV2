import { useEffect, useRef, useState } from "react";
import { Navigate } from "react-router-dom";
import { useRole } from "../../auth/useRole";
import { post } from "../../api/apiClient";
import { useTelemetryHub } from "../../hooks/useTelemetryHub";
import { useToast } from "../../components/ui/toastContext";
import styles from "./SimulatorPanel.module.css";

const FREQ_MIN = 45.0;
const FREQ_MAX = 55.0;
const LOAD_MIN = 0;
const LOAD_MAX = 200;
const DEBOUNCE_MS = 200;

export function SimulatorPanel() {
    const { is } = useRole();

    if (!is("Admin")) {
        return <Navigate to="/overview" replace />;
    }

    return <SimulatorPanelContent />;
}

function SimulatorPanelContent() {
    const { telemetry, connected } = useTelemetryHub();
    const { showToast } = useToast();

    const [frequency, setFrequency] = useState(50.0);
    const [loadKw, setLoadKw] = useState(80);
    const [generatorOn, setGeneratorOn] = useState(true);
    const [injectingFault, setInjectingFault] = useState(false);

    const freqTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const loadTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

    useEffect(() => {
        return () => {
            if (freqTimer.current) clearTimeout(freqTimer.current);
            if (loadTimer.current) clearTimeout(loadTimer.current);
        };
    }, []);

    const handleFrequencyChange = (value: number) => {
        setFrequency(value);
        if (freqTimer.current) clearTimeout(freqTimer.current);
        freqTimer.current = setTimeout(() => {
            void post("/simulator/telemetry", { frequency: value, totalLoadKw: loadKw, generatorOn })
                .catch((err: unknown) => {
                    showToast(err instanceof Error ? err.message : "Failed to set frequency", "error");
                });
        }, DEBOUNCE_MS);
    };

    const handleLoadChange = (value: number) => {
        setLoadKw(value);
        if (loadTimer.current) clearTimeout(loadTimer.current);
        loadTimer.current = setTimeout(() => {
            void post("/simulator/telemetry", { frequency, totalLoadKw: value, generatorOn })
                .catch((err: unknown) => {
                    showToast(err instanceof Error ? err.message : "Failed to set load", "error");
                });
        }, DEBOUNCE_MS);
    };

    const handleGeneratorToggle = (value: boolean) => {
        setGeneratorOn(value);
        void post("/simulator/telemetry", { frequency, totalLoadKw: loadKw, generatorOn: value })
            .catch((err: unknown) => {
                showToast(err instanceof Error ? err.message : "Failed to toggle generator", "error");
            });
    };

    const handleInjectFault = () => {
        setInjectingFault(true);
        void post("/simulator/fault", { preset: "frequency_drop" })
            .then(() => showToast("Fault injected: frequency_drop"))
            .catch((err: unknown) =>
                showToast(err instanceof Error ? err.message : "Fault injection failed", "error")
            )
            .finally(() => setInjectingFault(false));
    };

    return (
        <div className={styles.page} data-testid="simulator-panel">
            <h1 className={styles.heading}>Simulator Panel</h1>

            <div className={styles.statusRow}>
                <span className={connected ? styles.connected : styles.disconnected}>
                    {connected ? "● Live (Connected)" : "○ Disconnected"}
                </span>
            </div>

            <section className={styles.card}>
                <h2 className={styles.cardTitle}>Live Telemetry (server-authoritative)</h2>
                <div className={styles.telemetryGrid}>
                    <div className={styles.metric}>
                        <span className={styles.metricLabel}>Frequency</span>
                        <span className={styles.metricValue} data-testid="telemetry-frequency">
                            {telemetry ? `${telemetry.frequency.toFixed(2)} Hz` : "—"}
                        </span>
                    </div>
                    <div className={styles.metric}>
                        <span className={styles.metricLabel}>Voltage</span>
                        <span className={styles.metricValue} data-testid="telemetry-voltage">
                            {telemetry ? `${telemetry.voltage.toFixed(1)} V` : "—"}
                        </span>
                    </div>
                    <div className={styles.metric}>
                        <span className={styles.metricLabel}>Load</span>
                        <span className={styles.metricValue} data-testid="telemetry-load">
                            {telemetry ? `${telemetry.totalLoadKw.toFixed(1)} kW` : "—"}
                        </span>
                    </div>
                    <div className={styles.metric}>
                        <span className={styles.metricLabel}>Generator</span>
                        <span className={styles.metricValue} data-testid="telemetry-generator">
                            {telemetry ? (telemetry.generatorOn ? "ON" : "OFF") : "—"}
                        </span>
                    </div>
                </div>
            </section>

            <section className={styles.card}>
                <h2 className={styles.cardTitle}>Controls</h2>

                <label className={styles.control}>
                    <span className={styles.controlLabel}>
                        Target Frequency: <strong data-testid="frequency-value">{frequency.toFixed(1)} Hz</strong>
                    </span>
                    <input
                        type="range"
                        min={FREQ_MIN}
                        max={FREQ_MAX}
                        step={0.1}
                        value={frequency}
                        onChange={(event) => handleFrequencyChange(Number(event.target.value))}
                        data-testid="frequency-slider"
                    />
                </label>

                <label className={styles.control}>
                    <span className={styles.controlLabel}>
                        Target Load: <strong data-testid="load-value">{loadKw} kW</strong>
                    </span>
                    <input
                        type="range"
                        min={LOAD_MIN}
                        max={LOAD_MAX}
                        step={1}
                        value={loadKw}
                        onChange={(event) => handleLoadChange(Number(event.target.value))}
                        data-testid="load-slider"
                    />
                </label>

                <div className={styles.toggleRow}>
                    <span className={styles.controlLabel}>Generator</span>
                    <button
                        type="button"
                        className={`${styles.toggle} ${generatorOn ? styles.toggleOn : styles.toggleOff}`}
                        onClick={() => handleGeneratorToggle(!generatorOn)}
                        aria-pressed={generatorOn}
                        data-testid="generator-toggle"
                    >
                        {generatorOn ? "ON" : "OFF"}
                    </button>
                </div>

                <button
                    type="button"
                    className={styles.faultButton}
                    onClick={handleInjectFault}
                    disabled={injectingFault}
                    data-testid="inject-fault"
                >
                    {injectingFault ? "Injecting…" : "Inject Fault (frequency_drop)"}
                </button>
            </section>
        </div>
    );
}