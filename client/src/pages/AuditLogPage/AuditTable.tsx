import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useAuditLog, type AuditEntry } from "../../hooks/useAuditLog";
import { useAuditHub, type DecisionExecutedPayload } from "../../hooks/useAuditHub";
import { ExportButtons } from "./ExportButtons";
import styles from "./AuditTable.module.css";

const PAGE_SIZE = 20;
const MAX_LIVE_ROWS = 50;

function mapDecisionToEntry(payload: DecisionExecutedPayload): AuditEntry {
  const hasShed = payload.relayDecisions.some((d) => !d.energize);
  const hasRestore = payload.relayDecisions.some((d) => d.energize);

  const eventType =
    hasShed && hasRestore
      ? "Relay Decision Executed"
      : hasRestore
        ? "Load Restored"
        : "Load Shedding Executed";

  const affectedLoad =
    payload.relayDecisions.length > 0
      ? payload.relayDecisions
          .map((d) => `Relay #${d.relayAddress}`)
          .join(", ")
      : null;

  return {
    id: -Date.now(),
    timestampUtc: new Date().toISOString(),
    eventType,
    rationale: payload.rationale,
    affectedLoadId: affectedLoad,
  };
}

function formatTimestamp(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString();
}

export function AuditTable() {
  const [page, setPage] = useState(1);
  const [liveRows, setLiveRows] = useState<AuditEntry[]>([]);
  const queryClient = useQueryClient();

  const { data, isLoading, isError, error } = useAuditLog(page, PAGE_SIZE);

  const handleDecision = (payload: DecisionExecutedPayload) => {
    setLiveRows((current) =>
      [mapDecisionToEntry(payload), ...current].slice(0, MAX_LIVE_ROWS)
    );
  };

  const handleReconnected = () => {
    // Gap-fill: clear the transient live rows (they are now part of history)
    // and re-fetch the most recent page.
    setLiveRows([]);
    setPage(1);
    void queryClient.invalidateQueries({ queryKey: ["audit"] });
  };

  useAuditHub({
    onDecisionExecuted: handleDecision,
    onReconnected: handleReconnected,
  });

  if (isLoading) {
    return <div role="status">Loading audit log...</div>;
  }

  if (isError) {
    return (
      <div role="alert">
        Failed to load audit log: {error instanceof Error ? error.message : "unknown error"}
      </div>
    );
  }

  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? items.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const rows = [...liveRows, ...items];

  return (
    <div className={styles.page} data-testid="audit-table">
      <ExportButtons />

      <table className={styles.table}>
        <thead>
          <tr>
            <th>Timestamp</th>
            <th>Event</th>
            <th>Rationale</th>
            <th>Affected Load</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && (
            <tr>
              <td colSpan={4} className={styles.empty}>
                No audit entries yet.
              </td>
            </tr>
          )}
          {rows.map((entry) => (
            <tr
              key={entry.id}
              className={entry.id < 0 ? styles.liveRow : undefined}
              data-testid={`audit-row-${entry.id}`}
            >
              <td className={styles.timestamp}>{formatTimestamp(entry.timestampUtc)}</td>
              <td>{entry.eventType}</td>
              <td className={styles.rationale}>{entry.rationale}</td>
              <td>{entry.affectedLoadId ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className={styles.pagination}>
        <button
          type="button"
          onClick={() => setPage((p) => Math.max(1, p - 1))}
          disabled={page <= 1}
          data-testid="prev-page"
        >
          Previous
        </button>
        <span data-testid="page-indicator">
          Page {page} of {totalPages}
        </span>
        <button
          type="button"
          onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          disabled={page >= totalPages}
          data-testid="next-page"
        >
          Next
        </button>
      </div>
    </div>
  );
}
