import { useState, type ReactNode } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useZones } from "../../hooks/useZones";
import { useRole } from "../../auth/useRole";
import { useToast } from "../../components/ui/toastContext";
import { put } from "../../api/apiClient";
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
  onReparent: (zoneId: string, newParentId: string) => void;
}

function ZoneNode({ zone, depth, isAdmin, onReparent }: ZoneNodeProps) {
  const [expanded, setExpanded] = useState(true);
  const [isDropTarget, setIsDropTarget] = useState(false);

  const hasChildren = zone.children.length > 0;

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDropTarget(false);
    const draggedId = event.dataTransfer.getData("text/plain");
    if (!draggedId || draggedId === zone.id) return;
    onReparent(draggedId, zone.id);
  };

  return (
    <div className={styles.nodeBlock} role="treeitem">
      <div
        className={`${styles.nodeRow} ${isDropTarget ? styles.dropTarget : ""}`}
        style={{ paddingLeft: `${depth * 1.25}rem` }}
        onDragOver={(event) => {
          if (!isAdmin) return;
          event.preventDefault();
          setIsDropTarget(true);
        }}
        onDragLeave={() => setIsDropTarget(false)}
        onDrop={handleDrop}
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

        {isAdmin && (
          <span
            className={styles.dragHandle}
            draggable
            onDragStart={(event) => {
              event.dataTransfer.setData("text/plain", zone.id);
              event.dataTransfer.effectAllowed = "move";
            }}
            aria-label={`Drag ${zone.name}`}
            role="button"
            data-testid={`drag-handle-${zone.id}`}
          >
            ⠿
          </span>
        )}

        <span className={styles.zoneName}>{zone.name}</span>
        <span className={`${styles.badge} ${typeBadgeClass(zone.type)}`}>
          {zone.type}
        </span>
        <span className={styles.childCount}>{hasChildren ? `${zone.children.length} child${zone.children.length === 1 ? "" : "ren"}` : ""}</span>
      </div>

      {expanded &&
        zone.children.map((child) => (
          <ZoneNode
            key={child.id}
            zone={child}
            depth={depth + 1}
            isAdmin={isAdmin}
            onReparent={onReparent}
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

  const reparentMutation = useMutation({
    mutationFn: ({ zoneId, parentId }: { zoneId: string; parentId: string }) =>
      put(`/zones/${zoneId}`, { parentZoneId: parentId }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["zones"] });
    },
    onError: (err: unknown) => {
      const message =
        err instanceof Error ? err.message : "Failed to move zone.";
      showToast(message, "error");
    },
  });

  if (isLoading) {
    return <div role="status">Loading zones...</div>;
  }

  if (isError) {
    return (
      <div role="alert">
        Failed to load zones: {error instanceof Error ? error.message : "unknown error"}
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
          onReparent={(zoneId, newParentId) =>
            reparentMutation.mutate({ zoneId, parentId: newParentId })
          }
        />
      ))}
    </div>
  );
}
