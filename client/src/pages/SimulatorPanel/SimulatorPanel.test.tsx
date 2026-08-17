import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor, act } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../components/ui/Toast";
import { resetTokenStoreForTests, setTokens } from "../../auth/tokenStore";
import { post } from "../../api/apiClient";
import { SimulatorPanel } from "./SimulatorPanel";

vi.mock("../../api/apiClient", () => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  del: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number;
    constructor(status: number, message: string) {
      super(message);
      this.name = "ApiError";
      this.status = status;
    }
  },
}));

vi.mock("../../hooks/useTelemetryHub", () => ({
  useTelemetryHub: () => ({
    telemetry: { frequency: 49.8, voltage: 229.5, totalLoadKw: 150.2, generatorOn: true },
    connected: true,
  }),
}));

const mockedPost = vi.mocked(post);

function seedAuth(role: "Admin" | "Operator" | "Viewer") {
  setTokens({
    accessToken: "access-token-123",
    refreshToken: "refresh-token-456",
    user: {
      id: "user-1",
      email: `${role.toLowerCase()}@test.com`,
      role,
      facilityId: "facility-1",
    },
  });
}

function renderPanelAtSimulator(role: "Admin" | "Operator" | "Viewer") {
  seedAuth(role);
  return render(
    <MemoryRouter initialEntries={["/simulator"]}>
      <AuthProvider>
        <ToastProvider>
          <Routes>
            <Route path="/overview" element={<div>Overview Page</div>} />
            <Route path="/simulator" element={<SimulatorPanel />} />
          </Routes>
        </ToastProvider>
      </AuthProvider>
    </MemoryRouter>
  );
}

beforeEach(() => {
  localStorage.clear();
  resetTokenStoreForTests();
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("SimulatorPanel", () => {
  it("redirects non-Admin away from /simulator", async () => {
    renderPanelAtSimulator("Operator");

    await waitFor(() => {
      expect(screen.getByText("Overview Page")).toBeInTheDocument();
    });
    expect(screen.queryByTestId("simulator-panel")).not.toBeInTheDocument();
  });

  it("redirects Viewer away from /simulator", () => {
    renderPanelAtSimulator("Viewer");

    expect(screen.getByText("Overview Page")).toBeInTheDocument();
    expect(screen.queryByTestId("simulator-panel")).not.toBeInTheDocument();
  });

  it("renders panel for Admin with live telemetry from server", () => {
    renderPanelAtSimulator("Admin");

    expect(screen.getByTestId("simulator-panel")).toBeInTheDocument();
    expect(screen.getByTestId("telemetry-frequency")).toHaveTextContent("49.80 Hz");
    expect(screen.getByTestId("telemetry-voltage")).toHaveTextContent("229.5 V");
    expect(screen.getByTestId("telemetry-load")).toHaveTextContent("150.2 kW");
    expect(screen.getByTestId("telemetry-generator")).toHaveTextContent("ON");
  });

  it("debounces frequency slider changes by 150ms", async () => {
    vi.useFakeTimers();
    try {
      renderPanelAtSimulator("Admin");
      mockedPost.mockResolvedValue(undefined as never);

      const slider = screen.getByTestId("frequency-slider");
      fireEvent.change(slider, { target: { value: "47.5" } });
      fireEvent.change(slider, { target: { value: "47.0" } });
      fireEvent.change(slider, { target: { value: "46.5" } });

      expect(mockedPost).not.toHaveBeenCalled();

      await act(async () => {
        vi.advanceTimersByTime(149);
      });
      expect(mockedPost).not.toHaveBeenCalled();

      await act(async () => {
        vi.advanceTimersByTime(1);
      });

      expect(mockedPost).toHaveBeenCalledTimes(1);
      expect(mockedPost).toHaveBeenCalledWith("/simulator/telemetry", {
        frequency: 46.5,
      });
    } finally {
      vi.useRealTimers();
    }
  });

  it("posts fault injection with frequency_drop preset", async () => {
    renderPanelAtSimulator("Admin");
    mockedPost.mockResolvedValue(undefined as never);

    fireEvent.click(screen.getByTestId("inject-fault"));

    await waitFor(() => {
      expect(mockedPost).toHaveBeenCalledWith("/simulator/fault", {
        preset: "frequency_drop",
      });
    });
  });

  it("posts generator toggle state immediately", async () => {
    renderPanelAtSimulator("Admin");
    mockedPost.mockResolvedValue(undefined as never);

    fireEvent.click(screen.getByTestId("generator-toggle"));

    await waitFor(() => {
      expect(mockedPost).toHaveBeenCalledWith("/simulator/telemetry", {
        generatorOn: false,
      });
    });
  });
});
