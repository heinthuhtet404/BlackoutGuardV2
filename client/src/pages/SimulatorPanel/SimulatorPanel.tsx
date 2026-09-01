import { useEffect, useRef, useState } from "react";
import { Navigate } from "react-router-dom";
import { useRole } from "../../auth/useRole";
import { post, get } from "../../api/apiClient";
import { useTelemetry } from "../../context/TelemetryContext";
import { useToast } from "../../components/ui/toastContext";
import {
    Server,
    Activity,
    Zap,
    Gauge,
    Power,
    Wifi,
    WifiOff,
    AlertTriangle,
    Loader2,
    Play,
    Pause,
    Settings2,
    Battery,
    TrendingUp,
    Shield,
    Signal,
    Sun,
    PowerOff,
    Radio,
} from "lucide-react";
import styles from "./SimulatorPanel.module.css";

const FREQ_MIN = 45.0;
const FREQ_MAX = 55.0;

const CAP_MIN = 0;
const CAP_MAX = 2000;

const DEBOUNCE_MS = 300;

let globalAutoInterval: ReturnType<typeof setInterval> | null = null;
let globalIsAutoSimulating = true;

let autoSimAbortController: AbortController | null = null;
let freqAbortController: AbortController | null = null;
let solarAbortController: AbortController | null = null;
let genCapAbortController: AbortController | null = null;

const currentValuesRef = {
    frequency: 50.0,
    loadKw: 80,
    voltage: 230.0,
    generatorOn: true,
    gridOnline: true,
    solarCapacityKw: 50,
    generatorCapacityKw: 100,
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

        if (autoSimAbortController) {
            autoSimAbortController.abort();
        }
        autoSimAbortController = new AbortController();

        void post("/simulator/telemetry", {
            frequency: newFreq,
            voltage: newVolt,
            totalLoad: newLoad,
            totalLoadKw: newLoad,
            generatorOn: currentValuesRef.generatorOn,
            gridOnline: currentValuesRef.gridOnline,
            solarCapacityKw: currentValuesRef.solarCapacityKw,
            generatorCapacityKw: currentValuesRef.generatorCapacityKw,
        }, { signal: autoSimAbortController.signal }).catch((err: unknown) => {
            if (err instanceof Error && err.name === "AbortError") return;
        });
    }, 2000);
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

    const [isLoadingConfig, setIsLoadingConfig] = useState(true);

    const [frequency, setFrequency] = useState(currentValuesRef.frequency);
    const [loadKw] = useState(currentValuesRef.loadKw);
    const [voltage] = useState(currentValuesRef.voltage);
    const [generatorOn] = useState(currentValuesRef.generatorOn);

    // Main Grid, Solar Capacity, Generator Capacity States
    const [gridOnline, setGridOnline] = useState<boolean>(currentValuesRef.gridOnline);
    const [solarCapacityKw, setSolarCapacityKw] = useState<number>(currentValuesRef.solarCapacityKw);
    const [generatorCapacityKw, setGeneratorCapacityKw] = useState<number>(currentValuesRef.generatorCapacityKw);

    const [injectingFault, setInjectingFault] = useState(false);
    const [isAutoSimulating, setIsAutoSimulating] = useState(globalIsAutoSimulating);
    const isInitialSynced = useRef(false);

    // Refresh လုပ်ရင် DB ကနေ Main Grid Power, Solar Capacity, Generator Capacity တို့ကို ပြန်ဆွဲယူမည့် Effect
    useEffect(() => {
        let isMounted = true;

        const fetchInitialConfig = async () => {
            setIsLoadingConfig(true);
            try {
                // DB Config Endpoint ကို ခေါ်ယူခြင်း
                const config = await get<{
                    gridOnline?: boolean;
                    solarCapacityKw?: number;
                    generatorCapacityKw?: number;
                }>("/simulator/config").catch(() => null);

                if (config && isMounted) {
                    if (typeof config.gridOnline === "boolean") {
                        setGridOnline(config.gridOnline);
                        currentValuesRef.gridOnline = config.gridOnline;
                    }
                    if (typeof config.solarCapacityKw === "number") {
                        setSolarCapacityKw(config.solarCapacityKw);
                        currentValuesRef.solarCapacityKw = config.solarCapacityKw;
                    }
                    if (typeof config.generatorCapacityKw === "number") {
                        setGeneratorCapacityKw(config.generatorCapacityKw);
                        currentValuesRef.generatorCapacityKw = config.generatorCapacityKw;
                    }
                }
            } catch (err: unknown) {
                console.warn("DB fetch fallback:", err);
            } finally {
                if (isMounted) {
                    setIsLoadingConfig(false);
                }
            }
        };

        void fetchInitialConfig();

        return () => {
            isMounted = false;
        };
    }, []);

    useEffect(() => {
        if (telemetry && !isInitialSynced.current) {
            setFrequency(telemetry.frequency);
            currentValuesRef.frequency = telemetry.frequency;

            if (telemetry.gridOnline !== undefined) {
                setGridOnline(telemetry.gridOnline);
                currentValuesRef.gridOnline = telemetry.gridOnline;
            }
            if (telemetry.solarCapacityKw !== undefined) {
                setSolarCapacityKw(telemetry.solarCapacityKw);
                currentValuesRef.solarCapacityKw = telemetry.solarCapacityKw;
            }
            if (telemetry.generatorCapacityKw !== undefined) {
                setGeneratorCapacityKw(telemetry.generatorCapacityKw);
                currentValuesRef.generatorCapacityKw = telemetry.generatorCapacityKw;
            }

            isInitialSynced.current = true;
        }
    }, [telemetry]);

    useEffect(() => {
        if (telemetry && isAutoSimulating) {
            setFrequency(telemetry.frequency);
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
    const solarTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
    const genCapTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

    useEffect(() => {
        return () => {
            if (freqTimer.current) clearTimeout(freqTimer.current);
            if (solarTimer.current) clearTimeout(solarTimer.current);
            if (genCapTimer.current) clearTimeout(genCapTimer.current);
            if (freqAbortController) freqAbortController.abort();
            if (solarAbortController) solarAbortController.abort();
            if (genCapAbortController) genCapAbortController.abort();
        };
    }, []);

    // DB သို့ အချက်အလက်များ သိမ်းဆည်းသည့် Helper
    const updateDatabaseConfig = async (configPayload: Record<string, unknown>, signal?: AbortSignal) => {
        try {
            await post("/simulator/config", configPayload, { signal });
            showToast("DB Updated Successfully", "success");
        } catch (err: unknown) {
            if (err instanceof Error && err.name === "AbortError") return;
            showToast("Failed to update Database", "error");
        }
    };

    const handleFrequencyChange = (value: number) => {
        setFrequency(value);
        currentValuesRef.frequency = value;

        if (freqTimer.current) clearTimeout(freqTimer.current);
        if (freqAbortController) freqAbortController.abort();

        freqTimer.current = setTimeout(() => {
            freqAbortController = new AbortController();
            void post("/simulator/telemetry", {
                frequency: value,
                voltage,
                totalLoad: loadKw,
                totalLoadKw: loadKw,
                generatorOn,
                gridOnline,
                solarCapacityKw,
                generatorCapacityKw,
            }, { signal: freqAbortController.signal }).catch(() => { });
        }, DEBOUNCE_MS);
    };

    // 1. Main Grid Power Toggle -> Update DB
    const handleGridToggle = (value: boolean) => {
        setGridOnline(value);
        currentValuesRef.gridOnline = value;

        void updateDatabaseConfig({
            gridOnline: value,
            solarCapacityKw,
            generatorCapacityKw,
        });
    };

    // 2. Solar Capacity Change -> Update DB
    const handleSolarCapacityChange = (value: number) => {
        setSolarCapacityKw(value);
        currentValuesRef.solarCapacityKw = value;

        if (solarTimer.current) clearTimeout(solarTimer.current);
        if (solarAbortController) solarAbortController.abort();

        solarTimer.current = setTimeout(() => {
            solarAbortController = new AbortController();
            void updateDatabaseConfig({
                gridOnline,
                solarCapacityKw: value,
                generatorCapacityKw,
            }, solarAbortController.signal);
        }, DEBOUNCE_MS);
    };

    // 3. Generator Capacity Change -> Update DB
    const handleGeneratorCapacityChange = (value: number) => {
        setGeneratorCapacityKw(value);
        currentValuesRef.generatorCapacityKw = value;

        if (genCapTimer.current) clearTimeout(genCapTimer.current);
        if (genCapAbortController) genCapAbortController.abort();

        genCapTimer.current = setTimeout(() => {
            genCapAbortController = new AbortController();
            void updateDatabaseConfig({
                gridOnline,
                solarCapacityKw,
                generatorCapacityKw: value,
            }, genCapAbortController.signal);
        }, DEBOUNCE_MS);
    };

    const handleInjectFault = () => {
        setInjectingFault(true);
        void post("/simulator/fault", { preset: "frequency_drop" })
            .then(() => showToast("Fault injected: frequency_drop"))
            .catch(() => showToast("Fault injection failed", "error"))
            .finally(() => setInjectingFault(false));
    };

    if (isLoadingConfig) {
        return (
            <div className={styles.page} style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "400px" }}>
                <div style={{ textAlign: "center", color: "var(--text-muted, #888)" }}>
                    <Loader2 size={36} className={styles.spinning} style={{ marginBottom: "1rem" }} />
                    <p>Loading Simulator Settings...</p>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.page} data-testid="simulator-panel">
            {/* Header */}
            <div className={styles.header}>
                <div className={styles.headerLeft}>
                    <div className={styles.headerIconWrapper}>
                        <Server size={28} className={styles.headerIcon} />
                    </div>
                    <div>
                        <h1 className={styles.heading}>Simulator Panel</h1>
                        <p className={styles.subheading}>Control and monitor hardware emulation</p>
                    </div>
                </div>
                <div className={`${styles.statusBadge} ${connected ? styles.statusConnected : styles.statusDisconnected}`}>
                    {connected ? <Wifi size={14} /> : <WifiOff size={14} />}
                    <span className={styles.statusDot}></span>
                    {connected ? "Live Connected" : "Disconnected"}
                </div>
            </div>

            {/* Live Telemetry */}
            <section className={styles.card}>
                <div className={styles.cardHeader}>
                    <div className={styles.cardHeaderLeft}>
                        <Activity size={18} className={styles.cardIcon} />
                        <h2 className={styles.cardTitle}>Live Telemetry</h2>
                    </div>
                    <span className={styles.cardBadge}>
                        <Radio size={12} />
                        Real-time
                    </span>
                </div>
                <div className={styles.telemetryGrid}>
                    <div className={styles.metric}>
                        <span className={styles.metricLabel}>
                            <TrendingUp size={12} />
                            Frequency
                        </span>
                        <span className={styles.metricValue} data-testid="telemetry-frequency">
                            {telemetry ? `${telemetry.frequency.toFixed(2)} Hz` : "—"}
                        </span>
                    </div>
                    <div className={styles.metric}>
                        <span className={styles.metricLabel}>
                            <Zap size={12} />
                            Voltage
                        </span>
                        <span className={styles.metricValue} data-testid="telemetry-voltage">
                            {telemetry ? `${telemetry.voltage.toFixed(1)} V` : "—"}
                        </span>
                    </div>
                    <div className={styles.metric}>
                        <span className={styles.metricLabel}>
                            <Gauge size={12} />
                            Load
                        </span>
                        <span className={styles.metricValue} data-testid="telemetry-load">
                            {telemetry ? `${telemetry.totalLoadKw.toFixed(1)} kW` : "—"}
                        </span>
                    </div>
                    <div className={styles.metric}>
                        <span className={styles.metricLabel}>
                            <Power size={12} />
                            Grid Status
                        </span>
                        <span className={`${styles.metricValue} ${gridOnline ? styles.valueOn : styles.valueOff}`}>
                            {gridOnline ? "ONLINE" : "OFFLINE"}
                        </span>
                    </div>
                </div>
            </section>

            {/* Controls */}
            <section className={styles.card}>
                <div className={styles.cardHeader}>
                    <div className={styles.cardHeaderLeft}>
                        <Settings2 size={18} className={styles.cardIcon} />
                        <h2 className={styles.cardTitle}>Controls & Simulation</h2>
                    </div>
                    <span className={styles.cardBadge}>
                        <Shield size={12} />
                        Admin
                    </span>
                </div>

                <div className={styles.modeSection}>
                    <div className={styles.modeInfo}>
                        <div className={styles.modeIconWrapper}>
                            {isAutoSimulating ? <Play size={20} /> : <Pause size={20} />}
                        </div>
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
                        {isAutoSimulating ? (
                            <>
                                <Play size={14} />
                                AUTO
                            </>
                        ) : (
                            <>
                                <Pause size={14} />
                                MANUAL
                            </>
                        )}
                    </button>
                </div>

                {/* Main Grid Power Switch */}
                <div className={styles.generatorRow} style={{ marginTop: "1rem", marginBottom: "1rem" }}>
                    <div className={styles.generatorInfo}>
                        <div className={styles.generatorIconWrapper}>
                            {gridOnline ? <Zap size={18} /> : <PowerOff size={18} />}
                        </div>
                        <span className={styles.controlLabel}>Main Grid Power</span>
                        <span className={`${styles.generatorStatus} ${gridOnline ? styles.statusOn : styles.statusOff}`}>
                            {gridOnline ? "CONNECTED" : "BLACKOUT"}
                        </span>
                    </div>
                    <button
                        type="button"
                        className={`${styles.toggle} ${gridOnline ? styles.toggleOn : styles.toggleOff}`}
                        onClick={() => handleGridToggle(!gridOnline)}
                        aria-pressed={gridOnline}
                        data-testid="grid-toggle"
                    >
                        {gridOnline ? "GRID ON" : "GRID OFF"}
                    </button>
                </div>

                {isAutoSimulating ? (
                    <div className={styles.autoActive}>
                        <div className={styles.autoIconWrapper}>
                            <Signal size={20} />
                        </div>
                        <div>
                            <strong>Hardware Sensor Emulation is Active.</strong>
                            <div className={styles.autoSubtext}>
                                Frequency & Grid Voltage are dynamically changing in real-time across Overview & Dashboard.
                            </div>
                        </div>
                    </div>
                ) : (
                    <>
                        {/* Target Frequency Slider */}
                        <div className={styles.controlGroup}>
                            <div className={styles.controlHeader}>
                                <label className={styles.controlLabel}>
                                    <TrendingUp size={14} className={styles.controlIcon} />
                                    Target Frequency
                                </label>
                                <span className={styles.controlValue} data-testid="frequency-value">{frequency.toFixed(1)} Hz</span>
                            </div>
                            <input
                                type="range"
                                min={FREQ_MIN}
                                max={FREQ_MAX}
                                step={0.1}
                                value={frequency}
                                onChange={(e) => handleFrequencyChange(Number(e.target.value))}
                                className={`${styles.slider} ${styles.sliderFreq}`}
                                data-testid="frequency-slider"
                            />
                            <div className={styles.sliderLabels}>
                                <span>{FREQ_MIN} Hz</span>
                                <span className={styles.sliderTarget}>Target: 50.0 Hz</span>
                                <span>{FREQ_MAX} Hz</span>
                            </div>
                        </div>

                        {/* Solar Power Capacity Slider */}
                        <div className={styles.controlGroup}>
                            <div className={styles.controlHeader}>
                                <label className={styles.controlLabel}>
                                    <Sun size={14} className={styles.controlIcon} />
                                    Solar Capacity (Power)
                                </label>
                                <span className={styles.controlValue} data-testid="solar-value">{solarCapacityKw} kW</span>
                            </div>
                            <input
                                type="range"
                                min={CAP_MIN}
                                max={CAP_MAX}
                                step={1}
                                value={solarCapacityKw}
                                onChange={(e) => handleSolarCapacityChange(Number(e.target.value))}
                                className={`${styles.slider} ${styles.sliderLoad}`}
                                data-testid="solar-slider"
                            />
                            <div className={styles.sliderLabels}>
                                <span>{CAP_MIN} kW</span>
                                <span>{CAP_MAX} kW</span>
                            </div>
                        </div>

                        {/* Generator Capacity Slider */}
                        <div className={styles.controlGroup}>
                            <div className={styles.controlHeader}>
                                <label className={styles.controlLabel}>
                                    <Battery size={14} className={styles.controlIcon} />
                                    Generator Capacity (Power)
                                </label>
                                <span className={styles.controlValue} data-testid="gen-capacity-value">{generatorCapacityKw} kW</span>
                            </div>
                            <input
                                type="range"
                                min={CAP_MIN}
                                max={CAP_MAX}
                                step={1}
                                value={generatorCapacityKw}
                                onChange={(e) => handleGeneratorCapacityChange(Number(e.target.value))}
                                className={`${styles.slider} ${styles.sliderLoad}`}
                                data-testid="gen-capacity-slider"
                            />
                            <div className={styles.sliderLabels}>
                                <span>{CAP_MIN} kW</span>
                                <span>{CAP_MAX} kW</span>
                            </div>
                        </div>
                    </>
                )}

                {/* Inject Fault Button */}
                <button
                    type="button"
                    className={styles.faultButton}
                    onClick={handleInjectFault}
                    disabled={injectingFault}
                    data-testid="inject-fault"
                >
                    {injectingFault ? (
                        <>
                            <Loader2 size={18} className={styles.spinning} />
                            Injecting...
                        </>
                    ) : (
                        <>
                            <AlertTriangle size={18} />
                            Inject Fault (frequency_drop)
                        </>
                    )}
                </button>
            </section>
        </div>
    );
}