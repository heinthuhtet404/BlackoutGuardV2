import React, { useEffect, useState, useMemo } from "react";
import { get, post, del } from "../api/apiClient";
import { useToast } from "../components/ui/toastContext";
import { useAuth } from "../auth/authTypes";
import styles from "./UserManagementPage.module.css";

export type UserRole = "Admin" | "Operator" | "Viewer";
export type UserStatus = "Active" | "Pending" | "Inactive";

export interface User {
    id: string;
    fullName?: string;
    username?: string;
    email: string;
    role: UserRole;
    status: UserStatus;
    password?: string;
    tenantId?: string;
}

interface UserModalProps {
    isOpen: boolean;
    onClose: () => void;
    onSave: (userData: Partial<User> & { sendWelcomeEmail?: boolean }) => Promise<void>;
}

const validatePassword = (pwd: string) => {
    return (
        pwd.length >= 8 &&
        /[A-Z]/.test(pwd) &&
        /[a-z]/.test(pwd) &&
        /[0-9]/.test(pwd) &&
        /[!@#$%^&*(),.?":{}|<>]/.test(pwd)
    );
};

function UserModal({ isOpen, onClose, onSave }: UserModalProps) {
    const [fullName, setFullName] = useState("");
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [role, setRole] = useState<UserRole>("Operator");
    const [showPassword, setShowPassword] = useState(false);
    const [sendWelcomeEmail, setSendWelcomeEmail] = useState(true);
    const [submitting, setSubmitting] = useState(false);
    const [validationError, setValidationError] = useState<string | null>(null);

    useEffect(() => {
        if (isOpen) {
            setValidationError(null);
            setFullName("");
            setEmail("");
            setPassword("");
            setRole("Operator");
            setSendWelcomeEmail(true);
            setShowPassword(false);
        }
    }, [isOpen]);

    if (!isOpen) return null;

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setValidationError(null);

        if (!validatePassword(password)) {
            setValidationError(
                "Password တွင် အနည်းဆုံး ၈ လုံး၊ စာလုံးကြီး၊ စာလုံးငယ်၊ နံပါတ် နှင့် Special Character ပါဝင်ရပါမည်။"
            );
            return;
        }

        setSubmitting(true);
        try {
            const payload: Partial<User> & { sendWelcomeEmail?: boolean } = {
                fullName: fullName.trim(),
                email: email.trim(),
                password,
                role,
                status: "Active",
                sendWelcomeEmail,
            };

            await onSave(payload);
            onClose();
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className={styles.modalOverlay} role="dialog" aria-modal="true">
            <div className={styles.modal}>
                <div className={styles.modalHeader}>
                    <h2>👤 Create New User</h2>
                    <button type="button" className={styles.modalClose} onClick={onClose}>✕</button>
                </div>
                <form onSubmit={handleSubmit}>
                    <div className={styles.formGroup}>
                        <label htmlFor="fullName">Full Name</label>
                        <input
                            id="fullName"
                            type="text"
                            required
                            className={styles.formInput}
                            value={fullName}
                            onChange={(e) => setFullName(e.target.value)}
                            placeholder="e.g. Dr. Aung Zin"
                        />
                    </div>

                    <div className={styles.formGroup}>
                        <label htmlFor="email">Email Address</label>
                        <input
                            id="email"
                            type="email"
                            required
                            className={styles.formInput}
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            placeholder="user@hospital.com"
                        />
                    </div>

                    <div className={styles.formGroup}>
                        <label htmlFor="password">Password</label>
                        <div className={styles.passwordInputWrapper}>
                            <input
                                id="password"
                                type={showPassword ? "text" : "password"}
                                required
                                className={styles.formInput}
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                placeholder="Enter secure password"
                            />
                            <button
                                type="button"
                                className={styles.eyeIconBtn}
                                onClick={() => setShowPassword(!showPassword)}
                                aria-label={showPassword ? "Hide password" : "Show password"}
                            >
                                {showPassword ? "🙈" : "👁️"}
                            </button>
                        </div>
                    </div>

                    {validationError && (
                        <div className={styles.validationError}>
                            ⚠️ {validationError}
                        </div>
                    )}

                    <div className={styles.formGroup}>
                        <label htmlFor="role">Role</label>
                        <select
                            id="role"
                            className={styles.formSelect}
                            value={role}
                            onChange={(e) => setRole(e.target.value as UserRole)}
                        >
                            <option value="Operator">⚙️ Operator</option>
                            <option value="Viewer">👁️ Viewer</option>
                        </select>
                    </div>

                    <div className={styles.checkboxGroup}>
                        <input
                            type="checkbox"
                            id="welcomeEmail"
                            checked={sendWelcomeEmail}
                            onChange={(e) => setSendWelcomeEmail(e.target.checked)}
                        />
                        <label htmlFor="welcomeEmail">Send Welcome Email (with credentials)</label>
                    </div>

                    <div className={styles.modalActions}>
                        <button type="button" className={styles.cancelBtn} onClick={onClose} disabled={submitting}>
                            Cancel
                        </button>
                        <button type="submit" disabled={submitting} className={styles.saveBtn}>
                            {submitting ? "Creating..." : "Create User"}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}

export function UserManagementPage() {
    const [users, setUsers] = useState<User[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState("");
    const [roleFilter, setRoleFilter] = useState<string>("All");
    const [visiblePasswords, setVisiblePasswords] = useState<{ [key: string]: boolean }>({});

    const { user: currentUser } = useAuth();
    const currentLoggedInUserId = currentUser?.id;

    const [isModalOpen, setIsModalOpen] = useState(false);
    const { showToast } = useToast();

    const fetchUsers = async () => {
        try {
            setLoading(true);
            const data = await get<User[]>("/users");
            setUsers(data || []);
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to load users from backend DB.", "error");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void fetchUsers();
    }, []);

    const adminCount = useMemo(() => {
        return users.filter((u) => u.role === "Admin" && u.status !== "Inactive").length;
    }, [users]);

    const logAuditTrail = async (action: string, targetUserId: string, details: string) => {
        try {
            await post("/audit", {
                eventType: `USER_MANAGEMENT_${action.toUpperCase()}`,
                rationale: `Admin action: ${action} on User ID: ${targetUserId}. Details: ${details}`,
                timestampUtc: new Date().toISOString(),
            });
        } catch {
            console.warn("Failed to write to audit log endpoint.");
        }
    };

    const handleCreateUser = () => {
        setIsModalOpen(true);
    };

    const togglePasswordVisibility = (userId: string) => {
        setVisiblePasswords((prev) => ({
            ...prev,
            [userId]: !prev[userId],
        }));
    };

    const handleDeleteUser = async (user: User) => {
        if (user.id === currentLoggedInUserId) {
            showToast("Self-Delete Protection: You cannot delete your own logged-in account.", "error");
            return;
        }

        if (user.role === "Admin" && adminCount <= 1) {
            showToast("Last Admin Protection: Cannot delete the remaining active Administrator.", "error");
            return;
        }

        const displayName = user.fullName || user.username || user.email;
        if (!window.confirm(`Are you sure you want to permanently delete ${displayName} (${user.email})?`)) {
            return;
        }

        try {
            await del(`/users/${user.id}`);
            setUsers((prev) => prev.filter((u) => u.id !== user.id));
            showToast(`User ${user.email} deleted successfully.`, "success");
            await logAuditTrail("DELETE", user.id, `Deleted account ${user.email}`);
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to delete user from database.", "error");
        }
    };

    const handleSaveUser = async (userData: Partial<User> & { sendWelcomeEmail?: boolean }) => {
        try {
            const created = await post<User>("/users", userData);
            if (created) {
                setUsers((prev) => [...prev, created]);
                showToast(`New user ${created.email} created successfully.`, "success");
                await logAuditTrail("CREATE", created.id, `Created account ${created.email} with role ${created.role}`);
            } else {
                await fetchUsers();
            }
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to create user", "error");
        }
    };

    const filteredUsers = useMemo(() => {
        return users.filter((u) => {
            const name = (u.fullName || u.username || "").toLowerCase();
            const email = (u.email || "").toLowerCase();
            const search = searchTerm.toLowerCase();

            const matchesSearch = name.includes(search) || email.includes(search);
            const matchesRole = roleFilter === "All" || u.role === roleFilter;

            return matchesSearch && matchesRole;
        });
    }, [users, searchTerm, roleFilter]);

    const getRoleBadge = (role: UserRole) => {
        switch (role) {
            case "Admin":
                return <span className={`${styles.badge} ${styles.badgeAdmin}`}>🛡️ Admin</span>;
            case "Operator":
                return <span className={`${styles.badge} ${styles.badgeOperator}`}>⚙️ Operator</span>;
            case "Viewer":
                return <span className={`${styles.badge} ${styles.badgeViewer}`}>👁️ Viewer</span>;
            default:
                return <span className={styles.badge}>{role}</span>;
        }
    };

    const getStatusStyle = (status: UserStatus) => {
        switch (status) {
            case "Active":
                return <span className={styles.statusActive}>🟢 Active</span>;
            case "Pending":
                return <span className={styles.statusPending}>🟡 Pending</span>;
            case "Inactive":
                return <span className={styles.statusInactive}>🔴 Inactive</span>;
            default:
                return <span className={styles.statusActive}>🟢 Active</span>;
        }
    };

    return (
        <div className={styles.page}>
            <div className={styles.headerContainer}>
                <div>
                    <h1 className={styles.heading}>👥 User Management</h1>
                    <p className={styles.subHeading}>Configure user permissions, roles, and access credentials.</p>
                </div>
                <button className={styles.createBtn} onClick={handleCreateUser}>
                    + Create User
                </button>
            </div>

            <div className={styles.toolbar}>
                <input
                    type="text"
                    className={styles.searchInput}
                    placeholder="🔍 Search by name or email..."
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                />
                <select
                    className={styles.roleSelectFilter}
                    value={roleFilter}
                    onChange={(e) => setRoleFilter(e.target.value)}
                >
                    <option value="All">Role: All</option>
                    <option value="Admin">Admin</option>
                    <option value="Operator">Operator</option>
                    <option value="Viewer">Viewer</option>
                </select>
            </div>

            {loading ? (
                <div className={styles.loadingState}>
                    <span className={styles.spinner}></span>
                    Loading user registry...
                </div>
            ) : (
                <>
                    <div className={styles.tableWrapper}>
                        <table className={styles.table}>
                            <thead>
                                <tr>
                                    <th>#</th>
                                    <th>Name</th>
                                    <th>Email</th>
                                    <th>Role</th>
                                    <th>Status</th>
                                    <th>Password</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredUsers.length === 0 ? (
                                    <tr>
                                        <td colSpan={7} className={styles.emptyRow}>
                                            <span className={styles.emptyIcon}>📭</span>
                                            No users matched your criteria.
                                        </td>
                                    </tr>
                                ) : (
                                    filteredUsers.map((u, index) => {
                                        const isSelf = u.id === currentLoggedInUserId;
                                        const isLastAdmin = u.role === "Admin" && adminCount <= 1;
                                        const isPasswordVisible = visiblePasswords[u.id];

                                        return (
                                            <tr key={u.id} className={isSelf ? styles.selfRow : ""}>
                                                <td>{index + 1}</td>
                                                <td>
                                                    <span className={styles.userName}>{u.fullName || u.username || "—"}</span>
                                                    {isSelf && <span className={styles.selfBadge}>You</span>}
                                                </td>
                                                <td className={styles.userEmail}>{u.email}</td>
                                                <td>{getRoleBadge(u.role)}</td>
                                                <td>{getStatusStyle(u.status)}</td>
                                                <td>
                                                    <div className={styles.passwordCell}>
                                                        <span className={styles.passwordText}>
                                                            {isPasswordVisible ? u.password || "[Hashed]" : "••••••••"}
                                                        </span>
                                                        <button
                                                            type="button"
                                                            className={styles.eyeBtnSmall}
                                                            onClick={() => togglePasswordVisibility(u.id)}
                                                            title={isPasswordVisible ? "Hide password" : "Show password"}
                                                        >
                                                            {isPasswordVisible ? "🙈" : "👁️"}
                                                        </button>
                                                    </div>
                                                </td>
                                                <td>
                                                    <button
                                                        className={styles.deleteBtn}
                                                        disabled={isSelf || isLastAdmin}
                                                        title={
                                                            isSelf
                                                                ? "Cannot delete self"
                                                                : isLastAdmin
                                                                    ? "Cannot delete last admin"
                                                                    : "Delete user"
                                                        }
                                                        onClick={() => handleDeleteUser(u)}
                                                    >
                                                        🗑️ Delete
                                                    </button>
                                                </td>
                                            </tr>
                                        );
                                    })
                                )}
                            </tbody>
                        </table>
                    </div>

                    <div className={styles.footer}>
                        Showing <strong>{filteredUsers.length}</strong> of <strong>{users.length}</strong> users
                    </div>
                </>
            )}

            <UserModal
                isOpen={isModalOpen}
                onClose={() => setIsModalOpen(false)}
                onSave={handleSaveUser}
            />
        </div>
    );
}