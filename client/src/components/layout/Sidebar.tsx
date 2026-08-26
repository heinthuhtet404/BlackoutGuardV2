import { NavLink } from "react-router-dom";
import { useRole, getUserDisplayName, getUserInitial, useAuth } from "../../auth/authTypes";
import type { Role } from "../../auth/authTypes";
import styles from "./Sidebar.module.css";

interface NavItem {
    label: string;
    path: string;
    icon: string;
    roles: Role[];
}

const NAV_ITEMS: NavItem[] = [
    { label: "Live Overview", path: "/overview", icon: "⚡", roles: ["Admin", "Operator", "Viewer"] },
    { label: "Topology Config", path: "/topology", icon: "🗺️", roles: ["Admin", "Operator", "Viewer"] },
    { label: "Rules Engine", path: "/rules", icon: "⚙️", roles: ["Admin", "Operator"] },
    { label: "Simulator Panel", path: "/simulator", icon: "🎛️", roles: ["Admin"] },
    { label: "Audit Logs", path: "/audit", icon: "📜", roles: ["Admin", "Operator", "Viewer"] },
    { label: "User Management", path: "/users", icon: "👥", roles: ["Admin"] },
];

export function Sidebar() {
    const { role } = useRole();
    const { user } = useAuth();

    const visibleItems = NAV_ITEMS.filter(
        (item) => role !== null && item.roles.includes(role)
    );

    const displayName = getUserDisplayName(user);
    const userInitial = getUserInitial(user);

    return (
        <aside className={styles.sidebar} aria-label="Main navigation">
            {/* Brand */}
            <div className={styles.brand}>
                <span className={styles.brandIcon}>⚡</span>
                <span className={styles.brandName}>BlackoutGuard</span>
            </div>

            {/* Navigation */}
            <nav className={styles.nav}>
                <ul className={styles.navList}>
                    {visibleItems.map((item) => (
                        <li key={item.path}>
                            <NavLink
                                to={item.path}
                                className={({ isActive }) =>
                                    isActive ? `${styles.navLink} ${styles.active}` : styles.navLink
                                }
                            >
                                <span className={styles.icon} aria-hidden="true">
                                    {item.icon}
                                </span>
                                <span className={styles.label}>{item.label}</span>
                            </NavLink>
                        </li>
                    ))}
                </ul>
            </nav>

            {/* Footer */}
            <div className={styles.footer}>
                <div className={styles.userInfo}>
                    <div className={styles.userAvatar}>
                        {userInitial}
                    </div>
                    <div className={styles.userDetails}>
                        <span className={styles.userName}>
                            {displayName}
                        </span>
                        <span className={styles.userRole}>
                            {role || "Viewer"}
                        </span>
                    </div>
                </div>
                <button className={styles.versionBtn} disabled>
                    v2.0.0
                </button>
            </div>
        </aside>
    );
}