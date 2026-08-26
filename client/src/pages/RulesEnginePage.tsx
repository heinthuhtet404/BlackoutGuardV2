import { useEffect, useState } from "react";
import { get, post, del } from "../api/apiClient";
import { useToast } from "../components/ui/toastContext";
import { Button } from "../components/ui/Button";
import styles from "./RulesEnginePage.module.css";

interface Rule {
    id: string;
    name: string;
    triggerCondition: string;
    action: string;
    enabled: boolean;
}

export function RulesEnginePage() {
    const [rules, setRules] = useState<Rule[]>([]);
    const [loading, setLoading] = useState(true);
    const [name, setName] = useState("");
    const [triggerCondition, setTriggerCondition] = useState("");
    const [action, setAction] = useState("");
    const { showToast } = useToast();

    const fetchRules = async () => {
        try {
            setLoading(true);
            const data = await get<Rule[]>("/rules");
            setRules(data);
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to load rules", "error");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void fetchRules();
    }, []);

    const handleCreateRule = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await post("/rules", { name, triggerCondition, action, enabled: true });
            showToast("Rule created successfully");
            setName("");
            setTriggerCondition("");
            setAction("");
            void fetchRules();
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to create rule", "error");
        }
    };

    const handleDeleteRule = async (id: string) => {
        try {
            await del(`/rules/${id}`);
            showToast("Rule deleted");
            void fetchRules();
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to delete rule", "error");
        }
    };

    const handleToggleRule = async (id: string, currentStatus: boolean) => {
        try {
            await post(`/rules/${id}/toggle`, { enabled: !currentStatus });
            showToast(`Rule ${!currentStatus ? "enabled" : "disabled"}`);
            void fetchRules();
        } catch (err) {
            showToast(err instanceof Error ? err.message : "Failed to toggle rule", "error");
        }
    };

    const enabledRules = rules.filter(r => r.enabled);
    const disabledRules = rules.filter(r => !r.enabled);

    return (
        <div className={styles.page}>
            {/* Header */}
            <div className={styles.header}>
                <div>
                    <h1 className={styles.heading}>⚙️ Rules Engine</h1>
                    <p className={styles.subheading}>Manage your load shedding automation rules</p>
                </div>
                <div className={styles.stats}>
                    <div className={styles.statItem}>
                        <span className={styles.statNumber}>{rules.length}</span>
                        <span className={styles.statLabel}>Total</span>
                    </div>
                    <div className={styles.statDivider}></div>
                    <div className={styles.statItem}>
                        <span className={`${styles.statNumber} ${styles.statEnabled}`}>{enabledRules.length}</span>
                        <span className={styles.statLabel}>Active</span>
                    </div>
                    <div className={styles.statDivider}></div>
                    <div className={styles.statItem}>
                        <span className={`${styles.statNumber} ${styles.statDisabled}`}>{disabledRules.length}</span>
                        <span className={styles.statLabel}>Inactive</span>
                    </div>
                </div>
            </div>

            {/* Create Form - Inline Compact */}
            <form className={styles.form} onSubmit={handleCreateRule}>
                <div className={styles.formInner}>
                    <div className={styles.formFields}>
                        <div className={styles.fieldGroup}>
                            <input
                                className={styles.input}
                                placeholder="Rule name..."
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                                required
                            />
                        </div>
                        <div className={styles.fieldGroup}>
                            <input
                                className={styles.input}
                                placeholder="Trigger (e.g., frequency < 49.5)"
                                value={triggerCondition}
                                onChange={(e) => setTriggerCondition(e.target.value)}
                                required
                            />
                        </div>
                        <div className={styles.fieldGroup}>
                            <input
                                className={styles.input}
                                placeholder="Action (e.g., shed P3 loads)"
                                value={action}
                                onChange={(e) => setAction(e.target.value)}
                                required
                            />
                        </div>
                    </div>
                    <button type="submit" className={styles.createBtn}>
                        + Create Rule
                    </button>
                </div>
            </form>

            {/* Rules Grid - Card Based */}
            {loading ? (
                <div className={styles.loadingState}>
                    <div className={styles.spinner}></div>
                    Loading rules...
                </div>
            ) : rules.length === 0 ? (
                <div className={styles.emptyState}>
                    <div className={styles.emptyIcon}>📋</div>
                    <h3>No rules defined yet</h3>
                    <p>Create your first rule using the form above</p>
                </div>
            ) : (
                <div className={styles.board}>
                    {/* Active Rules Column */}
                    <div className={styles.column}>
                        <div className={styles.columnHeader}>
                            <span className={styles.columnDot}></span>
                            <span className={styles.columnTitle}>Active Rules</span>
                            <span className={styles.columnCount}>{enabledRules.length}</span>
                        </div>
                        <div className={styles.columnBody}>
                            {enabledRules.length === 0 ? (
                                <div className={styles.emptyColumn}>No active rules</div>
                            ) : (
                                enabledRules.map((rule) => (
                                    <div key={rule.id} className={`${styles.card} ${styles.cardActive}`}>
                                        <div className={styles.cardHeader}>
                                            <span className={styles.cardName}>{rule.name}</span>
                                            <span className={styles.cardStatus}>● Active</span>
                                        </div>
                                        <div className={styles.cardBody}>
                                            <div className={styles.cardDetail}>
                                                <span className={styles.detailLabel}>Trigger</span>
                                                <code className={styles.detailCode}>{rule.triggerCondition}</code>
                                            </div>
                                            <div className={styles.cardDetail}>
                                                <span className={styles.detailLabel}>Action</span>
                                                <code className={styles.detailCode}>{rule.action}</code>
                                            </div>
                                        </div>
                                        <div className={styles.cardActions}>
                                            <button
                                                type="button"
                                                className={styles.toggleBtn}
                                                onClick={() => handleToggleRule(rule.id, rule.enabled)}
                                            >
                                                ⏸️ Disable
                                            </button>
                                            <button
                                                type="button"
                                                className={styles.deleteBtn}
                                                onClick={() => void handleDeleteRule(rule.id)}
                                            >
                                                🗑️
                                            </button>
                                        </div>
                                    </div>
                                ))
                            )}
                        </div>
                    </div>

                    {/* Inactive Rules Column */}
                    <div className={styles.column}>
                        <div className={styles.columnHeader}>
                            <span className={`${styles.columnDot} ${styles.dotInactive}`}></span>
                            <span className={styles.columnTitle}>Inactive Rules</span>
                            <span className={styles.columnCount}>{disabledRules.length}</span>
                        </div>
                        <div className={styles.columnBody}>
                            {disabledRules.length === 0 ? (
                                <div className={styles.emptyColumn}>No inactive rules</div>
                            ) : (
                                disabledRules.map((rule) => (
                                    <div key={rule.id} className={`${styles.card} ${styles.cardInactive}`}>
                                        <div className={styles.cardHeader}>
                                            <span className={styles.cardName}>{rule.name}</span>
                                            <span className={styles.cardStatus}>○ Inactive</span>
                                        </div>
                                        <div className={styles.cardBody}>
                                            <div className={styles.cardDetail}>
                                                <span className={styles.detailLabel}>Trigger</span>
                                                <code className={styles.detailCode}>{rule.triggerCondition}</code>
                                            </div>
                                            <div className={styles.cardDetail}>
                                                <span className={styles.detailLabel}>Action</span>
                                                <code className={styles.detailCode}>{rule.action}</code>
                                            </div>
                                        </div>
                                        <div className={styles.cardActions}>
                                            <button
                                                type="button"
                                                className={styles.toggleBtn}
                                                onClick={() => handleToggleRule(rule.id, rule.enabled)}
                                            >
                                                ▶️ Enable
                                            </button>
                                            <button
                                                type="button"
                                                className={styles.deleteBtn}
                                                onClick={() => void handleDeleteRule(rule.id)}
                                            >
                                                🗑️
                                            </button>
                                        </div>
                                    </div>
                                ))
                            )}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}