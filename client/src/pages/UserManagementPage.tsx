import { useEffect, useState } from "react";
import { get } from "../api/apiClient";
import { useToast } from "../components/ui/toastContext";
import styles from "./UserManagementPage.module.css";

interface User {
    id: string;
    username: string;
    email: string;
    role: string;
}

export function UserManagementPage() {
    const [users, setUsers] = useState<User[]>([]);
    const [loading, setLoading] = useState(true);
    const { showToast } = useToast();

    useEffect(() => {
        async function fetchUsers() {
            try {
                setLoading(true);
                const data = await get<User[]>("/users");
                setUsers(data);
            } catch (err) {
                showToast(err instanceof Error ? err.message : "Failed to load users", "error");
            } finally {
                setLoading(false);
            }
        }
        void fetchUsers();
    }, []);

    return (
        <div className={styles.page}>
            <h1 className={styles.heading}>User Management</h1>

            {loading ? (
                <p className={styles.loading}>Loading user list...</p>
            ) : (
                <div className={styles.tableWrapper}>
                    <table className={styles.table}>
                        <thead>
                            <tr>
                                <th>Username</th>
                                <th>Email</th>
                                <th>Role</th>
                            </tr>
                        </thead>
                        <tbody>
                            {users.map((user) => (
                                <tr key={user.id}>
                                    <td>{user.username}</td>
                                    <td>{user.email}</td>
                                    <td>
                                        <span className={styles.badge}>{user.role}</span>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </div>
    );
}