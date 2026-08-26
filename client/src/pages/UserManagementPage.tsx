import React, { useEffect, useState, useMemo } from "react";
import { get, post, put, del } from "../api/apiClient";
import { useToast } from "../components/ui/toastContext";
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
    initialData?: User | null;
    isLastAdmin?: boolean;
}

function UserModal({ isOpen, onClose, onSave, initialData, isLastAdmin }: UserModalProps) {
    const [fullName, setFullName] = useState("");
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [role, setRole] = useState<UserRole>("Operator");
    const [status, setStatus] = useState<UserStatus>("Active");
    const [showPassword, setShowPassword] = useState(false);
    const [sendWelcomeEmail, setSendWelcomeEmail] = useState(true);
    const [submitting, setSubmitting] = useState(false);

    useEffect(() => {
        if (isOpen) {
            if (initialData) {
                setFullName(initialData.fullName || initialData.username || "");
                setEmail(initialData.email || "");
                setPassword(""); // Keep empty on edit for security
                setRole(initialData.role || "Operator");
                setStatus(initialData.status || "Active");
            } else {
                setFullName("");
                setEmail("");
                setPassword("");
                setRole("Operator");
                setStatus("Active");
                setSendWelcomeEmail(true);
            }
            setShowPassword(false);
        }
    }, [initialData, isOpen]);

    if (!isOpen) return null;

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setSubmitting(true);
        try {
            const payload: Partial<User> & { sendWelcomeEmail?: boolean } = {
                fullName,
                email,
                role,
                status,
                sendWelcomeEmail: !initialData ? sendWelcomeEmail : undefined,
            };

            if (password) {
                payload.password = password;
            }

            await onSave(payload);
            onClose();
        } finally {
            setSubmitting(false);
        }
    };

    const isEditing = Boolean(initialData);

    return (
        <div className={styles.modalOverlay} role="dialog" aria-modal="true">
            <div className={styles.modal}>
                <h2 className={styles.modalHeader}>
                    {isEditing ? `✏️ Edit User: ${initialData?.email}` : "👤 Create New User"}
                </h2>
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
                        <label htmlFor="password">
                            Password {isEditing && <span style={{ color: "#94a3b8" }}>(Leave blank to keep unchanged)</span>}
                        </label>
                        <div className={styles.passwordInputWrapper}>
                            <input
                                id="password"
                                type={showPassword ? "text" : "password"}
                                required={!isEditing}
                                className={styles.formInput}
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                placeholder={isEditing ? "••••••••" : "Enter temporary password"}
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

                    <div className={styles.formGroup}>
                        <label htmlFor="role">Role</label>
                        <select
                            id="role"
                            className={styles.formSelect}
                            value={role}
                            disabled={isEditing && isLastAdmin && initialData?.role === "Admin"}
                            onChange={(e) => setRole(e.target.value as UserRole)}
                        >
                            <option value="Admin">🛡️ Admin</option>
                            <option value="Operator">⚙️ Operator</option>
                            <option value="Viewer">👁️ Viewer</option>
                        </select>
                        {isEditing && isLastAdmin && initialData?.role === "Admin" && (
                            <p className={styles.warningText}>
                                ⚠️ Cannot demote or change role of the last remaining Admin.
                            </p>
                        )}
                    </div>

                    {isEditing && (
                        <div className={styles.formGroup}>
                            <label htmlFor="status">Status</label>
                            <select
                                id="status"
                                className={styles.formSelect}
                                value={status}
                                onChange={(e) => setStatus(e.target.value as UserStatus)}
                            >
                                <option value="Active">Active</option>
                                <option value="Pending">Pending</option>
                                <option value="Inactive">Inactive</option>
                            </select>
                        </div>
                    )}

                    {!isEditing && (
                        <div className={styles.checkboxGroup}>
                            <input
                                type="checkbox"
                                id="welcomeEmail"
                                checked={sendWelcomeEmail}
                                onChange={(e) => setSendWelcomeEmail(e.target.checked)}
                            />
                            <label htmlFor="welcomeEmail">Send Welcome Email (with credentials)</label>
                        </div>
                    )}

                    <div className={styles.modalActions}>
                        <button type="button" className={styles.cancelBtn} onClick={onClose} disabled={submitting}>
                            Cancel
                        </button>
                        <button type="submit" disabled={submitting} className={styles.saveBtn}>
                            {submitting ? "Saving..." : isEditing ? "Save Changes" : "Create User"}
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
    const [visiblePasswords, setVisiblePasswords] = useState<Record<string, boolean>>({});

    // Current Logged-in User ID (Matches DB UUID for "heinthuhtet2004@gmail.com")
    const currentLoggedInUserId = "3681f571-cc9a-4242-a1b9-fc64d911d6a0";

    const [isModalOpen, setIsModalOpen] = useState(false);
    const [editingUser, setEditingUser] = useState<User | null>(null);

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
        setEditingUser(null);
        setIsModalOpen(true);
    };

    const handleEditUser = (user: User) => {
        setEditingUser(user);
        setIsModalOpen(true);
    };

    const handleDeleteUser = async (user: User) => {
        // Prevent deleting self
        if (user.id === currentLoggedInUserId) {
            showToast("Self-Delete Protection: You cannot delete your own logged-in account.", "error");
            return;
        }

        // Prevent deleting the last remaining admin
        if (user.role === "Admin" && adminCount <= 1) {
            showToast("Last Admin Protection: Cannot delete the remaining active Administrator.", "error");
            return;
        }

        const displayName = user.fullName || user.username || user.email;
        if (!window.confirm(`Are you sure you want to permanently delete ${displayName} (${user.email})?`)) {
            return;
        }

        try {
            // Delete request directly to API endpoint -> PostgreSQL Database
            await del(`/users/${user.id}`);
            setUsers((prev) => prev.filter((u) => u.id !== user.id));
            showToast(`User ${user.email} deleted successfully.`, "success");
            await logAuditTrail("DELETE", user.id, `Deleted account ${user.email}`);
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to delete user from database.", "error");
        }
    };

    const handleSaveUser = async (userData: Partial<User> & { sendWelcomeEmail?: boolean }) => {
        if (editingUser) {
            if (editingUser.role === "Admin" && userData.role !== "Admin" && adminCount <= 1) {
                showToast("Last Admin Protection: Cannot demote the last remaining Admin.", "error");
                return;
            }

            try {
                const updated = await put<User>(`/users/${editingUser.id}`, userData);
                setUsers((prev) =>
                    prev.map((u) => (u.id === editingUser.id ? { ...u, ...userData, ...(updated || {}) } : u))
                );
                showToast(`User ${userData.email} updated successfully.`, "success");
                await logAuditTrail("UPDATE", editingUser.id, `Updated details for ${userData.email}`);
            } catch (err) {
                showToast(err instanceof Error ? err.message : "Failed to update user", "error");
            }
        } else {
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
        }
    };

    const togglePasswordVisibility = (id: string) => {
        setVisiblePasswords((prev) => ({
            ...prev,
            [id]: !prev[id],
        }));
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
                    <div className={styles.subHeading}>Configure user permissions, roles, and access credentials.</div>
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
                <div className={styles.loading}>Loading user registry...</div>
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
                                        <td colSpan={7} style={{ textAlign: "center", padding: "2rem", color: "#94a3b8" }}>
                                            No users matched your criteria.
                                        </td>
                                    </tr>
                                ) : (
                                    filteredUsers.map((u, index) => {
                                        const isSelf = u.id === currentLoggedInUserId;
                                        const isLastAdmin = u.role === "Admin" && adminCount <= 1;

                                        return (
                                            <tr key={u.id}>
                                                <td>{index + 1}</td>
                                                <td>
                                                    <strong>{u.fullName || u.username || "—"}</strong>
                                                </td>
                                                <td>{u.email}</td>
                                                <td>{getRoleBadge(u.role)}</td>
                                                <td>{getStatusStyle(u.status)}</td>
                                                <td>
                                                    <div className={styles.passwordField}>
                                                        <span>
                                                            {visiblePasswords[u.id] ? u.password || "••••••••" : "••••••••"}
                                                        </span>
                                                        <button
                                                            className={styles.toggleBtn}
                                                            onClick={() => togglePasswordVisibility(u.id)}
                                                            title="Toggle password view"
                                                        >
                                                            {visiblePasswords[u.id] ? "🙈" : "👁️"}
                                                        </button>
                                                    </div>
                                                </td>
                                                <td>
                                                    <div className={styles.actionCell}>
                                                        <button
                                                            className={styles.editBtn}
                                                            onClick={() => handleEditUser(u)}
                                                        >
                                                            Edit
                                                        </button>
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
                                                            Delete
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        );
                                    })
                                )}
                            </tbody>
                        </table>
                    </div>

                    <div className={styles.footer}>
                        Showing {filteredUsers.length} of {users.length} users
                    </div>
                </>
            )}

            <UserModal
                isOpen={isModalOpen}
                onClose={() => setIsModalOpen(false)}
                onSave={handleSaveUser}
                initialData={editingUser}
                isLastAdmin={editingUser?.role === "Admin" && adminCount <= 1}
            />
        </div>
    );
}