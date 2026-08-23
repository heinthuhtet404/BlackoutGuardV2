import { useEffect, useState } from "react";
import { get } from "../api/apiClient";
import { useToast } from "../components/ui/toastContext";

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
        <div style={{ padding: "1.5rem", maxWidth: "900px", margin: "0 auto" }}>
            <h1>User Management</h1>

            {loading ? (
                <p>Loading user list...</p>
            ) : (
                <table style={{ width: "100%", textAlign: "left", borderCollapse: "collapse", marginTop: "1rem" }}>
                    <thead>
                        <tr style={{ borderBottom: "2px solid #555" }}>
                            <th>Username</th>
                            <th>Email</th>
                            <th>Role</th>
                        </tr>
                    </thead>
                    <tbody>
                        {users.map((user) => (
                            <tr key={user.id} style={{ borderBottom: "1px solid #333" }}>
                                <td>{user.username}</td>
                                <td>{user.email}</td>
                                <td>{user.role}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}