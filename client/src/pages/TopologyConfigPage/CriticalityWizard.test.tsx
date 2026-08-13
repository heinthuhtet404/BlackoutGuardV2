import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent, act } from "@testing-library/react";
import { CriticalityWizard } from "./CriticalityWizard";
import { post } from "../../api/apiClient";

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

const mockedPost = vi.mocked(post);

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

function renderWizard(loadId = "load-1") {
  return render(<CriticalityWizard loadId={loadId} />);
}

describe("CriticalityWizard", () => {
  it("enables sliders in auto mode and disables them in manual mode", async () => {
    renderWizard();

    expect(screen.getByTestId("slider-q1")).not.toBeDisabled();
    expect(screen.getByTestId("slider-q2")).not.toBeDisabled();
    expect(screen.getByTestId("slider-q3")).not.toBeDisabled();
    expect(screen.getByTestId("slider-q4")).not.toBeDisabled();

    fireEvent.click(screen.getByTestId("mode-manual"));

    expect(screen.getByTestId("slider-q1")).toBeDisabled();
    expect(screen.getByTestId("slider-q2")).toBeDisabled();
    expect(screen.getByTestId("slider-q3")).toBeDisabled();
    expect(screen.getByTestId("slider-q4")).toBeDisabled();

    fireEvent.click(screen.getByTestId("mode-auto"));

    expect(screen.getByTestId("slider-q1")).not.toBeDisabled();
  });

  it("displays priority from API response, not client-side calculation", async () => {
    // Client-side calculation for q1=1,q2=1,q3=1 would be 10 -> P3.
    // API responds P1 — badge MUST show P1.
    mockedPost.mockResolvedValueOnce({ score: 99, priority: "P1" } as never);

    renderWizard();

    act(() => {
      fireEvent.change(screen.getByTestId("slider-q1"), { target: { value: "1" } });
      fireEvent.change(screen.getByTestId("slider-q2"), { target: { value: "1" } });
      fireEvent.change(screen.getByTestId("slider-q3"), { target: { value: "1" } });
    });

    await waitFor(() => {
      expect(mockedPost).toHaveBeenCalled();
    });
    await waitFor(() => {
      expect(screen.getByTestId("priority-badge")).toHaveTextContent("P1");
    });
  });

  it("shows the manual dropdown in manual mode", async () => {
    renderWizard();

    expect(screen.queryByTestId("manual-priority-dropdown")).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId("mode-manual"));

    expect(screen.getByTestId("manual-priority-dropdown")).toBeInTheDocument();
    expect(screen.getByTestId("manual-priority-select")).toBeInTheDocument();
  });

  it("debounces slider changes by 300ms", async () => {
    vi.useFakeTimers();
    try {
      mockedPost.mockResolvedValue({ score: 70, priority: "P2" } as never);

      renderWizard();

      fireEvent.change(screen.getByTestId("slider-q1"), { target: { value: "8" } });
      fireEvent.change(screen.getByTestId("slider-q1"), { target: { value: "9" } });
      fireEvent.change(screen.getByTestId("slider-q1"), { target: { value: "7" } });

      expect(mockedPost).not.toHaveBeenCalled();

      await act(async () => {
        vi.advanceTimersByTime(299);
      });
      expect(mockedPost).not.toHaveBeenCalled();

      await act(async () => {
        vi.advanceTimersByTime(1);
      });

      expect(mockedPost).toHaveBeenCalledTimes(1);
      expect(mockedPost).toHaveBeenCalledWith(
        "/loads/load-1/criticality",
        expect.objectContaining({ q1: 7 })
      );
    } finally {
      vi.useRealTimers();
    }
  });

  it("shows the formula reference", () => {
    renderWizard();
    expect(screen.getByTestId("formula-reference")).toHaveTextContent(
      "Score = ((Q1 × 0.5) + (Q2 × 0.3) + (Q3 × 0.2)) × 10"
    );
  });

  it("notifies parent of manual priority selection", async () => {
    const onManualPriorityChange = vi.fn();
    render(
      <CriticalityWizard loadId="load-1" onManualPriorityChange={onManualPriorityChange} />
    );

    fireEvent.click(screen.getByTestId("mode-manual"));
    expect(onManualPriorityChange).toHaveBeenCalledWith("P2");

    fireEvent.change(screen.getByTestId("manual-priority-select"), {
      target: { value: "P1" },
    });
    expect(onManualPriorityChange).toHaveBeenCalledWith("P1");
  });
});
