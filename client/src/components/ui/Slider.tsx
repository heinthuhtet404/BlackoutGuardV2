import type { InputHTMLAttributes } from "react";
import styles from "./Slider.module.css";

interface SliderProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type"> {
  label: string;
  value: number;
  min?: number;
  max?: number;
}

export function Slider({ label, value, min = 1, max = 10, disabled, id, ...rest }: SliderProps) {
  const sliderId = id ?? `slider-${label.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;

  return (
    <div className={`${styles.wrapper} ${disabled ? styles.disabled : ""}`}>
      <div className={styles.header}>
        <label className={styles.label} htmlFor={sliderId}>
          {label}
        </label>
        <span className={styles.value} data-testid={`${sliderId}-value`}>
          {value}
        </span>
      </div>
      <input
        id={sliderId}
        type="range"
        min={min}
        max={max}
        value={value}
        disabled={disabled}
        className={styles.slider}
        {...rest}
      />
    </div>
  );
}
