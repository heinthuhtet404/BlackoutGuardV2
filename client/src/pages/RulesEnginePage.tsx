import { useEffect, useState } from "react";
import { get, post, del } from "../api/apiClient";
import { useToast } from "../components/ui/toastContext";
import { Button } from "../components/ui/Button";

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
        <div style={{ padding: "1.5rem", maxWidth: "900px", margin: "0 auto" }}>
            <h1>Rules Engine</h1>

            {/* New Rule Form */}
            <form onSubmit={handleCreateRule} style={{ marginBottom: "2rem", display: "flex", flexDirection: "column", gap: "1rem" }}>
                <h3>Create Shedding Rule</h3>
                <input placeholder="Rule Name" value={name} onChange={(e) => setName(e.target.value)} required />
                <input placeholder="Trigger Condition (e.g. Frequency < 49.5)" value={triggerCondition} onChange={(e) => setTriggerCondition(e.target.value)} required />
                <input placeholder="Action (e.g. Shed P3 Loads)" value={action} onChange={(e) => setAction(e.target.value)} required />
                <Button type="submit">Add Rule</Button>
            </form>

            {/* Rules List */}
            <h3>Configured Rules</h3>
            {loading ? (
                <p>Loading rules...</p>
            ) : rules.length === 0 ? (
                <p>No rules defined yet.</p>
            ) : (
                <table style={{ width: "100%", textAlign: "left", borderCollapse: "collapse" }}>
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
                            <tr key={rule.id} style={{ borderBottom: "1px solid #333" }}>
                                <td>{rule.name}</td>
                                <td>{rule.triggerCondition}</td>
                                <td>{rule.action}</td>
                                <td>{rule.enabled ? "Enabled" : "Disabled"}</td>
                                <td>
                                    <Button variant="danger" onClick={() => void handleDeleteRule(rule.id)}>Delete</Button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}