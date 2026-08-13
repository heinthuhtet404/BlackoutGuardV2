import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "../../auth/AuthContext";
import { resetTokenStoreForTests, setTokens } from "../../auth/tokenStore";
import { ApiError, post, put } from "../../api/apiClient";
import { LoadForm } from "./LoadForm";
import type { ZoneTree } from "../../types/zone";

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
const mockedPut = vi.mocked(put);

const sampleZones: ZoneTree[] = [
  {
    id: "zone-1",
    facilityId: "facility-1",
    name: "Main Building",
    type: "building",
    parentZoneId: null,
    children: [],
  },
];

function renderForm(loadId?: string) {
  seedAuth("Admin");
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  queryClient.setQueryData(["zones"], sampleZones);

  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <LoadForm loadId={loadId} />
      </AuthProvider>
    </QueryClientProvider>
  );
}

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

function fillRequiredFields() {
  fireEvent.change(screen.getByTestId("load-name"), { target: { value: "New Load" } });
  fireEvent.change(screen.getByTestId("load-zone"), { target: { value: "zone-1" } });
  fireEvent.change(screen.getByTestId("load-relay-address"), { target: { value: "3" } });
  fireEvent.change(screen.getByTestId("load-power-rating"), { target: { value: "50" } });
}

beforeEach(() => {
  localStorage.clear();
  resetTokenStoreForTests();
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("LoadForm error handling", () => {
  it("renders the conflicting load's name on relay conflict (409)", async () => {
    mockedPost.mockRejectedValueOnce(
      new ApiError(409, "Relay address 3 is already assigned to 'ICU Ventilator Bank'")
    );
    renderForm();

    fillRequiredFields();
    fireEvent.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(screen.getByText(/ICU Ventilator Bank/)).toBeInTheDocument();
    });
    expect(screen.getByText(/already assigned to/)).toBeInTheDocument();
    expect(screen.queryByTestId("capacity-error")).not.toBeInTheDocument();
    expect(screen.queryByTestId("override-button")).not.toBeInTheDocument();
    expect(screen.queryByTestId("general-error")).not.toBeInTheDocument();
  });

  it("renders a distinct override button on capacity conflict (409)", async () => {
    mockedPost.mockRejectedValueOnce(
      new ApiError(
        409,
        "P1 capacity exceeded by 30.0 kW. Total P1: 130.0 kW, Capacity: 100.0 kW. Use force=true to override."
      )
    );
    renderForm();

    fillRequiredFields();
    fireEvent.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(screen.getByTestId("capacity-error")).toBeInTheDocument();
    });
    expect(screen.getByText(/30\.0 kW/)).toBeInTheDocument();
    const overrideButton = screen.getByTestId("override-button");
    expect(overrideButton).toBeInTheDocument();
    expect(overrideButton.textContent).toBe("Save anyway (override)");
    expect(screen.queryByTestId("general-error")).not.toBeInTheDocument();
  });

  it("does NOT render override button on other error types", async () => {
    mockedPost.mockRejectedValueOnce(new ApiError(400, "Load name is required."));
    renderForm();

    fireEvent.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(screen.getByTestId("general-error")).toBeInTheDocument();
    });
    expect(screen.getByText("Load name is required.")).toBeInTheDocument();
    expect(screen.queryByTestId("override-button")).not.toBeInTheDocument();
    expect(screen.queryByTestId("capacity-error")).not.toBeInTheDocument();
  });

  it("resubmits with force=true when override button clicked", async () => {
    mockedPost.mockRejectedValueOnce(
      new ApiError(409, "P1 capacity exceeded by 30.0 kW. Use force=true to override.")
    );
    mockedPost.mockResolvedValueOnce({ id: "load-1" } as never);
    renderForm();

    fillRequiredFields();
    fireEvent.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(screen.getByTestId("override-button")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("override-button"));

    await waitFor(() => {
      expect(mockedPost).toHaveBeenLastCalledWith(
        "/api/v1/loads?force=true",
        expect.objectContaining({ relayAddress: 3 })
      );
    });
  });

  it("edit mode uses PUT to the load's endpoint", async () => {
    mockedPut.mockResolvedValueOnce(undefined as never);
    renderForm("load-42");

    fillRequiredFields();
    fireEvent.click(screen.getByTestId("save-button"));

    await waitFor(() => {
      expect(mockedPut).toHaveBeenCalledWith(
        "/api/v1/loads/load-42?force=false",
        expect.objectContaining({ name: "New Load" })
      );
    });
  });
});
