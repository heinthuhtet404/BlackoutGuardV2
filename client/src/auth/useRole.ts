// These are UI-VISIBILITY checks only. Real security is server-side via JWT + [Authorize] attributes.
import { useAuth } from "./authTypes";
import type { Role } from "./authTypes";

const ROLE_HIERARCHY: Record<Role, number> = {
  Viewer: 0,
  Operator: 1,
  Admin: 2,
};

export interface UseRoleResult {
  role: Role | null;
  isAtLeast: (role: Role) => boolean;
  is: (role: Role) => boolean;
}

export function useRole(): UseRoleResult {
  const { user } = useAuth();
  const role = user?.role ?? null;

  const isAtLeast = (required: Role): boolean => {
    if (role === null) return false;
    return ROLE_HIERARCHY[role] >= ROLE_HIERARCHY[required];
  };

  const is = (required: Role): boolean => role === required;

  return { role, isAtLeast, is };
}
