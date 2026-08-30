import { useState } from "react";
import { useRole } from "../../auth/useRole";
import { getAccessToken } from "../../auth/tokenStore";
import type { AuditEntry } from "../../hooks/useAuditLog";
import {
    Download,
    FileSpreadsheet,
    FileText,
    Loader2,
    CheckCircle,
    AlertCircle,
} from "lucide-react";
import styles from "./ExportButtons.module.css";

interface ExportButtonsProps {
    data?: AuditEntry[];
}

export function ExportButtons({ data }: ExportButtonsProps) {
    const { isAtLeast } = useRole();
    const [isExporting, setIsExporting] = useState<"csv" | "pdf" | null>(null);
    const [error, setError] = useState<string | null>(null);

    if (!isAtLeast("Operator")) {
        return null;
    }

    const handleDownload = async (format: "csv" | "pdf") => {
        setIsExporting(format);
        setError(null);
        try {
            const apiBase = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/api/v1";
            const url = `${apiBase}/audit/export?format=${format}`;
            const token = getAccessToken();

            const response = await fetch(url, {
                method: "GET",
                headers: {
                    ...(token ? { Authorization: `Bearer ${token}` } : {}),
                },
            });

            if (!response.ok) {
                throw new Error(`Export failed (${response.status})`);
            }

            const blob = await response.blob();
            const objectUrl = URL.createObjectURL(blob);
            const link = document.createElement("a");
            link.href = objectUrl;
            link.download = `audit_${new Date().toISOString().slice(0, 10)}.${format}`;
            document.body.appendChild(link);
            link.click();
            link.remove();
            URL.revokeObjectURL(objectUrl);
        } catch (err: unknown) {
            const errorMessage = err instanceof Error ? err.message : "Export failed";
            console.error("Export Error:", errorMessage);
            setError(errorMessage);
        } finally {
            setIsExporting(null);
        }
    };

    return (
        <div className={styles.container}>
            {error && (
                <div className={styles.errorToast}>
                    <AlertCircle size={16} />
                    {error}
                </div>
            )}
            <button
                type="button"
                className={`${styles.button} ${styles.buttonCsv}`}
                onClick={() => handleDownload("csv")}
                disabled={isExporting !== null}
                data-testid="export-csv"
            >
                {isExporting === "csv" ? (
                    <>
                        <Loader2 size={16} className={styles.spinning} />
                        Exporting...
                    </>
                ) : (
                    <>
                        <FileSpreadsheet size={16} />
                        Export CSV
                    </>
                )}
            </button>
            <button
                type="button"
                className={`${styles.button} ${styles.buttonPdf}`}
                onClick={() => handleDownload("pdf")}
                disabled={isExporting !== null}
                data-testid="export-pdf"
            >
                {isExporting === "pdf" ? (
                    <>
                        <Loader2 size={16} className={styles.spinning} />
                        Exporting...
                    </>
                ) : (
                    <>
                        <FileText size={16} />
                        Export PDF
                    </>
                )}
            </button>
        </div>
    );
}