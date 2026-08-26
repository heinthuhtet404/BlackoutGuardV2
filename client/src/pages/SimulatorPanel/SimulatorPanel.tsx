import { useEffect, useRef, useState } from "react";
import { Navigate } from "react-router-dom";
import { useRole } from "../../auth/useRole";
import { post } from "../../api/apiClient";
import { useTelemetry } from "../../context/TelemetryContext";
import { useToast } from "../../components/ui/toastContext";
import styles from "./SimulatorPanel.module.css";

const FREQ_MIN = 45.0;
const FREQ_MAX = 55.0;
const LOAD_MIN = 0;
const LOAD_MAX = 200;
const DEBOUNCE_MS = 200;

let globalAutoInterval: ReturnType<typeof setInterval> | null = null;
let globalIsAutoSimulating = true;
const currentValuesRef = {
    frequency: 50.0,
    loadKw: 80,
    voltage: 230.0,
    generatorOn: true,
};

function startGlobalAutoSimulation() {
    if (globalAutoInterval) return;

    globalAutoInterval = setInterval(() => {
        if (!globalIsAutoSimulating) return;

        const freqDelta = (Math.random() - 0.5) * 0.12;
        let newFreq = Number((currentValuesRef.frequency + freqDelta).toFixed(2));
        if (newFreq < 48.8) newFreq = 49.6;
        if (newFreq > 51.2) newFreq = 50.4;

        const loadDelta = (Math.random() - 0.5) * 3.0;
        let newLoad = Number((currentValuesRef.loadKw + loadDelta).toFixed(1));
        if (newLoad < 40) newLoad = 65;
        if (newLoad > 180) newLoad = 140;

        const voltDelta = (Math.random() - 0.5) * 0.8;
        let newVolt = Number((currentValuesRef.voltage + voltDelta).toFixed(1));
        if (newVolt < 222) newVolt = 227;
        if (newVolt > 238) newVolt = 233;

        currentValuesRef.frequency = newFreq;
        currentValuesRef.loadKw = newLoad;
        currentValuesRef.voltage = newVolt;

        void post("/simulator/telemetry", {
            frequency: newFreq,
            voltage: newVolt,
            totalLoad: newLoad,
            totalLoadKw: newLoad,
            generatorOn: currentValuesRef.generatorOn,
        }).catch((err: unknown) => {
            console.warn("Auto-telemetry push failed:", err);
        });
    }, 1000);
}

export function SimulatorPanel() {
    const { is } = useRole();

    if (!is("Admin")) {
        return <Navigate to="/overview" replace />;
    }

    return <SimulatorPanelContent />;
}

function SimulatorPanelContent() {
    const { telemetry, connected } = useTelemetry();
    const { showToast } = useToast();

    const [frequency, setFrequency] = useState(currentValuesRef.frequency);
    const [loadKw, setLoadKw] = useState(currentValuesRef.loadKw);
    const [voltage, setVoltage] = useState(currentValuesRef.voltage);
    const [generatorOn, setGeneratorOn] = useState(currentValuesRef.generatorOn);
    const [injectingFault, setInjectingFault] = useState(false);

    const [isAutoSimulating, setIsAutoSimulating] = useState(globalIsAutoSimulating);
    const isInitialSynced = useRef(false);

    useEffect(() => {
        if (telemetry && !isInitialSynced.current) {
            setFrequency(telemetry.frequency);
            setLoadKw(telemetry.totalLoadKw ?? 80);
            if (telemetry.voltage !== undefined) setVoltage(telemetry.voltage);
            if (telemetry.generatorOn !== undefined) setGeneratorOn(telemetry.generatorOn);

            currentValuesRef.frequency = telemetry.frequency;
            currentValuesRef.loadKw = telemetry.totalLoadKw ?? 80;
            currentValuesRef.voltage = telemetry.voltage ?? 230.0;
            currentValuesRef.generatorOn = telemetry.generatorOn ?? true;

            isInitialSynced.current = true;
        }
    }, [telemetry]);

    useEffect(() => {
        if (telemetry && isAutoSimulating) {
            setFrequency(telemetry.frequency);
            setLoadKw(telemetry.totalLoadKw);
            if (telemetry.voltage !== undefined) setVoltage(telemetry.voltage);
        }
    }, [telemetry, isAutoSimulating]);

    useEffect(() => {
        startGlobalAutoSimulation();
    }, []);

    const toggleAutoSimulation = () => {
        const nextState = !isAutoSimulating;
        setIsAutoSimulating(nextState);
        globalIsAutoSimulating = nextState;
    };

    const freqTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const loadTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

    useEffect(() => {
        return () => {
            if (freqTimer.current) clearTimeout(freqTimer.current);
            if (loadTimer.current) clearTimeout(loadTimer.current);
        };
    }, []);

    const buildTelemetryPayload = (f = frequency, l = loadKw, v = voltage, g = generatorOn) => ({
        frequency: f,
        voltage: v,
        totalLoad: l,
        totalLoadKw: l,
        generatorOn: g,
    });

    const handleFrequencyChange = (value: number) => {
        setFrequency(value);
        currentValuesRef.frequency = value;
        if (freqTimer.current) clearTimeout(freqTimer.current);
        freqTimer.current = setTimeout(() => {
            void post("/simulator/telemetry", buildTelemetryPayload(value, loadKw, voltage, generatorOn)).catch(
                (err: unknown) => {
                    showToast(err instanceof Error ? err.message : "Failed to set frequency", "error");
                }
            );
        }, DEBOUNCE_MS);
    };

    const handleLoadChange = (value: number) => {
        setLoadKw(value);
        currentValuesRef.loadKw = value;
        if (loadTimer.current) clearTimeout(loadTimer.current);
        loadTimer.current = setTimeout(() => {
            void post("/simulator/telemetry", buildTelemetryPayload(frequency, value, voltage, generatorOn)).catch(
                (err: unknown) => {
                    showToast(err instanceof Error ? err.message : "Failed to set load", "error");
                }
            );
        }, DEBOUNCE_MS);
    };

    const handleGeneratorToggle = (value: boolean) => {
        setGeneratorOn(value);
        currentValuesRef.generatorOn = value;
        void post("/simulator/telemetry", buildTelemetryPayload(frequency, loadKw, voltage, value)).catch(
            (err: unknown) => {
                showToast(err instanceof Error ? err.message : "Failed to toggle generator", "error");
            }
        );
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
            <div className={styles.header}>
                <div>
                    <h1 className={styles.heading}>🎮 Simulator Panel</h1>
                    <p className={styles.subheading}>Control and monitor hardware emulation</p>
                </div>
                <div className={`${styles.statusBadge} ${connected ? styles.statusConnected : styles.statusDisconnected}`}>
                    <span className={styles.statusDot}></span>
                    {connected ? "Live Connected" : "Disconnected"}
                </div>
            </div>

            {/* Live Telemetry */}
            <section className={styles.card}>
                <div className={styles.cardHeader}>
                    <h2 className={styles.cardTitle}>📊 Live Telemetry</h2>
                    <span className={styles.cardBadge}>Real-time</span>
                </div>
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
                        <span className={`${styles.metricValue} ${telemetry?.generatorOn ? styles.valueOn : styles.valueOff}`} data-testid="telemetry-generator">
                            {telemetry ? (telemetry.generatorOn ? "ON" : "OFF") : "—"}
                        </span>
                    </div>
                </div>
            </section>

            {/* Controls */}
            <section className={styles.card}>
                <div className={styles.cardHeader}>
                    <h2 className={styles.cardTitle}>🎛️ Controls & Simulation</h2>
                    <span className={styles.cardBadge}>Admin</span>
                </div>

                <div className={styles.modeSection}>
                    <div className={styles.modeInfo}>
                        <span className={styles.modeIcon}>⚡</span>
                        <div>
                            <div className={styles.modeTitle}>Live Hardware Emulation</div>
                            <div className={styles.modeSubtext}>
                                {isAutoSimulating ? "Broadcasting live sensor data to Overview & System" : "Manual override control active"}
                            </div>
                        </div>
                    </div>
                    <button
                        type="button"
                        className={`${styles.toggle} ${isAutoSimulating ? styles.toggleOn : styles.toggleOff}`}
                        onClick={toggleAutoSimulation}
                        aria-pressed={isAutoSimulating}
                        data-testid="auto-simulation-toggle"
                    >
                        {isAutoSimulating ? "AUTO" : "MANUAL"}
                    </button>
                </div>

                {isAutoSimulating ? (
                    <div className={styles.autoActive}>
                        <span className={styles.autoIcon}>✨</span>
                        <div>
                            <strong>Hardware Sensor Emulation is Active.</strong>
                            <div className={styles.autoSubtext}>
                                Frequency & Grid Voltage are dynamically changing in real-time across Overview & Dashboard.
                            </div>
                        </div>
                    </div>
                ) : (
                    <>
                        <div className={styles.controlGroup}>
                            <div className={styles.controlHeader}>
                                <label className={styles.controlLabel}>Target Frequency</label>
                                <span className={styles.controlValue} data-testid="frequency-value">{frequency.toFixed(1)} Hz</span>
                            </div>
                            <input
                                type="range"
                                min={FREQ_MIN}
                                max={FREQ_MAX}
                                step={0.1}
                                value={frequency}
                                onChange={(e) => handleFrequencyChange(Number(e.target.value))}
                                className={styles.slider}
                                data-testid="frequency-slider"
                                style={{ '--slider-color': '#f87171' } as React.CSSProperties}
                            />
                            <div className={styles.sliderLabels}>
                                <span>{FREQ_MIN} Hz</span>
                                <span>{FREQ_MAX} Hz</span>
                            </div>
                        </div>

                        <div className={styles.controlGroup}>
                            <div className={styles.controlHeader}>
                                <label className={styles.controlLabel}>Target Load</label>
                                <span className={styles.controlValue} data-testid="load-value">{loadKw} kW</span>
                            </div>
                            <input
                                type="range"
                                min={LOAD_MIN}
                                max={LOAD_MAX}
                                step={1}
                                value={loadKw}
                                onChange={(e) => handleLoadChange(Number(e.target.value))}
                                className={styles.slider}
                                data-testid="load-slider"
                                style={{ '--slider-color': '#60a5fa' } as React.CSSProperties}
                            />
                            <div className={styles.sliderLabels}>
                                <span>{LOAD_MIN} kW</span>
                                <span>{LOAD_MAX} kW</span>
                            </div>
                        </div>
                    </>
                )}

                <div className={styles.generatorRow}>
                    <div className={styles.generatorInfo}>
                        <span className={styles.generatorIcon}>🔋</span>
                        <span className={styles.controlLabel}>Generator</span>
                    </div>
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
                    {injectingFault ? (
                        <>
                            <span className={styles.spinner}></span>
                            Injecting...
                        </>
                    ) : (
                        "⚠️ Inject Fault (frequency_drop)"
                    )}
                </button>
            </section>
        </div>
    );
}