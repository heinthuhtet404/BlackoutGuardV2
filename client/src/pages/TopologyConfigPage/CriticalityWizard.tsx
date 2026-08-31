import { useState, type ReactNode } from "react";
import { useCriticality } from "../../hooks/useCriticality";
import { Slider } from "../../components/ui/Slider";
import { Badge } from "../../components/ui/Badge";
import {
    Shield,
    ShieldAlert,
    ShieldCheck,
    AlertTriangle,
    Sparkles,
    Brain,
    Settings2,
    Zap,
    HelpCircle,
} from "lucide-react";
import styles from "./CriticalityWizard.module.css";

export type PriorityMode = "auto" | "manual";
export type PriorityLevel = "P1" | "P2" | "P3";

export interface CriticalityInputs {
    safety: number;
    dataLoss: number;
    operational: number;
    comfort: number;
}

// 🔽 Live Client-side Score Calculation Function
export function calculateCriticalityScore(inputs: CriticalityInputs): {
    score: number;
    priority: PriorityLevel;
} {
    const { safety, dataLoss, operational } = inputs;
    const rawScore = safety * 0.5 + dataLoss * 0.3 + operational * 0.2;
    const score = Math.round(rawScore * 10);

    let priority: PriorityLevel = "P3";
    if (score >= 75) {
        priority = "P1";
    } else if (score >= 45) {
        priority = "P2";
    } else {
        priority = "P3";
    }

    return { score, priority };
}

function getPriorityIcon(priority: PriorityLevel): ReactNode {
    switch (priority) {
        case "P1":
            return <ShieldAlert size={16} />;
        case "P2":
            return <Shield size={16} />;
        case "P3":
            return <ShieldCheck size={16} />;
        default:
            return null;
    }
}

function getPriorityLabel(priority: PriorityLevel): string {
    switch (priority) {
        case "P1":
            return "Critical";
        case "P2":
            return "Essential";
        case "P3":
            return "Non-Essential";
        default:
            return "";
    }
}

interface CriticalityWizardProps {
    loadId?: string;
    onManualPriorityChange?: (priority: PriorityLevel | null) => void;
    safety?: number;
    onSafetyChange?: (value: number) => void;
    dataLoss?: number;
    onDataLossChange?: (value: number) => void;
    operational?: number;
    onOperationalChange?: (value: number) => void;
    comfort?: number;
    onComfortChange?: (value: number) => void;
}

export function CriticalityWizard({
    loadId,
    onManualPriorityChange,
    safety = 5,
    onSafetyChange,
    dataLoss = 5,
    onDataLossChange,
    operational = 5,
    onOperationalChange,
    comfort = 5,
    onComfortChange,
}: CriticalityWizardProps): ReactNode {
    const [mode, setMode] = useState<PriorityMode>("auto");
    const [q1, setQ1] = useState(safety);
    const [q2, setQ2] = useState(dataLoss);
    const [q3, setQ3] = useState(operational);
    const [q4, setQ4] = useState(comfort);
    const [manualPriority, setManualPriority] = useState<PriorityLevel>("P2");

    const { schedule, response, error } = useCriticality(loadId ?? "");

    const isManual = mode === "manual";
    const canScore = loadId !== undefined && loadId !== "";

    // 🔽 Client-side Real-time Calculated Score & Priority
    const localCalc = calculateCriticalityScore({
        safety: q1,
        dataLoss: q2,
        operational: q3,
        comfort: q4,
    });

    const handleModeChange = (nextMode: PriorityMode) => {
        setMode(nextMode);
        if (nextMode === "manual") {
            onManualPriorityChange?.(manualPriority);
        } else {
            onManualPriorityChange?.(null);
        }
    };

    const handleManualPriorityChange = (priority: PriorityLevel) => {
        setManualPriority(priority);
        onManualPriorityChange?.(priority);
    };

    const priorityLabel = getPriorityLabel(localCalc.priority);
    const priorityIcon = getPriorityIcon(localCalc.priority);

    return (
        <div className={styles.wizard} data-testid="criticality-wizard">
            {/* Header */}
            <div className={styles.wizardHeader}>
                <div className={styles.wizardHeaderLeft}>
                    <div className={styles.wizardIconWrapper}>
                        <Brain size={18} />
                    </div>
                    <span className={styles.wizardTitle}>Criticality Assessment</span>
                </div>
                {!isManual && (
                    <div className={styles.liveBadge}>
                        <Sparkles size={12} />
                        Live
                    </div>
                )}
            </div>

            {/* Mode Toggle */}
            <div className={styles.modeToggle} role="radiogroup" aria-label="Priority mode">
                <label className={mode === "auto" ? styles.modeActive : styles.mode}>
                    <input
                        type="radio"
                        name="priority-mode"
                        value="auto"
                        checked={mode === "auto"}
                        onChange={() => handleModeChange("auto")}
                        data-testid="mode-auto"
                    />
                    <Sparkles size={14} />
                    Auto-Assign
                </label>
                <label className={mode === "manual" ? styles.modeActive : styles.mode}>
                    <input
                        type="radio"
                        name="priority-mode"
                        value="manual"
                        checked={mode === "manual"}
                        onChange={() => handleModeChange("manual")}
                        data-testid="mode-manual"
                    />
                    <Settings2 size={14} />
                    Manual
                </label>
            </div>

            {/* Sliders with color classes */}
            <div className="slider-q1">
                <Slider
                    label="Q1: Safety Risk (0.5)"
                    value={q1}
                    disabled={isManual}
                    onChange={(event) => {
                        const value = Number(event.target.value);
                        setQ1(value);
                        onSafetyChange?.(value);
                        if (canScore) schedule({ q1: value, q2, q3, q4 });
                    }}
                    data-testid="slider-q1"
                />
            </div>

            <div className="slider-q2">
                <Slider
                    label="Q2: Data/Financial Risk (0.3)"
                    value={q2}
                    disabled={isManual}
                    onChange={(event) => {
                        const value = Number(event.target.value);
                        setQ2(value);
                        onDataLossChange?.(value);
                        if (canScore) schedule({ q1, q2: value, q3, q4 });
                    }}
                    data-testid="slider-q2"
                />
            </div>

            <div className="slider-q3">
                <Slider
                    label="Q3: Operational Impact (0.2)"
                    value={q3}
                    disabled={isManual}
                    onChange={(event) => {
                        const value = Number(event.target.value);
                        setQ3(value);
                        onOperationalChange?.(value);
                        if (canScore) schedule({ q1, q2, q3: value, q4 });
                    }}
                    data-testid="slider-q3"
                />
            </div>

            <div className="slider-q4">
                <Slider
                    label="Q4: Comfort (display only)"
                    value={q4}
                    disabled={isManual}
                    onChange={(event) => {
                        const value = Number(event.target.value);
                        setQ4(value);
                        onComfortChange?.(value);
                    }}
                    data-testid="slider-q4"
                />
            </div>

            {/* Manual Dropdown */}
            {isManual && (
                <div className={styles.manualDropdown} data-testid="manual-priority-dropdown">
                    <label className={styles.label} htmlFor="manual-priority">
                        <Zap size={14} className={styles.dropdownIcon} />
                        Direct priority assignment
                    </label>
                    <select
                        id="manual-priority"
                        value={manualPriority}
                        onChange={(event) =>
                            handleManualPriorityChange(event.target.value as PriorityLevel)
                        }
                        data-testid="manual-priority-select"
                    >
                        <option value="P1">🔴 P1 - Critical</option>
                        <option value="P2">🟡 P2 - Essential</option>
                        <option value="P3">🟢 P3 - Non-Essential</option>
                    </select>
                </div>
            )}

            {/* Priority Row */}
            <div className={styles.priorityRow}>
                <span className={styles.label}>
                    <span className={styles.priorityLabelIcon}>
                        {isManual ? getPriorityIcon(manualPriority) : priorityIcon}
                    </span>
                    Priority
                </span>
                <div className={styles.priorityDisplay}>
                    {isManual ? (
                        <Badge priority={manualPriority}>
                            {getPriorityIcon(manualPriority)}
                            {manualPriority} - {getPriorityLabel(manualPriority)}
                        </Badge>
                    ) : (
                        <Badge priority={response?.priority ?? localCalc.priority}>
                            {response?.priority ? (
                                <>
                                    {getPriorityIcon(response.priority as PriorityLevel)}
                                    {response.priority} - {getPriorityLabel(response.priority as PriorityLevel)}
                                </>
                            ) : (
                                <>
                                    {priorityIcon}
                                    {localCalc.priority} - {priorityLabel}
                                    <span className={styles.scoreBadge}>
                                        {localCalc.score}/100
                                    </span>
                                </>
                            )}
                        </Badge>
                    )}
                </div>
            </div>

            {/* Formula */}
            {/* <div className={styles.formula} data-testid="formula-reference">
                <HelpCircle size={12} className={styles.formulaIcon} />
                Score = ((Q1 × 0.5) + (Q2 × 0.3) + (Q3 × 0.2)) × 10
            </div> */}

            {/* Error */}
            {error && (
                <div className={styles.error} role="alert" data-testid="criticality-error">
                    <AlertTriangle size={16} />
                    {error}
                </div>
            )}
        </div>
    );
}