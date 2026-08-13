import { useState, type FormEvent, type ReactNode } from "react";
import { useZones } from "../../hooks/useZones";
import { ApiError, post, put } from "../../api/apiClient";
import { Button } from "../../components/ui/Button";
import { Input } from "../../components/ui/Input";
import { CriticalityWizard } from "./CriticalityWizard";
import type { ZoneTree } from "../../types/zone";
import styles from "./LoadForm.module.css";

interface LoadFormProps {
  loadId?: string;
  initialValues?: Partial<LoadFormValues>;
  onSaved?: () => void;
  onCreated?: (id: string) => void;
}

export interface LoadFormValues {
  name: string;
  zoneId: string;
  relayAddress: string;
  powerRatingKw: string;
  isSheddable: boolean;
}

type ConflictKind = "relay" | "capacity" | "other";

function classifyError(error: ApiError): ConflictKind {
  if (error.status === 409 && /assigned to/i.test(error.message)) return "relay";
  if (error.status === 409 && /capacity exceeded/i.test(error.message)) return "capacity";
  return "other";
}

interface FlatZone {
  id: string;
  label: string;
}

function flattenZones(zones: ZoneTree[], depth = 0): FlatZone[] {
  const result: FlatZone[] = [];
  for (const zone of zones) {
    result.push({ id: zone.id, label: zone.name });
    result.push(...flattenZones(zone.children, depth + 1));
  }
  return result;
}

export function LoadForm({ loadId, initialValues, onSaved, onCreated }: LoadFormProps): ReactNode {
  const { data: zones, isLoading: zonesLoading } = useZones();

  const [name, setName] = useState(initialValues?.name ?? "");
  const [zoneId, setZoneId] = useState(initialValues?.zoneId ?? "");
  const [relayAddress, setRelayAddress] = useState(initialValues?.relayAddress ?? "");
  const [powerRatingKw, setPowerRatingKw] = useState(initialValues?.powerRatingKw ?? "");
  const [isSheddable, setIsSheddable] = useState(initialValues?.isSheddable ?? true);

  const [relayError, setRelayError] = useState<string | null>(null);
  const [capacityError, setCapacityError] = useState<string | null>(null);
  const [generalError, setGeneralError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [manualPriority, setManualPriority] = useState<"P1" | "P2" | "P3" | null>(null);

  const flatZones = flattenZones(zones ?? []);
  const isEdit = loadId !== undefined;

  const submit = async (force: boolean) => {
    setRelayError(null);
    setCapacityError(null);
    setGeneralError(null);
    setSaving(true);

    const body = {
      name,
      zoneId,
      relayAddress: Number(relayAddress),
      powerRatingKw: Number(powerRatingKw),
      isSheddable,
      ...(manualPriority
        ? { priority: manualPriority, priorityMode: "manual" }
        : isEdit
          ? {}
          : { priority: "P3", priorityMode: "auto" }),
    };

    try {
      if (isEdit) {
        await put(`/loads/${loadId}?force=${force}`, body);
      } else {
        const response = await post<{ id: string }>(`/loads?force=${force}`, body);
        onCreated?.(response.id);
      }
      onSaved?.();
    } catch (err) {
      if (err instanceof ApiError) {
        switch (classifyError(err)) {
          case "relay":
            setRelayError(err.message);
            break;
          case "capacity":
            setCapacityError(err.message);
            break;
          default:
            setGeneralError(err.message);
            break;
        }
      } else {
        setGeneralError(err instanceof Error ? err.message : "Failed to save load.");
      }
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    void submit(false);
  };

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      <Input
        label="Name"
        value={name}
        onChange={(event) => setName(event.target.value)}
        required
        data-testid="load-name"
      />

      <div className={styles.field}>
        <label className={styles.label} htmlFor="load-zone">
          Zone
        </label>
        <select
          id="load-zone"
          className={styles.select}
          value={zoneId}
          onChange={(event) => setZoneId(event.target.value)}
          data-testid="load-zone"
        >
          <option value="">
            {zonesLoading ? "Loading zones..." : "Select a zone"}
          </option>
          {flatZones.map((zone) => (
            <option key={zone.id} value={zone.id}>
              {zone.label}
            </option>
          ))}
        </select>
      </div>

      <Input
        label="Relay address"
        type="number"
        min={0}
        value={relayAddress}
        onChange={(event) => setRelayAddress(event.target.value)}
        required
        error={relayError}
        data-testid="load-relay-address"
      />

      <Input
        label="Power rating (kW)"
        type="number"
        min={0}
        step="0.1"
        value={powerRatingKw}
        onChange={(event) => setPowerRatingKw(event.target.value)}
        required
        data-testid="load-power-rating"
      />

      <label className={styles.checkbox}>
        <input
          type="checkbox"
          checked={isSheddable}
          onChange={(event) => setIsSheddable(event.target.checked)}
          data-testid="load-is-sheddable"
        />
        Sheddable load
      </label>

      <CriticalityWizard loadId={loadId} onManualPriorityChange={setManualPriority} />

      {capacityError && (
        <div className={styles.capacityError} role="alert" data-testid="capacity-error">
          <p>{capacityError}</p>
          <Button
            type="button"
            variant="danger"
            onClick={() => void submit(true)}
            disabled={saving}
            data-testid="override-button"
          >
            Save anyway (override)
          </Button>
        </div>
      )}

      {generalError && (
        <div className={styles.generalError} role="alert" data-testid="general-error">
          {generalError}
        </div>
      )}

      <Button type="submit" disabled={saving} data-testid="save-button">
        {saving ? "Saving..." : isEdit ? "Update Load" : "Create Load"}
      </Button>
    </form>
  );
}
