import { useRole } from "../../auth/useRole";
import { getAccessToken } from "../../auth/tokenStore";
import styles from "./ExportButtons.module.css";

function download(format: "csv" | "pdf"): void {
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/api/v1";
  const url = `${apiBase}/audit/export?format=${format}`;
  const token = getAccessToken();

  fetch(url, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })
    .then((response) => {
      if (!response.ok) {
        throw new Error(`Export failed: ${response.status}`);
      }
      return response.blob();
    })
    .then((blob) => {
      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = objectUrl;
      link.download = `audit.${format}`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(objectUrl);
    })
    .catch((err: unknown) => {
      console.error(err instanceof Error ? err.message : "Export failed");
    });
}

export function ExportButtons() {
  const { isAtLeast } = useRole();

  if (!isAtLeast("Operator")) {
    return null;
  }

  return (
    <div className={styles.container}>
      <button
        type="button"
        className={`${styles.button} ${styles.buttonCsv}`}
        onClick={() => download("csv")}
        data-testid="export-csv"
      >
         Export CSV
      </button>
      <button
        type="button"
        className={`${styles.button} ${styles.buttonPdf}`}
        onClick={() => download("pdf")}
        data-testid="export-pdf"
      >
         Export PDF
      </button>
    </div>
  );
}