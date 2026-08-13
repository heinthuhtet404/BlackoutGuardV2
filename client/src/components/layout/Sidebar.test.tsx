import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "../../auth/AuthContext";
import { resetTokenStoreForTests, setTokens } from "../../auth/tokenStore";
import type { AuthUser } from "../../auth/tokenStore";
import { Sidebar } from "./Sidebar";
import { AppShell } from "./AppShell";

vi.mock("../../api/apiClient", () => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  del: vi.fn(),
}));

function makeUser(role: "Admin" | "Operator" | "Viewer"): AuthUser {
  return {
    id: "user-1",
    email: `${role.toLowerCase()}@test.com`,
    role,
    facilityId: "facility-1",
  };
}

function seedTokens(role: "Admin" | "Operator" | "Viewer"): void {
  setTokens({
    accessToken: "access-token-123",
    refreshToken: "refresh-token-456",
    user: makeUser(role),
  });
}

function renderSidebarWith(role: "Admin" | "Operator" | "Viewer") {
  seedTokens(role);
  return render(
    <MemoryRouter>
      <AuthProvider>
        <Sidebar />
      </AuthProvider>
    </MemoryRouter>
  );
}

beforeEach(() => {
  localStorage.clear();
  resetTokenStoreForTests();
});

afterEach(() => {
  cleanup();
});

describe("Sidebar role visibility", () => {
  it("shows all items for Admin", () => {
    renderSidebarWith("Admin");

    expect(screen.getByText("Live Overview")).toBeInTheDocument();
    expect(screen.getByText("Topology Config")).toBeInTheDocument();
    expect(screen.getByText("Rules Engine")).toBeInTheDocument();
    expect(screen.getByText("Simulator Panel")).toBeInTheDocument();
    expect(screen.getByText("Audit Logs")).toBeInTheDocument();
    expect(screen.getByText("User Management")).toBeInTheDocument();
  });

  it("hides Simulator Panel and User Management for Operator", () => {
    renderSidebarWith("Operator");

    expect(screen.getByText("Live Overview")).toBeInTheDocument();
    expect(screen.getByText("Topology Config")).toBeInTheDocument();
    expect(screen.getByText("Rules Engine")).toBeInTheDocument();
    expect(screen.getByText("Audit Logs")).toBeInTheDocument();

    expect(screen.queryByText("Simulator Panel")).not.toBeInTheDocument();
    expect(screen.queryByText("User Management")).not.toBeInTheDocument();
  });

  it("hides Rules Engine, Simulator Panel, and User Management for Viewer", () => {
    renderSidebarWith("Viewer");

    expect(screen.getByText("Live Overview")).toBeInTheDocument();
    expect(screen.getByText("Topology Config")).toBeInTheDocument();
    expect(screen.getByText("Audit Logs")).toBeInTheDocument();

    expect(screen.queryByText("Rules Engine")).not.toBeInTheDocument();
    expect(screen.queryByText("Simulator Panel")).not.toBeInTheDocument();
    expect(screen.queryByText("User Management")).not.toBeInTheDocument();
  });

  it("renders nothing when unauthenticated", () => {
    render(
      <MemoryRouter>
        <AuthProvider>
          <Sidebar />
        </AuthProvider>
      </MemoryRouter>
    );

    expect(screen.queryByText("Live Overview")).not.toBeInTheDocument();
    expect(screen.queryByText("Audit Logs")).not.toBeInTheDocument();
  });
});

describe("AppShell redirect", () => {
  it("redirects to /login when unauthenticated", () => {
    render(
      <MemoryRouter initialEntries={["/overview"]}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page</div>} />
            <Route element={<AppShell />}>
              <Route path="/overview" element={<div>Protected Content</div>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    );

    expect(screen.getByText("Login Page")).toBeInTheDocument();
    expect(screen.queryByText("Protected Content")).not.toBeInTheDocument();
  });

  it("shows protected content when authenticated", () => {
    seedTokens("Admin");
    render(
      <MemoryRouter initialEntries={["/overview"]}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page</div>} />
            <Route element={<AppShell />}>
              <Route path="/overview" element={<div>Protected Content</div>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    );

    expect(screen.getByText("Protected Content")).toBeInTheDocument();
    expect(screen.queryByText("Login Page")).not.toBeInTheDocument();
  });
});
