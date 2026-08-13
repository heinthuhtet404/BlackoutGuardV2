import type { ButtonHTMLAttributes, ReactNode } from "react";
import styles from "./Button.module.css";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger";
  children: ReactNode;
}

export function Button({ variant = "primary", children, className, ...rest }: ButtonProps) {
  const variantClass =
    variant === "danger"
      ? styles.danger
      : variant === "secondary"
        ? styles.secondary
        : styles.primary;

  return (
    <button
      className={`${styles.button} ${variantClass} ${className ?? ""}`}
      {...rest}
    >
      {children}
    </button>
  );
}
