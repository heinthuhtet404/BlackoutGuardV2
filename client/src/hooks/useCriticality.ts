import { useEffect, useRef, useState } from "react";
import { post } from "../api/apiClient";

export interface CriticalityInput {
  q1: number;
  q2: number;
  q3: number;
  q4: number;
}

export interface CriticalityResponse {
  score: number;
  priority: string;
}

const DEBOUNCE_MS = 300;

export function useCriticality(loadId: string) {
  const [response, setResponse] = useState<CriticalityResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const schedule = (input: CriticalityInput) => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }
    timerRef.current = setTimeout(() => {
      void submit(input);
    }, DEBOUNCE_MS);
  };

  const submit = async (input: CriticalityInput) => {
    setSaving(true);
    setError(null);
    try {
      const result = await post<CriticalityResponse>(
        `/loads/${loadId}/criticality`,
        input
      );
      setResponse(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to score criticality.");
    } finally {
      setSaving(false);
    }
  };

  useEffect(() => {
    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
    };
  }, []);

  return { schedule, response, error, saving };
}
