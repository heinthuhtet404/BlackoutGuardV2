import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ZoneTreeView } from "./ZoneTreeView";
import { LoadForm } from "./LoadForm";
import { useZones } from "../../hooks/useZones";
import { useToast } from "../../components/ui/toastContext";
import { Button } from "../../components/ui/Button";
import { Input } from "../../components/ui/Input";
import { post, del } from "../../api/apiClient";
import type { ZoneType } from "../../types/zone";
import {
    Layers,
    Plus,
    Edit,
    Trash2,
    Building,
    Home,
    Server,
    Grid,
    ArrowRight,
    Loader2,
    CheckCircle,
    XCircle,
} from "lucide-react";
import styles from "./TopologyConfigPage.module.css";

interface FlatZone {
    id: string;
    name: string;
}

function flattenNames(zones: ReturnType<typeof useZones>["data"]): FlatZone[] {
    if (!zones) return [];
    const result: FlatZone[] = [];
    const walk = (nodes: NonNullable<typeof zones>) => {
        for (const zone of nodes) {
            result.push({ id: zone.id, name: zone.name });
            walk(zone.children);
        }
    };
    walk(zones);
    return result;
}

export function TopologyConfigPage() {
    const { data: zones } = useZones();
    const { showToast } = useToast();
    const queryClient = useQueryClient();

    const [zoneName, setZoneName] = useState("");
    const [zoneType, setZoneType] = useState<ZoneType>("building");
    const [zoneParentId, setZoneParentId] = useState("");
    const [creatingZone, setCreatingZone] = useState(false);

    const [editLoadId, setEditLoadId] = useState<string | null>(null);
    const [loadFormKey, setLoadFormKey] = useState(0);

    const flatZones = flattenNames(zones);

    const createZoneMutation = useMutation({
        mutationFn: () =>
            post("/zones", {
                name: zoneName,
                type: zoneType,
                parentZoneId: zoneParentId === "" ? null : zoneParentId,
            }),
        onSuccess: () => {
            showToast(`Zone "${zoneName}" created.`);
            setZoneName("");
            setZoneParentId("");
            void queryClient.invalidateQueries({ queryKey: ["zones"] });
        },
        onError: (err: unknown) => {
            showToast(err instanceof Error ? err.message : "Failed to create zone.", "error");
        },
    });

    const deleteLoadMutation = useMutation({
        mutationFn: (loadId: string) => del(`/loads/${loadId}`),
        onSuccess: () => {
            showToast("Load deleted successfully.");
            setEditLoadId(null);
            setLoadFormKey((key) => key + 1);
            void queryClient.invalidateQueries({ queryKey: ["zones"] });
        },
        onError: (err: unknown) => {
            showToast(err instanceof Error ? err.message : "Failed to delete load.", "error");
        },
    });

    const handleCreateZone = (event: FormEvent) => {
        event.preventDefault();
        if (!zoneName.trim()) return;
        setCreatingZone(true);
        createZoneMutation.mutate(undefined, {
            onSettled: () => setCreatingZone(false),
        });
    };

    const handleDeleteLoad = () => {
        if (!editLoadId) return;
        if (window.confirm("Are you sure you want to delete this load?")) {
            deleteLoadMutation.mutate(editLoadId);
        }
    };

    return (
        <div className={styles.page}>
            <div className={styles.pageHeader}>
                <div className={styles.headerLeft}>
                    <div className={styles.headerIconWrapper}>
                        <Layers size={28} className={styles.headerIcon} />
                    </div>
                    <div>
                        <h1 className={styles.heading}>Topology Config</h1>
                        <p className={styles.headingSub}>Manage zones and loads hierarchy</p>
                    </div>
                </div>
                <div className={styles.headerBadge}>
                    <Grid size={14} />
                    <span>{zones?.length || 0} Zones</span>
                </div>
            </div>

            <div className={styles.columns}>
                {/* Left Column - Zone Hierarchy */}
                <section className={styles.column}>
                    <div className={styles.columnHeader}>
                        <Home size={18} className={styles.columnIcon} />
                        <h2 className={styles.sectionTitle}>Zone Hierarchy</h2>
                    </div>

                    <form className={styles.zoneForm} onSubmit={handleCreateZone} noValidate>
                        <div className={styles.formFields}>
                            <div className={styles.formField}>
                                <Input
                                    label="Zone Name"
                                    value={zoneName}
                                    onChange={(event) => setZoneName(event.target.value)}
                                    required
                                    data-testid="zone-name-input"
                                    placeholder="e.g., Building A"
                                />
                            </div>
                            <div className={styles.formField}>
                                <div className={styles.field}>
                                    <label className={styles.label} htmlFor="zone-type">
                                        Zone Type
                                    </label>
                                    <select
                                        id="zone-type"
                                        className={styles.select}
                                        value={zoneType}
                                        onChange={(event) => setZoneType(event.target.value as ZoneType)}
                                        data-testid="zone-type-select"
                                    >
                                        <option value="building">🏢 Building</option>
                                        <option value="floor">📐 Floor</option>
                                        <option value="room">🚪 Room</option>
                                    </select>
                                </div>
                            </div>
                            <div className={styles.formField}>
                                <div className={styles.field}>
                                    <label className={styles.label} htmlFor="zone-parent">
                                        Parent Zone
                                    </label>
                                    <select
                                        id="zone-parent"
                                        className={styles.select}
                                        value={zoneParentId}
                                        onChange={(event) => setZoneParentId(event.target.value)}
                                        data-testid="zone-parent-select"
                                    >
                                        <option value="">None (Root)</option>
                                        {flatZones.map((zone) => (
                                            <option key={zone.id} value={zone.id}>
                                                {zone.name}
                                            </option>
                                        ))}
                                    </select>
                                </div>
                            </div>
                            <div className={styles.formAction}>
                                <button
                                    type="submit"
                                    className={styles.createBtn}
                                    disabled={creatingZone || !zoneName.trim()}
                                    data-testid="zone-create-button"
                                >
                                    {creatingZone ? (
                                        <>
                                            <Loader2 size={16} className={styles.spinning} />
                                            Creating...
                                        </>
                                    ) : (
                                        <>
                                            <Plus size={16} />
                                            Create Zone
                                        </>
                                    )}
                                </button>
                            </div>
                        </div>
                    </form>

                    <div className={styles.treeWrapper}>
                        <div className={styles.treeHeader}>
                            <Server size={16} className={styles.treeIcon} />
                            <span>Zone Tree</span>
                            <span className={styles.treeCount}>{zones?.length || 0} zones</span>
                        </div>
                        <div className={styles.tree}>
                            <ZoneTreeView />
                        </div>
                    </div>
                </section>

                {/* Right Column - Loads */}
                <section className={styles.column}>
                    <div className={styles.columnHeader}>
                        <Grid size={18} className={styles.columnIcon} />
                        <h2 className={styles.sectionTitle}>Loads</h2>
                    </div>

                    {editLoadId ? (
                        <div className={styles.editBanner} data-testid="edit-banner">
                            <div className={styles.editBannerLeft}>
                                <Edit size={16} className={styles.editIcon} />
                                <span>Editing load <strong>{editLoadId.slice(0, 8)}</strong>…</span>
                            </div>
                            <div className={styles.editBannerActions}>
                                <button
                                    className={styles.newBtn}
                                    onClick={() => {
                                        setEditLoadId(null);
                                        setLoadFormKey((key) => key + 1);
                                    }}
                                    data-testid="new-load-button"
                                >
                                    <Plus size={14} />
                                    New Load
                                </button>
                                <button
                                    className={styles.deleteBtn}
                                    onClick={handleDeleteLoad}
                                    data-testid="delete-load-button"
                                >
                                    <Trash2 size={14} />
                                    Delete Load
                                </button>
                            </div>
                        </div>
                    ) : (
                        <div className={styles.createHint}>
                            <Plus size={20} className={styles.hintIcon} />
                            <span>Create a new load using the form below</span>
                        </div>
                    )}

                    <div className={styles.loadFormWrapper}>
                        <LoadForm
                            key={loadFormKey}
                            loadId={editLoadId ?? undefined}
                            onCreated={(id) => setEditLoadId(id)}
                            onSaved={() => {
                                showToast(editLoadId ? "Load updated." : "Load created.");
                                void queryClient.invalidateQueries({ queryKey: ["zones"] });
                            }}
                        />
                    </div>
                </section>
            </div>
        </div>
    );
}