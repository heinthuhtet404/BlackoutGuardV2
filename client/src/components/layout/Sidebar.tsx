import { NavLink } from "react-router-dom";
import { useRole } from "../../auth/useRole";
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

  const visibleItems = NAV_ITEMS.filter(
    (item) => role !== null && item.roles.includes(role)
  );

  return (
    <nav className={styles.sidebar} aria-label="Main navigation">
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
              <span>{item.label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
