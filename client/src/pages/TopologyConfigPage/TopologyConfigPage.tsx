import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ZoneTreeView } from "./ZoneTreeView";
import { LoadForm } from "./LoadForm";
import { useZones } from "../../hooks/useZones";
import { useToast } from "../../components/ui/toastContext";
import { Button } from "../../components/ui/Button";
import { Input } from "../../components/ui/Input";
import { post } from "../../api/apiClient";
import type { ZoneType } from "../../types/zone";
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

  const handleCreateZone = (event: FormEvent) => {
    event.preventDefault();
    if (!zoneName.trim()) return;
    setCreatingZone(true);
    createZoneMutation.mutate(undefined, {
      onSettled: () => setCreatingZone(false),
    });
  };

  return (
    <div className={styles.page}>
      <h1 className={styles.heading}>Topology Config</h1>

      <div className={styles.columns}>
        <section className={styles.column}>
          <h2 className={styles.sectionTitle}>Zone Hierarchy</h2>

          <form className={styles.zoneForm} onSubmit={handleCreateZone} noValidate>
            <Input
              label="Zone name"
              value={zoneName}
              onChange={(event) => setZoneName(event.target.value)}
              required
              data-testid="zone-name-input"
            />
            <div className={styles.field}>
              <label className={styles.label} htmlFor="zone-type">
                Zone type
              </label>
              <select
                id="zone-type"
                className={styles.select}
                value={zoneType}
                onChange={(event) => setZoneType(event.target.value as ZoneType)}
                data-testid="zone-type-select"
              >
                <option value="building">Building</option>
                <option value="floor">Floor</option>
                <option value="room">Room</option>
              </select>
            </div>
            <div className={styles.field}>
              <label className={styles.label} htmlFor="zone-parent">
                Parent zone
              </label>
              <select
                id="zone-parent"
                className={styles.select}
                value={zoneParentId}
                onChange={(event) => setZoneParentId(event.target.value)}
                data-testid="zone-parent-select"
              >
                <option value="">None</option>
                {flatZones.map((zone) => (
                  <option key={zone.id} value={zone.id}>
                    {zone.name}
                  </option>
                ))}
              </select>
            </div>
            <Button
              type="submit"
              disabled={creatingZone || !zoneName.trim()}
              data-testid="zone-create-button"
            >
              {creatingZone ? "Creating..." : "Create Zone"}
            </Button>
          </form>

          <div className={styles.tree}>
            <ZoneTreeView />
          </div>
        </section>

        <section className={styles.column}>
          <h2 className={styles.sectionTitle}>Loads</h2>

          {editLoadId ? (
            <div className={styles.editBanner} data-testid="edit-banner">
              <span>Editing load {editLoadId.slice(0, 8)}…</span>
              <Button
                variant="secondary"
                onClick={() => {
                  setEditLoadId(null);
                  setLoadFormKey((key) => key + 1);
                }}
                data-testid="new-load-button"
              >
                New Load
              </Button>
            </div>
          ) : null}

          <LoadForm
            key={loadFormKey}
            loadId={editLoadId ?? undefined}
            onCreated={(id) => setEditLoadId(id)}
            onSaved={() => showToast(editLoadId ? "Load updated." : "Load created.")}
          />
        </section>
      </div>
    </div>
  );
}
