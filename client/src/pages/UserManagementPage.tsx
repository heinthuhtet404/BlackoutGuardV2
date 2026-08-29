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
    onSave: (userData: Partial<User> & { sendWelcomeEmail?: boolean }) => Promise<User | undefined>;
}

interface DeleteModalProps {
    isOpen: boolean;
    user: User | null;
    onClose: () => void;
    onConfirm: () => Promise<void>;
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

// ----------------------------------------------------
// Custom Delete Confirmation Modal Component
// ----------------------------------------------------
function DeleteConfirmationModal({ isOpen, user, onClose, onConfirm }: DeleteModalProps) {
    const [deleting, setDeleting] = useState(false);

    if (!isOpen || !user) return null;

    const displayName = user.fullName || user.username;

    const handleConfirm = async () => {
        setDeleting(true);
        try {
            await onConfirm();
        } finally {
            setDeleting(false);
            onClose();
        }
    };

    return (
        <div className={styles.modalOverlay} role="dialog" aria-modal="true">
            <div className={styles.modal}>
                {/* Header with icon */}
                <div className={styles.modalHeader}>
                    <div className={styles.modalIconWrapper}>
                        <span className={styles.modalIcon}>🗑️</span>
                    </div>
                    <h2 className={styles.modalTitle}>Delete User</h2>
                    <button
                        type="button"
                        className={styles.modalClose}
                        onClick={onClose}
                        disabled={deleting}
                    >
                        ✕
                    </button>
                </div>

                {/* Body */}
                <div className={styles.modalBody}>
                    <p className={styles.modalText}>
                        Are you sure you want to permanently delete this user account?
                    </p>

                    <div className={styles.deleteTargetCard}>
                        <div className={styles.deleteTargetAvatar}>
                            {displayName?.[0] || user.email[0]}
                        </div>
                        <div className={styles.deleteTargetInfo}>
                            <div className={styles.deleteTargetName}>
                                {displayName || user.email}
                            </div>
                            <div className={styles.deleteTargetEmail}>{user.email}</div>
                        </div>
                    </div>

                    <div className={styles.warningBanner}>
                        <span className={styles.warningIcon}>⚠️</span>
                        <span>This action cannot be undone.</span>
                    </div>
                </div>

                {/* Footer Actions */}
                <div className={styles.modalFooter}>
                    <button
                        type="button"
                        className={styles.cancelBtn}
                        onClick={onClose}
                        disabled={deleting}
                    >
                        Cancel
                    </button>
                    <button
                        type="button"
                        className={styles.deleteConfirmBtn}
                        onClick={handleConfirm}
                        disabled={deleting}
                    >
                        {deleting ? (
                            <>
                                <span className={styles.spinnerSmall}></span>
                                Deleting...
                            </>
                        ) : (
                            "Yes, Delete"
                        )}
                    </button>
                </div>
            </div>
        </div>
    );
}

// ----------------------------------------------------
// Create User Modal Component
// ----------------------------------------------------
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
                    <div className={styles.modalIconWrapper}>
                        <span className={styles.modalIcon}>👤</span>
                    </div>
                    <h2 className={styles.modalTitle}>Create New User</h2>
                    <button type="button" className={styles.modalClose} onClick={onClose}>
                        ✕
                    </button>
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
                            <option value="Admin">🛡️ Admin</option>
                            <option value="Operator">⚙️ Operator</option>
                            <option value="Viewer">👁️ Viewer</option>
                        </select>
                    </div>

                    {/* <div className={styles.checkboxGroup}>
                        <input
                            type="checkbox"
                            id="welcomeEmail"
                            checked={sendWelcomeEmail}
                            onChange={(e) => setSendWelcomeEmail(e.target.checked)}
                        />
                        <label htmlFor="welcomeEmail">Send Welcome Email (with credentials)</label>
                    </div> */}

                    <div className={styles.modalFooter}>
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

// ----------------------------------------------------
// Main User Management Page Component
// ----------------------------------------------------
export function UserManagementPage() {
    const [users, setUsers] = useState<User[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState("");
    const [roleFilter, setRoleFilter] = useState<string>("All");
    const [visiblePasswords, setVisiblePasswords] = useState<{ [key: string]: boolean }>({});

    // Delete Modal state
    const [userToDelete, setUserToDelete] = useState<User | null>(null);

    // Demo environment state persistence
    const [createdPasswords, setCreatedPasswords] = useState<{ [userId: string]: string }>(() => {
        try {
            const saved = localStorage.getItem("demo_created_passwords");
            return saved ? JSON.parse(saved) : {};
        } catch {
            return {};
        }
    });

    useEffect(() => {
        try {
            localStorage.setItem("demo_created_passwords", JSON.stringify(createdPasswords));
        } catch (err) {
            console.warn("Failed to persist created passwords to LocalStorage.", err);
        }
    }, [createdPasswords]);

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

    const openDeleteModal = (user: User) => {
        if (user.id === currentLoggedInUserId) {
            showToast("Self-Delete Protection: You cannot delete your own logged-in account.", "error");
            return;
        }

        if (user.role === "Admin" && adminCount <= 1) {
            showToast("Last Admin Protection: Cannot delete the remaining active Administrator.", "error");
            return;
        }

        setUserToDelete(user);
    };

    const confirmDeleteUser = async () => {
        if (!userToDelete) return;

        try {
            await del(`/users/${userToDelete.id}`);
            setUsers((prev) => prev.filter((u) => u.id !== userToDelete.id));

            setCreatedPasswords((prev) => {
                const updated = { ...prev };
                delete updated[userToDelete.id];
                return updated;
            });

            showToast(`User ${userToDelete.email} deleted successfully.`, "success");
            await logAuditTrail("DELETE", userToDelete.id, `Deleted account ${userToDelete.email}`);
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to delete user from database.", "error");
        }
    };

    const handleSaveUser = async (userData: Partial<User> & { sendWelcomeEmail?: boolean }): Promise<User | undefined> => {
        try {
            const created = await post<User>("/users", userData);
            if (created) {
                if (userData.password && created.id) {
                    setCreatedPasswords((prev) => ({
                        ...prev,
                        [created.id]: userData.password!,
                    }));
                }

                setUsers((prev) => [...prev, created]);
                showToast(`New user ${created.email} created successfully.`, "success");
                await logAuditTrail("CREATE", created.id, `Created account ${created.email} with role ${created.role}`);
                return created;
            } else {
                await fetchUsers();
            }
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to create user", "error");
        }
        return undefined;
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
                                    <th>Password</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredUsers.length === 0 ? (
                                    <tr>
                                        <td colSpan={6} className={styles.emptyRow}>
                                            <span className={styles.emptyIcon}>📭</span>
                                            No users matched your criteria.
                                        </td>
                                    </tr>
                                ) : (
                                    filteredUsers.map((u, index) => {
                                        const isSelf = u.id === currentLoggedInUserId;
                                        const isLastAdmin = u.role === "Admin" && adminCount <= 1;
                                        const isPasswordVisible = visiblePasswords[u.id];
                                        const displayPassword = createdPasswords[u.id] || u.password;

                                        return (
                                            <tr key={u.id} className={isSelf ? styles.selfRow : ""}>
                                                <td>{index + 1}</td>
                                                <td>
                                                    <span className={styles.userName}>{u.fullName || u.username || "—"}</span>
                                                    {isSelf && <span className={styles.selfBadge}>You</span>}
                                                </td>
                                                <td className={styles.userEmail}>{u.email}</td>
                                                <td>{getRoleBadge(u.role)}</td>
                                                <td>
                                                    <div className={styles.passwordCell}>
                                                        <span className={styles.passwordText}>
                                                            {isPasswordVisible
                                                                ? displayPassword || "[Hashed]"
                                                                : "••••••••"}
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
                                                        onClick={() => openDeleteModal(u)}
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

            <DeleteConfirmationModal
                isOpen={Boolean(userToDelete)}
                user={userToDelete}
                onClose={() => setUserToDelete(null)}
                onConfirm={confirmDeleteUser}
            />
        </div>
    );
}