import { createContext, useContext } from "react";

export interface ToastContextValue {
  showToast: (text: string, kind?: "error" | "info") => void;
}

export const ToastContext = createContext<ToastContextValue | undefined>(undefined);

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error("useToast must be used within a ToastProvider");
  }
  return context;
}
