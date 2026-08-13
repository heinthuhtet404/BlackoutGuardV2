import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, waitFor, fireEvent } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../components/ui/Toast";
import { resetTokenStoreForTests, setTokens } from "../../auth/tokenStore";
import type { AuthUser } from "../../auth/tokenStore";
import { ZoneTreeView } from "./ZoneTreeView";
import type { ZoneTree } from "../../types/zone";

const sampleTree: ZoneTree[] = [
  {
    id: "zone-1",
    facilityId: "facility-1",
    name: "Main Building",
    type: "building",
    parentZoneId: null,
    children: [
      {
        id: "zone-2",
        facilityId: "facility-1",
        name: "Floor 1",
        type: "floor",
        parentZoneId: "zone-1",
        children: [
          {
            id: "zone-3",
            facilityId: "facility-1",
            name: "Server Room",
            type: "room",
            parentZoneId: "zone-2",
            children: [],
          },
        ],
      },
      {
        id: "zone-4",
        facilityId: "facility-1",
        name: "Floor 2",
        type: "floor",
        parentZoneId: "zone-1",
        children: [],
      },
    ],
  },
];

function makeUser(role: "Admin" | "Operator" | "Viewer"): AuthUser {
  return {
    id: "user-1",
    email: `${role.toLowerCase()}@test.com`,
    role,
    facilityId: "facility-1",
  };
}

function seedAuth(role: "Admin" | "Operator" | "Viewer"): void {
  setTokens({
    accessToken: "access-token-123",
    refreshToken: "refresh-token-456",
    user: makeUser(role),
  });
}

function renderTree(role: "Admin" | "Operator" | "Viewer") {
  seedAuth(role);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  queryClient.setQueryData(["zones"], sampleTree);

  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ToastProvider>
          <ZoneTreeView />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}

beforeEach(() => {
  localStorage.clear();
  resetTokenStoreForTests();
});

afterEach(() => {
  cleanup();
});

describe("ZoneTreeView", () => {
  it("renders the zone tree with names, types, and children", async () => {
    renderTree("Admin");

    await waitFor(() => {
      expect(screen.getByText("Main Building")).toBeInTheDocument();
    });
    expect(screen.getByText("Floor 1")).toBeInTheDocument();
    expect(screen.getByText("Server Room")).toBeInTheDocument();
    expect(screen.getByText("Floor 2")).toBeInTheDocument();
    expect(screen.getAllByText("building").length).toBeGreaterThan(0);
  });

  it("renders no drag handles for Viewer (read-only mode)", async () => {
    renderTree("Viewer");

    await waitFor(() => {
      expect(screen.getByText("Main Building")).toBeInTheDocument();
    });

    expect(screen.queryByTestId("drag-handle-zone-1")).not.toBeInTheDocument();
    expect(screen.queryByTestId("drag-handle-zone-2")).not.toBeInTheDocument();
    expect(screen.queryByTestId("drag-handle-zone-3")).not.toBeInTheDocument();
    expect(screen.queryByTestId("drag-handle-zone-4")).not.toBeInTheDocument();
  });

  it("renders no drag handles for Operator (read-only mode)", async () => {
    renderTree("Operator");

    await waitFor(() => {
      expect(screen.getByText("Main Building")).toBeInTheDocument();
    });

    expect(screen.queryByTestId("drag-handle-zone-1")).not.toBeInTheDocument();
    expect(screen.queryByTestId("drag-handle-zone-4")).not.toBeInTheDocument();
  });

  it("renders drag handles for Admin", async () => {
    renderTree("Admin");

    await waitFor(() => {
      expect(screen.getByText("Main Building")).toBeInTheDocument();
    });

    expect(screen.getByTestId("drag-handle-zone-1")).toBeInTheDocument();
    expect(screen.getByTestId("drag-handle-zone-2")).toBeInTheDocument();
    expect(screen.getByTestId("drag-handle-zone-3")).toBeInTheDocument();
    expect(screen.getByTestId("drag-handle-zone-4")).toBeInTheDocument();
  });

  it("collapses and expands children", async () => {
    renderTree("Admin");

    await waitFor(() => {
      expect(screen.getByText("Main Building")).toBeInTheDocument();
    });

    expect(screen.getByText("Floor 1")).toBeInTheDocument();

    const toggle = screen.getByTestId("toggle-zone-1");
    fireEvent.click(toggle);

    expect(screen.queryByText("Floor 1")).not.toBeInTheDocument();

    fireEvent.click(toggle);
    expect(screen.getByText("Floor 1")).toBeInTheDocument();
  });
});
