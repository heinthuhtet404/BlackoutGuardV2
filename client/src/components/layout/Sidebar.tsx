import { useState } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import { useRole, getUserDisplayName, getUserInitial, useAuth } from "../../auth/authTypes";
import type { Role } from "../../auth/authTypes";
import {
    Zap,
    Network,
    Sliders,
    SlidersHorizontal,
    ClipboardList,
    Users,
    LogOut,
    AlertTriangle,
    Building2,
    BarChart3,
    LineChart,
    PieChart,
    Activity,
} from "lucide-react";
import styles from "./Sidebar.module.css";

interface NavItem {
    label: string;
    path: string;
    icon: React.ReactNode;
    roles: Role[];
}

const NAV_ITEMS: NavItem[] = [
    { label: "Live Overview", path: "/overview", icon: <Zap size={20} />, roles: ["Admin", "Operator", "Viewer"] },
    {
        label: "Dashboard Analytics",
        path: "/dashboard",
        icon: <BarChart3 size={20} />,
        roles: ["Admin", "Operator"]
    },
    { label: "Topology Config", path: "/topology", icon: <Network size={20} />, roles: ["Admin", "Operator", "Viewer"] },
    { label: "Rules Engine", path: "/rules", icon: <Sliders size={20} />, roles: ["Admin", "Operator"] },
    { label: "Simulator Panel", path: "/simulator", icon: <SlidersHorizontal size={20} />, roles: ["Admin"] },
    { label: "Audit Logs", path: "/audit", icon: <ClipboardList size={20} />, roles: ["Admin", "Operator", "Viewer"] },
    { label: "User Management", path: "/users", icon: <Users size={20} />, roles: ["Admin"] },
];

export function Sidebar() {
    const { role } = useRole();
    const { user, logout } = useAuth();
    const navigate = useNavigate();

    const [showLogoutModal, setShowLogoutModal] = useState(false);

    const visibleItems = NAV_ITEMS.filter(
        (item) => role !== null && item.roles.includes(role)
    );

    const displayName = getUserDisplayName(user);
    const userInitial = getUserInitial(user);

    // DB မှ OrganizationName သို့မဟုတ် fallback အနေဖြင့် BlackoutGuard
    const organizationName = user?.organizationName || (user as Record<string, unknown>)?.OrganizationName || "BlackoutGuard";

    const handleConfirmLogout = async () => {
        setShowLogoutModal(false);
        if (logout) {
            await logout();
        }
        navigate("/login", { replace: true });
    };

    return (
        <>
            <aside className={styles.sidebar} aria-label="Main navigation">
                {/* Brand: Organization Name ပြသခြင်း */}
                <div className={styles.brand}>
                    <span className={styles.brandIcon}>
                        <Building2 size={22} />
                    </span>
                    <span className={styles.brandName} title={String(organizationName)}>
                        {organizationName}
                    </span>
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

                    <button
                        className={styles.logoutBtn}
                        onClick={() => setShowLogoutModal(true)}
                        type="button"
                    >
                        <span className={styles.logoutIcon}>
                            <LogOut size={18} />
                        </span>
                        <span>Logout</span>
                    </button>
                </div>
            </aside>

            {/* Custom Logout Confirmation Modal */}
            {showLogoutModal && (
                <div className={styles.modalOverlay} onClick={() => setShowLogoutModal(false)}>
                    <div className={styles.modalContent} onClick={(e) => e.stopPropagation()}>
                        <div className={styles.modalHeader}>
                            <span className={styles.modalWarnIcon}>
                                <AlertTriangle size={24} color="#f59e0b" />
                            </span>
                            <h3>Confirm Logout</h3>
                        </div>
                        <p className={styles.modalText}>
                            Are you sure you want to log out of your account?
                            You will be redirected to the login page.
                        </p>
                        <div className={styles.modalActions}>
                            <button
                                className={styles.cancelBtn}
                                onClick={() => setShowLogoutModal(false)}
                            >
                                Cancel
                            </button>
                            <button
                                className={styles.confirmBtn}
                                onClick={handleConfirmLogout}
                            >
                                Logout
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
}