import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { get } from "../../api/apiClient";
import { AuditTable } from "./AuditTable";
import type { AuditPage } from "../../hooks/useAuditLog";
import type { DecisionExecutedPayload } from "../../hooks/useAuditHub";

let emitDecision: ((payload: DecisionExecutedPayload) => void) | null = null;
let emitReconnect: (() => void) | null = null;

vi.mock("../../hooks/useAuditHub", () => ({
  useAuditHub: (options: {
    onDecisionExecuted: (payload: DecisionExecutedPayload) => void;
    onReconnected: () => void;
  }) => {
    emitDecision = options.onDecisionExecuted;
    emitReconnect = options.onReconnected;
  },
}));

vi.mock("../../api/apiClient", () => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  del: vi.fn(),
}));

const mockedGet = vi.mocked(get);

const initialPage: AuditPage = {
  items: [
    {
      id: 1,
      timestampUtc: "2026-08-16T10:00:00Z",
      eventType: "Load Shedding Executed",
      rationale: "Frequency below 48.0 Hz",
      affectedLoadId: "Relay #1",
    },
    {
      id: 2,
      timestampUtc: "2026-08-16T09:59:00Z",
      eventType: "Load Restored",
      rationale: "Frequency recovered above 48.5 Hz",
      affectedLoadId: "Relay #1",
    },
  ],
  totalCount: 2,
};

function renderAuditTable() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuditTable />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  emitDecision = null;
  emitReconnect = null;
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("AuditTable", () => {
  it("renders initial history from API", async () => {
    mockedGet.mockResolvedValueOnce(initialPage as never);
    renderAuditTable();

    await waitFor(() => {
      expect(screen.getByText("Frequency below 48.0 Hz")).toBeInTheDocument();
    });
    expect(screen.getByText("Load Shedding Executed")).toBeInTheDocument();
    expect(screen.getByText("Load Restored")).toBeInTheDocument();
  });

  it("prepends a new row when DecisionExecuted arrives", async () => {
    mockedGet.mockResolvedValueOnce(initialPage as never);
    renderAuditTable();

    await waitFor(() => {
      expect(screen.getByText("Frequency below 48.0 Hz")).toBeInTheDocument();
    });

    const payload: DecisionExecutedPayload = {
      relayDecisions: [
        { relayAddress: 7, energize: false, rationale: "frequency low" },
      ],
      rationale: "Shedding load due to low frequency",
    };

    act(() => {
      emitDecision?.(payload);
    });

    const liveRowText = await screen.findByText("Shedding load due to low frequency");
    expect(liveRowText).toBeInTheDocument();

    // Verify it's the FIRST row in the tbody.
    const rows = screen.getAllByRole("row");
    const firstDataRow = rows[1]; // rows[0] is the header
    expect(firstDataRow.textContent).toContain("Shedding load due to low frequency");
    expect(firstDataRow.textContent).toContain("Relay #7");
  });

  it("refetches history on reconnect (gap-fill)", async () => {
    const refetchedPage: AuditPage = {
      items: [
        {
          id: 3,
          timestampUtc: "2026-08-16T10:05:00Z",
          eventType: "Load Shedding Executed",
          rationale: "Gap-filled entry",
          affectedLoadId: "Relay #2",
        },
      ],
      totalCount: 3,
    };

    mockedGet
      .mockResolvedValueOnce(initialPage as never)
      .mockResolvedValueOnce(refetchedPage as never);

    renderAuditTable();

    await waitFor(() => {
      expect(screen.getByText("Frequency below 48.0 Hz")).toBeInTheDocument();
    });

    act(() => {
      emitReconnect?.();
    });

    await waitFor(() => {
      expect(mockedGet).toHaveBeenCalledTimes(2);
    });
    await waitFor(() => {
      expect(screen.getByText("Gap-filled entry")).toBeInTheDocument();
    });
  });
});
