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

    return (
        <div className={styles.page}>
            <h1 className={styles.heading}>Rules Engine</h1>

            <form className={styles.form} onSubmit={handleCreateRule}>
                <h3 className={styles.formTitle}>Create Shedding Rule</h3>
                <div className={styles.formRow}>
                    <input
                        className={styles.input}
                        placeholder="Rule Name"
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                        required
                    />
                    <input
                        className={styles.input}
                        placeholder="Trigger (e.g. Frequency < 49.5)"
                        value={triggerCondition}
                        onChange={(e) => setTriggerCondition(e.target.value)}
                        required
                    />
                    <input
                        className={styles.input}
                        placeholder="Action (e.g. Shed P3 Loads)"
                        value={action}
                        onChange={(e) => setAction(e.target.value)}
                        required
                    />
                    <Button type="submit">Add Rule</Button>
                </div>
            </form>

            <h3 className={styles.sectionTitle}>Configured Rules</h3>
            {loading ? (
                <p className={styles.loading}>Loading rules...</p>
            ) : rules.length === 0 ? (
                <p className={styles.empty}>No rules defined yet.</p>
            ) : (
                <div className={styles.tableWrapper}>
                    <table className={styles.table}>
                        <thead>
                            <tr>
                                <th>Name</th>
                                <th>Trigger</th>
                                <th>Action</th>
                                <th>Status</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {rules.map((rule) => (
                                <tr key={rule.id}>
                                    <td>{rule.name}</td>
                                    <td>{rule.triggerCondition}</td>
                                    <td>{rule.action}</td>
                                    <td>
                                        <span
                                            className={
                                                rule.enabled ? styles.badgeEnabled : styles.badgeDisabled
                                            }
                                        >
                                            {rule.enabled ? "Enabled" : "Disabled"}
                                        </span>
                                    </td>
                                    <td>
                                        <Button variant="danger" onClick={() => void handleDeleteRule(rule.id)}>
                                            Delete
                                        </Button>
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