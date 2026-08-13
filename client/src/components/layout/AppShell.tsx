import { Outlet, Navigate } from "react-router-dom";
import { useAuth } from "../../auth/authTypes";
import { Sidebar } from "./Sidebar";
import styles from "./AppShell.module.css";

export function AppShell() {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className={styles.loading} role="status">
        Loading...
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  return (
    <div className={styles.shell}>
      <Sidebar />
      <main className={styles.content}>
        <Outlet />
      </main>
    </div>
  );
}
