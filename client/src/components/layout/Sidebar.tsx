import { useState } from "react";
import { NavLink, useNavigate } from "react-router-dom";
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
    const { user, logout } = useAuth(); // useAuth ထဲမှ logout ကို ယူသုံးထားသည်
    const navigate = useNavigate();

    // Alert Box (Modal) ဖွင့်/ပိတ် ထိန်းချုပ်ရန် state
    const [showLogoutModal, setShowLogoutModal] = useState(false);

    const visibleItems = NAV_ITEMS.filter(
        (item) => role !== null && item.roles.includes(role)
    );

    const displayName = getUserDisplayName(user);
    const userInitial = getUserInitial(user);

    // Logout ထွက်မည်ဟု အတည်ပြုလိုက်လျှင် အလုပ်လုပ်မည့် Function
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

                    {/* v2.0.0 နေရာတွင် ပြင်ဆင်ထားသော Logout Button */}
                    <button
                        className={styles.logoutBtn}
                        onClick={() => setShowLogoutModal(true)}
                        type="button"
                    >
                        <span className={styles.logoutIcon}>🚪</span>
                        <span>Logout</span>
                    </button>
                </div>
            </aside>

            {/* Custom Logout Confirmation Alert Box / Modal */}
            {showLogoutModal && (
                <div className={styles.modalOverlay} onClick={() => setShowLogoutModal(false)}>
                    <div className={styles.modalContent} onClick={(e) => e.stopPropagation()}>
                        <div className={styles.modalHeader}>
                            <span className={styles.modalWarnIcon}>⚠️</span>
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