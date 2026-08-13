import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, fireEvent, cleanup } from "@testing-library/react";
import { AuthProvider } from "./AuthContext";
import { useAuth } from "./authTypes";
import { useRole } from "./useRole";
import { post } from "../api/apiClient";
import { resetTokenStoreForTests, type TokenSet } from "./tokenStore";

vi.mock("../api/apiClient", () => ({
  post: vi.fn(),
}));

const mockedPost = vi.mocked(post);

function TestConsumer() {
  const auth = useAuth();
  const role = useRole();

  return (
    <div>
      <span data-testid="user-id">{auth.user?.id ?? "none"}</span>
      <span data-testid="user-email">{auth.user?.email ?? "none"}</span>
      <span data-testid="user-role">{auth.user?.role ?? "none"}</span>
      <span data-testid="user-facility">{auth.user?.facilityId ?? "none"}</span>
      <span data-testid="access-token">{auth.accessToken ?? "none"}</span>
      <span data-testid="loading">{String(auth.isLoading)}</span>
      <span data-testid="error">{auth.error ?? "none"}</span>
      <span data-testid="hook-role">{role.role ?? "none"}</span>
      <span data-testid="is-admin">{String(role.is("Admin"))}</span>
      <span data-testid="is-at-least-admin">{String(role.isAtLeast("Admin"))}</span>
      <span data-testid="is-at-least-operator">{String(role.isAtLeast("Operator"))}</span>
      <span data-testid="is-at-least-viewer">{String(role.isAtLeast("Viewer"))}</span>
      <button
        onClick={() => {
          void auth.login("admin@test.com", "password123").catch(() => {});
        }}
      >
        login
      </button>
      <button onClick={auth.logout}>logout</button>
    </div>
  );
}

function renderWithProvider(): void {
  render(
    <AuthProvider>
      <TestConsumer />
    </AuthProvider>
  );
}

const validTokenSet: TokenSet = {
  accessToken: "access-token-123",
  refreshToken: "refresh-token-456",
  user: {
    id: "user-1",
    email: "admin@test.com",
    role: "Admin",
    facilityId: "facility-1",
  },
};

beforeEach(() => {
  localStorage.clear();
  resetTokenStoreForTests();
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("AuthContext", () => {
  it("login success populates context correctly", async () => {
    mockedPost.mockResolvedValueOnce(validTokenSet);
    renderWithProvider();

    expect(screen.getByTestId("user-id").textContent).toBe("none");

    fireEvent.click(screen.getByRole("button", { name: "login" }));

    await waitFor(() => {
      expect(screen.getByTestId("user-id").textContent).toBe("user-1");
    });
    expect(screen.getByTestId("user-email").textContent).toBe("admin@test.com");
    expect(screen.getByTestId("user-role").textContent).toBe("Admin");
    expect(screen.getByTestId("user-facility").textContent).toBe("facility-1");
    expect(screen.getByTestId("access-token").textContent).toBe("access-token-123");
    expect(screen.getByTestId("error").textContent).toBe("none");
    expect(screen.getByTestId("loading").textContent).toBe("false");
  });

  it("login failure sets error and does not populate user", async () => {
    mockedPost.mockRejectedValueOnce(new Error("Invalid credentials"));
    renderWithProvider();

    fireEvent.click(screen.getByRole("button", { name: "login" }));

    await waitFor(() => {
      expect(screen.getByTestId("error").textContent).toBe("Invalid credentials");
    });
    expect(screen.getByTestId("user-id").textContent).toBe("none");
  });

  it("logout clears context", async () => {
    mockedPost.mockResolvedValueOnce(validTokenSet);
    renderWithProvider();

    fireEvent.click(screen.getByRole("button", { name: "login" }));
    await waitFor(() => {
      expect(screen.getByTestId("user-id").textContent).toBe("user-1");
    });

    fireEvent.click(screen.getByRole("button", { name: "logout" }));

    expect(screen.getByTestId("user-id").textContent).toBe("none");
    expect(screen.getByTestId("user-email").textContent).toBe("none");
    expect(screen.getByTestId("user-role").textContent).toBe("none");
    expect(screen.getByTestId("user-facility").textContent).toBe("none");
    expect(screen.getByTestId("access-token").textContent).toBe("none");
    expect(screen.getByTestId("hook-role").textContent).toBe("none");
  });
});

describe("useRole", () => {
  const roleCases: Array<{
    role: "Admin" | "Operator" | "Viewer";
    isAdmin: boolean;
    atLeastAdmin: boolean;
    atLeastOperator: boolean;
    atLeastViewer: boolean;
  }> = [
    {
      role: "Admin",
      isAdmin: true,
      atLeastAdmin: true,
      atLeastOperator: true,
      atLeastViewer: true,
    },
    {
      role: "Operator",
      isAdmin: false,
      atLeastAdmin: false,
      atLeastOperator: true,
      atLeastViewer: true,
    },
    {
      role: "Viewer",
      isAdmin: false,
      atLeastAdmin: false,
      atLeastOperator: false,
      atLeastViewer: true,
    },
  ];

  it.each(roleCases)("returns correct values for role $role", async (roleCase) => {
    mockedPost.mockResolvedValueOnce({
      ...validTokenSet,
      user: { ...validTokenSet.user, role: roleCase.role },
    });
    renderWithProvider();

    fireEvent.click(screen.getByRole("button", { name: "login" }));
    await waitFor(() => {
      expect(screen.getByTestId("user-role").textContent).toBe(roleCase.role);
    });

    expect(screen.getByTestId("hook-role").textContent).toBe(roleCase.role);
    expect(screen.getByTestId("is-admin").textContent).toBe(String(roleCase.isAdmin));
    expect(screen.getByTestId("is-at-least-admin").textContent).toBe(
      String(roleCase.atLeastAdmin)
    );
    expect(screen.getByTestId("is-at-least-operator").textContent).toBe(
      String(roleCase.atLeastOperator)
    );
    expect(screen.getByTestId("is-at-least-viewer").textContent).toBe(
      String(roleCase.atLeastViewer)
    );
  });

  it("returns null role and false checks when unauthenticated", () => {
    renderWithProvider();

    expect(screen.getByTestId("hook-role").textContent).toBe("none");
    expect(screen.getByTestId("is-admin").textContent).toBe("false");
    expect(screen.getByTestId("is-at-least-admin").textContent).toBe("false");
    expect(screen.getByTestId("is-at-least-viewer").textContent).toBe("false");
  });
});
