import { useCallback, useState, type ReactNode } from "react";
import { ToastContext } from "./toastContext";
import styles from "./Toast.module.css";

interface ToastMessage {
  id: number;
  text: string;
  kind: "error" | "info";
}

let nextId = 1;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((t) => t.id !== id));
  }, []);

  const showToast = useCallback(
    (text: string, kind: "error" | "info" = "info") => {
      const id = nextId++;
      setToasts((current) => [...current, { id, text, kind }]);
      setTimeout(() => dismiss(id), 5000);
    },
    [dismiss]
  );

  return (
    <ToastContext.Provider value={{ showToast }}>
      {children}
      <div className={styles.container} role="region" aria-label="Notifications">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={`${styles.toast} ${toast.kind === "error" ? styles.error : styles.info}`}
            role="alert"
          >
            {toast.text}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}
