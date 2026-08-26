import { useState, type ReactNode } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useZones } from "../../hooks/useZones";
import { useRole } from "../../auth/useRole";
import { useToast } from "../../components/ui/toastContext";
import { del, put } from "../../api/apiClient";
import type { ZoneTree } from "../../types/zone";
import styles from "./ZoneTreeView.module.css";

function typeBadgeClass(type: string): string {
    switch (type) {
        case "building":
            return styles.badgeBuilding;
        case "floor":
            return styles.badgeFloor;
        case "room":
            return styles.badgeRoom;
        default:
            return styles.badgeDefault;
    }
}

interface ZoneNodeProps {
    zone: ZoneTree;
    depth: number;
    isAdmin: boolean;
    onUpdateZone: (zoneId: string, name: string, type: string) => void;
    onDeleteZone: (zoneId: string, name: string) => void;
}

function ZoneNode({
    zone,
    depth,
    isAdmin,
    onUpdateZone,
    onDeleteZone,
}: ZoneNodeProps) {
    const [expanded, setExpanded] = useState(true);
    const [isEditing, setIsEditing] = useState(false);
    const [editName, setEditName] = useState(zone.name);

    const hasChildren = zone.children.length > 0;

    const handleSaveEdit = () => {
        if (!editName.trim()) return;
        // Pass zone.type along with zone.id and editName
        onUpdateZone(zone.id, editName.trim(), zone.type);
        setIsEditing(false);
    };

    return (
        <div className={styles.nodeBlock} role="treeitem">
            <div
                className={styles.nodeRow}
                style={{ paddingLeft: `${depth * 1.25}rem` }}
                data-testid={`zone-node-${zone.id}`}
            >
                <button
                    type="button"
                    className={styles.toggle}
                    onClick={() => setExpanded((current) => !current)}
                    disabled={!hasChildren}
                    aria-label={expanded ? "Collapse" : "Expand"}
                    data-testid={`toggle-${zone.id}`}
                >
                    {hasChildren ? (expanded ? "▾" : "▸") : "·"}
                </button>

                {isEditing ? (
                    <div className={styles.editContainer}>
                        <input
                            type="text"
                            className={styles.editInput}
                            value={editName}
                            onChange={(e) => setEditName(e.target.value)}
                            autoFocus
                        />
                        <button
                            type="button"
                            className={styles.actionBtnBtn}
                            onClick={handleSaveEdit}
                        >
                            ✓
                        </button>
                        <button
                            type="button"
                            className={styles.actionBtnBtn}
                            onClick={() => {
                                setEditName(zone.name);
                                setIsEditing(false);
                            }}
                        >
                            ✕
                        </button>
                    </div>
                ) : (
                    <>
                        <span className={styles.zoneName}>{zone.name}</span>
                        <span className={`${styles.badge} ${typeBadgeClass(zone.type)}`}>
                            {zone.type}
                        </span>
                        <span className={styles.childCount}>
                            {hasChildren
                                ? `${zone.children.length} child${zone.children.length === 1 ? "" : "ren"}`
                                : ""}
                        </span>

                        {isAdmin && (
                            <div className={styles.actionGroup}>
                                <button
                                    type="button"
                                    className={styles.iconBtn}
                                    onClick={() => setIsEditing(true)}
                                    title="Edit Zone"
                                >
                                    ✏️
                                </button>
                                <button
                                    type="button"
                                    className={styles.iconBtnDanger}
                                    onClick={() => onDeleteZone(zone.id, zone.name)}
                                    title="Delete Zone"
                                >
                                    🗑️
                                </button>
                            </div>
                        )}
                    </>
                )}
            </div>

            {expanded &&
                zone.children.map((child) => (
                    <ZoneNode
                        key={child.id}
                        zone={child}
                        depth={depth + 1}
                        isAdmin={isAdmin}
                        onUpdateZone={onUpdateZone}
                        onDeleteZone={onDeleteZone}
                    />
                ))}
        </div>
    );
}

export function ZoneTreeView(): ReactNode {
    const { data, isLoading, error, isError } = useZones();
    const { is } = useRole();
    const { showToast } = useToast();
    const queryClient = useQueryClient();
    const isAdmin = is("Admin");

    const updateZoneMutation = useMutation({
        mutationFn: ({ zoneId, name, type }: { zoneId: string; name: string; type: string }) =>
            // Included `type` in the API payload
            put(`/zones/${zoneId}`, { name, type }),
        onSuccess: () => {
            showToast("Zone updated successfully.");
            void queryClient.invalidateQueries({ queryKey: ["zones"] });
        },
        onError: (err: unknown) => {
            showToast(
                err instanceof Error ? err.message : "Failed to update zone.",
                "error"
            );
        },
    });

    const deleteZoneMutation = useMutation({
        mutationFn: (zoneId: string) => del(`/zones/${zoneId}`),
        onSuccess: () => {
            showToast("Zone deleted successfully.");
            void queryClient.invalidateQueries({ queryKey: ["zones"] });
        },
        onError: (err: unknown) => {
            showToast(
                err instanceof Error ? err.message : "Failed to delete zone.",
                "error"
            );
        },
    });

    const handleDeleteZone = (zoneId: string, name: string) => {
        if (
            window.confirm(
                `Are you sure you want to delete zone "${name}"? Sub-zones/loads may be affected.`
            )
        ) {
            deleteZoneMutation.mutate(zoneId);
        }
    };

    if (isLoading) {
        return <div role="status">Loading zones...</div>;
    }

    if (isError) {
        return (
            <div role="alert">
                Failed to load zones:{" "}
                {error instanceof Error ? error.message : "unknown error"}
            </div>
        );
    }

    const tree = data ?? [];

    return (
        <div className={styles.treeContainer} role="tree" data-testid="zone-tree">
            {tree.length === 0 && <p>No zones configured for this facility.</p>}
            {tree.map((zone) => (
                <ZoneNode
                    key={zone.id}
                    zone={zone}
                    depth={0}
                    isAdmin={isAdmin}
                    onUpdateZone={(zoneId, name, type) =>
                        updateZoneMutation.mutate({ zoneId, name, type })
                    }
                    onDeleteZone={handleDeleteZone}
                />
            ))}
        </div>
    );
}