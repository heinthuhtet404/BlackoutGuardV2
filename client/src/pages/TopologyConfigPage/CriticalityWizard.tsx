import { useState, type ReactNode } from "react";
import { useCriticality } from "../../hooks/useCriticality";
import { Slider } from "../../components/ui/Slider";
import { Badge } from "../../components/ui/Badge";
import styles from "./CriticalityWizard.module.css";

export type PriorityMode = "auto" | "manual";

interface CriticalityWizardProps {
  loadId?: string;
  onManualPriorityChange?: (priority: "P1" | "P2" | "P3") => void;
}

export function CriticalityWizard({
  loadId,
  onManualPriorityChange,
}: CriticalityWizardProps): ReactNode {
  const [mode, setMode] = useState<PriorityMode>("auto");
  const [q1, setQ1] = useState(5);
  const [q2, setQ2] = useState(5);
  const [q3, setQ3] = useState(5);
  const [q4, setQ4] = useState(5);
  const [manualPriority, setManualPriority] = useState<"P1" | "P2" | "P3">("P2");

  const { schedule, response, error } = useCriticality(loadId ?? "");

  const isManual = mode === "manual";
  const canScore = loadId !== undefined && loadId !== "";

  const handleModeChange = (nextMode: PriorityMode) => {
    setMode(nextMode);
    if (nextMode === "manual") {
      onManualPriorityChange?.(manualPriority);
    }
  };

  const handleManualPriorityChange = (priority: "P1" | "P2" | "P3") => {
    setManualPriority(priority);
    onManualPriorityChange?.(priority);
  };

  return (
    <div className={styles.wizard} data-testid="criticality-wizard">
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
          Manual
        </label>
      </div>

      <Slider
        label="Q1: Safety Risk (0.5)"
        value={q1}
        disabled={isManual || !canScore}
        onChange={(event) => {
          const value = Number(event.target.value);
          setQ1(value);
          if (canScore) schedule({ q1: value, q2, q3, q4 });
        }}
        data-testid="slider-q1"
      />
      <Slider
        label="Q2: Data/Financial Risk (0.3)"
        value={q2}
        disabled={isManual || !canScore}
        onChange={(event) => {
          const value = Number(event.target.value);
          setQ2(value);
          if (canScore) schedule({ q1, q2: value, q3, q4 });
        }}
        data-testid="slider-q2"
      />
      <Slider
        label="Q3: Operational Impact (0.2)"
        value={q3}
        disabled={isManual || !canScore}
        onChange={(event) => {
          const value = Number(event.target.value);
          setQ3(value);
          if (canScore) schedule({ q1, q2, q3: value, q4 });
        }}
        data-testid="slider-q3"
      />
      <Slider
        label="Q4: Comfort (display only)"
        value={q4}
        disabled={isManual || !canScore}
        onChange={(event) => setQ4(Number(event.target.value))}
        data-testid="slider-q4"
      />

      {isManual && (
        <div className={styles.manualDropdown} data-testid="manual-priority-dropdown">
          <label className={styles.label} htmlFor="manual-priority">
            Direct priority assignment
          </label>
          <select
            id="manual-priority"
            value={manualPriority}
            onChange={(event) =>
              handleManualPriorityChange(event.target.value as "P1" | "P2" | "P3")
            }
            data-testid="manual-priority-select"
          >
            <option value="P1">P1</option>
            <option value="P2">P2</option>
            <option value="P3">P3</option>
          </select>
        </div>
      )}

      <div className={styles.priorityRow}>
        <span className={styles.label}>Priority</span>
        {isManual ? (
          <Badge priority={manualPriority}>{manualPriority}</Badge>
        ) : (
          <Badge priority={response?.priority ?? "—"}>
            {response ? response.priority : "Awaiting score..."}
          </Badge>
        )}
      </div>

      <div className={styles.formula} data-testid="formula-reference">
        Score = ((Q1 × 0.5) + (Q2 × 0.3) + (Q3 × 0.2)) × 10
      </div>

      {error && (
        <div className={styles.error} role="alert" data-testid="criticality-error">
          {error}
        </div>
      )}
    </div>
  );
}
