import { useState, useEffect, useMemo, type FormEvent } from "react";
import { useTelemetry } from "../context/TelemetryContext";
import { get, put, del } from "../api/apiClient";
import {
    Zap,
    Gauge,
    Factory,
    Activity,
    Plug,
    Thermometer,
    Fuel,
    Clock,
    Shield,
    AlertTriangle,
    AlertCircle,
    CheckCircle,
    Edit,
    Trash2,
    Home,
    Bell,
    Wifi,
    WifiOff,
    Power,
    ArrowUpRight,
    Layers,
    Server,
    Cpu,
    Database,
    Settings2,
    RefreshCw,
    Loader2,
    X,
} from "lucide-react";
import styles from "./LiveOverviewPage.module.css";

interface AlarmLog {
    id: string;
    time: string;
    message: string;
    type: "warning" | "critical" | "success" | "info";
}

interface LoadDto {
    id: string;
    zoneId: string;
    name: string;
    powerRatingKw: number;
    priority?: string | number;
    priorityLevel?: number;
    relayAddress?: number;
    isActive?: boolean;
    isSheddable?: boolean;
}

interface ZoneDto {
    id: string;
    facilityId: string;
    name: string;
    type: string;
    parentZoneId?: string | null;
    loads?: LoadDto[];
    children?: ZoneDto[];
    subZones?: ZoneDto[];
}

interface FacilityDto {
    id: string;
    tenantId: string;
    name: string;
    generatorCapacityKw: number;
    solarCapacityKw: number;
    isGridOnline: boolean;
    timezoneId: string;
    createdAt: string;
}

function getLoadPriorityNum(load: LoadDto): number {
    if (typeof load.priorityLevel === "number") return load.priorityLevel;
    if (typeof load.priority === "number") return load.priority;
    if (typeof load.priority === "string") {
        const parsed = parseInt(load.priority.replace(/\D/g, ""), 10);
        if (!isNaN(parsed)) return parsed;
    }
    return 1;
}

function getAllZoneLoads(zone: ZoneDto): LoadDto[] {
    let loads: LoadDto[] = Array.isArray(zone.loads) ? [...zone.loads] : [];
    const childZones = zone.children || zone.subZones || [];

    if (childZones.length > 0) {
        childZones.forEach((child) => {
            loads = [...loads, ...getAllZoneLoads(child)];
        });
    }

    return loads;
}

export function LiveOverviewPage() {
    const { telemetry, connected, latestDecision } = useTelemetry();

    const [zones, setZones] = useState<ZoneDto[]>([]);
    const [loadingDbData, setLoadingDbData] = useState<boolean>(true);
    const [facility, setFacility] = useState<FacilityDto | null>(null);
    const [loadingFacility, setLoadingFacility] = useState<boolean>(true);

    const [freqHistory, setFreqHistory] = useState<number[]>([]);
    const [alarms, setAlarms] = useState<AlarmLog[]>([]);

    const [editingLoad, setEditingLoad] = useState<LoadDto | null>(null);
    const [editName, setEditName] = useState("");
    const [editPower, setEditPower] = useState<number>(0);
    const [editPriority, setEditPriority] = useState<number>(1);
    const [isSaving, setIsSaving] = useState(false);
    const [deletingLoadId, setDeletingLoadId] = useState<string | null>(null);

    const [editingZone, setEditingZone] = useState<ZoneDto | null>(null);
    const [editZoneName, setEditZoneName] = useState("");
    const [isSavingZone, setIsSavingZone] = useState(false);
    const [deletingZoneId, setDeletingZoneId] = useState<string | null>(null);

    // ============================================
    // State for smooth decreasing fuel and runtime
    // ============================================
    const [simulatedFuelLevel, setSimulatedFuelLevel] = useState<number>(85);
    const [simulatedRuntime, setSimulatedRuntime] = useState<number>(12);

    // ============================================
    // Previous state tracking for alarm generation
    // ============================================
    const [prevGridStatus, setPrevGridStatus] = useState<boolean | null>(null);
    const [prevSolarAvailable, setPrevSolarAvailable] = useState<boolean | null>(null);
    const [prevGeneratorAvailable, setPrevGeneratorAvailable] = useState<boolean | null>(null);
    const [prevLoadStatusMap, setPrevLoadStatusMap] = useState<Map<string, string>>(new Map());
    const [prevFuelLevel, setPrevFuelLevel] = useState<number | null>(null);
    const [prevRuntime, setPrevRuntime] = useState<number | null>(null);
    const [prevEngineTemp, setPrevEngineTemp] = useState<number | null>(null);

    // Fetch Facility Data
    useEffect(() => {
        const fetchFacility = async () => {
            try {
                let facilityData = null;

                try {
                    const data = await get<FacilityDto[]>("/facilities");
                    if (data && data.length > 0) {
                        facilityData = data[0];
                    }
                } catch (err) {
                    console.log("Facilities endpoint failed, trying simulator config...");
                }

                if (!facilityData) {
                    try {
                        const configData = await get<any>("/simulator/config");
                        facilityData = {
                            id: configData.id || "current-facility",
                            tenantId: configData.tenantId || "current-tenant",
                            name: configData.name || "Main Facility",
                            generatorCapacityKw: configData.generatorCapacityKw || 0,
                            solarCapacityKw: configData.solarCapacityKw || 0,
                            isGridOnline: configData.gridOnline ?? true,
                            timezoneId: configData.timezoneId || "UTC",
                            createdAt: configData.createdAt || new Date().toISOString()
                        };
                    } catch (err) {
                        console.error("Simulator config also failed:", err);
                        facilityData = {
                            id: "0e6efd17-9662-4de1-8871-2d5ce7190f0a",
                            tenantId: "0e6efd17-9662-4de1-8871-2d5ce7190f0a",
                            name: "Main Facility",
                            generatorCapacityKw: 196,
                            solarCapacityKw: 344,
                            isGridOnline: true,
                            timezoneId: "UTC",
                            createdAt: new Date().toISOString()
                        };
                    }
                }

                setFacility(facilityData);
            } catch (err) {
                console.error("Failed to load facility data:", err);
            } finally {
                setLoadingFacility(false);
            }
        };

        fetchFacility();
    }, []);

    // ============================================
    // Simulate real-time decreasing fuel and runtime
    // ============================================
    useEffect(() => {
        const isGridOnline = facility?.isGridOnline ?? true;
        const hasGenerator = (facility?.generatorCapacityKw || 0) > 0;

        if (isGridOnline || !hasGenerator) {
            return;
        }

        const interval = setInterval(() => {
            setSimulatedFuelLevel((prev) => {
                const newValue = Math.max(0, prev - 0.05);
                return Math.round(newValue * 10) / 10;
            });

            setSimulatedRuntime((prev) => {
                const newValue = Math.max(0, prev - 0.01);
                return Math.round(newValue * 10) / 10;
            });
        }, 1000);

        return () => clearInterval(interval);
    }, [facility?.isGridOnline, facility?.generatorCapacityKw]);

    // ============================================
    // Reset fuel and runtime when Grid turns ON
    // ============================================
    useEffect(() => {
        const isGridOnline = facility?.isGridOnline ?? true;
        const hasGenerator = (facility?.generatorCapacityKw || 0) > 0;

        if (isGridOnline || !hasGenerator) {
            setSimulatedFuelLevel(85);
            setSimulatedRuntime(12);
        }
    }, [facility?.isGridOnline, facility?.generatorCapacityKw]);

    // Fetch Zones and Loads
    useEffect(() => {
        const fetchZonesAndLoads = async () => {
            try {
                const data = await get<ZoneDto[]>("/zones");
                setZones(data || []);
            } catch (err) {
                console.error("Failed to load zones and loads from API:", err);
            } finally {
                setLoadingDbData(false);
            }
        };

        fetchZonesAndLoads();
    }, []);

    // ============================================
    // Add Alarm Helper Function
    // ============================================
    const addAlarm = (message: string, type: "warning" | "critical" | "success" | "info") => {
        const timeStr = new Date().toLocaleTimeString();
        const newAlarm: AlarmLog = {
            id: `${Date.now()}-${Math.random()}`,
            time: timeStr,
            message,
            type,
        };

        setAlarms((prev) => {
            // Prevent duplicate alarms
            const isDuplicate = prev.some(a => a.message === message);
            if (isDuplicate) return prev;
            return [newAlarm, ...prev.slice(0, 19)];
        });
    };

    // ============================================
    // Core Variables
    // ============================================
    const isUnderFrequency = telemetry ? telemetry.frequency < 49.5 : false;
    const isGridOnline = facility?.isGridOnline ?? true;
    const generatorCapacityKw = facility?.generatorCapacityKw || 0;
    const solarCapacityKw = facility?.solarCapacityKw || 0;
    const totalAvailableCapacity = solarCapacityKw + generatorCapacityKw;
    const hasGenerator = generatorCapacityKw > 0;

    // ============================================
    // Load Calculations
    // ============================================
    const allLoads = useMemo(() => {
        return zones.flatMap((z) => getAllZoneLoads(z));
    }, [zones]);

    const totalConfiguredKw = useMemo(() => {
        return allLoads.reduce((sum, l) => sum + (l.powerRatingKw || 0), 0);
    }, [allLoads]);

    // ============================================
    // Capacity Allocation - MOVED BEFORE useEffects that use it
    // ============================================
    const loadStatusMap = useMemo(() => {
        const statusMap = new Map<string, "Normal" | "Shedded">();

        if (isGridOnline) {
            allLoads.forEach((load) => {
                statusMap.set(load.id, "Normal");
            });
            return statusMap;
        }

        let availableCapacity = 0;

        if (solarCapacityKw > 0 && generatorCapacityKw > 0) {
            availableCapacity = solarCapacityKw + generatorCapacityKw;
        } else if (solarCapacityKw > 0) {
            availableCapacity = solarCapacityKw;
        } else if (generatorCapacityKw > 0) {
            availableCapacity = generatorCapacityKw;
        }

        if (availableCapacity <= 0) {
            allLoads.forEach((load) => {
                statusMap.set(load.id, "Shedded");
            });
            return statusMap;
        }

        if (totalConfiguredKw <= availableCapacity) {
            allLoads.forEach((load) => {
                statusMap.set(load.id, "Normal");
            });
            return statusMap;
        }

        allLoads.forEach((load) => {
            statusMap.set(load.id, "Shedded");
        });

        const sortedLoads = [...allLoads].sort((a, b) => {
            const priorityA = getLoadPriorityNum(a);
            const priorityB = getLoadPriorityNum(b);

            if (priorityA !== priorityB) {
                return priorityA - priorityB;
            }
            return (b.powerRatingKw || 0) - (a.powerRatingKw || 0);
        });

        let remainingCapacity = availableCapacity;

        for (const load of sortedLoads) {
            const loadPower = load.powerRatingKw || 0;
            if (loadPower <= remainingCapacity) {
                statusMap.set(load.id, "Normal");
                remainingCapacity -= loadPower;
            }
        }

        return statusMap;
    }, [allLoads, totalConfiguredKw, generatorCapacityKw, solarCapacityKw, isGridOnline]);

    const realTimeLoadKw = useMemo(() => {
        return allLoads.reduce((sum, load) => {
            const status = loadStatusMap.get(load.id);
            return status === "Shedded" ? sum : sum + (load.powerRatingKw || 0);
        }, 0);
    }, [allLoads, loadStatusMap]);

    // ============================================
    // Generator Metrics
    // ============================================
    const generatorMetrics = useMemo(() => {
        const shouldShowGeneratorMetrics = !isGridOnline && hasGenerator;

        if (!shouldShowGeneratorMetrics) {
            return {
                engineTemp: null,
                fuelLevel: null,
                runtimeRemaining: null,
                isActive: false
            };
        }

        const baseTemp = 85;
        const loadPercent = totalAvailableCapacity > 0
            ? (realTimeLoadKw / totalAvailableCapacity) * 100
            : 0;
        const loadFactor = (loadPercent / 10) * 3;
        const engineTemp = Math.min(baseTemp + loadFactor, 150);

        const fuelLevel = simulatedFuelLevel;

        const totalFuelCapacity = 500;
        const baseConsumption = 5;
        const loadConsumption = (realTimeLoadKw / 100) * 10;
        const consumptionRate = baseConsumption + loadConsumption;
        const remainingFuel = (fuelLevel / 100) * totalFuelCapacity;
        const runtimeRemaining = remainingFuel / consumptionRate;

        return {
            engineTemp: Math.round(engineTemp * 10) / 10,
            fuelLevel: Math.round(fuelLevel * 10) / 10,
            runtimeRemaining: Math.round(runtimeRemaining * 10) / 10,
            isActive: true
        };
    }, [isGridOnline, hasGenerator, realTimeLoadKw, totalAvailableCapacity, simulatedFuelLevel]);

    // ============================================
    // Generate Alarms based on state changes
    // ============================================
    useEffect(() => {
        if (!facility) return;

        const isGridOnline = facility.isGridOnline;
        const hasGenerator = facility.generatorCapacityKw > 0;
        const hasSolar = facility.solarCapacityKw > 0;

        // Grid Status Change
        if (prevGridStatus !== null && prevGridStatus !== isGridOnline) {
            if (isGridOnline) {
                addAlarm("🔌 Grid POWER RESTORED", "success");
            } else {
                addAlarm("⚡ Grid POWER OUTAGE detected", "critical");
            }
        }
        setPrevGridStatus(isGridOnline);

        // Solar Availability
        const solarAvailable = hasSolar && isGridOnline;
        if (prevSolarAvailable !== null && prevSolarAvailable !== solarAvailable) {
            if (solarAvailable) {
                addAlarm("☀️ Solar power is AVAILABLE", "success");
            } else {
                addAlarm("☀️ Solar power is UNAVAILABLE", "warning");
            }
        }
        setPrevSolarAvailable(solarAvailable);

        // Generator Availability
        const generatorAvailable = hasGenerator && !isGridOnline;
        if (prevGeneratorAvailable !== null && prevGeneratorAvailable !== generatorAvailable) {
            if (generatorAvailable) {
                addAlarm("🔄 Generator is now RUNNING", "warning");
            } else if (prevGeneratorAvailable === true) {
                addAlarm("🛑 Generator has STOPPED", "info");
            }
        }
        setPrevGeneratorAvailable(generatorAvailable);

    }, [facility]);

    // ============================================
    // Load Status Change Alarms - FIXED: loadStatusMap is now defined
    // ============================================
    useEffect(() => {
        // Skip if still loading or no zones
        if (loadingDbData || zones.length === 0) return;
        // Skip if loadStatusMap is empty (no loads)
        if (loadStatusMap.size === 0) return;

        const currentLoadStatusMap = new Map<string, string>();

        allLoads.forEach((load) => {
            const status = loadStatusMap.get(load.id) || "Shedded";
            currentLoadStatusMap.set(load.id, status);
        });

        // Compare with previous status
        if (prevLoadStatusMap.size > 0) {
            currentLoadStatusMap.forEach((status, loadId) => {
                const prevStatus = prevLoadStatusMap.get(loadId);
                if (prevStatus && prevStatus !== status) {
                    const load = allLoads.find(l => l.id === loadId);
                    if (load) {
                        if (status === "Normal") {
                            addAlarm(`✅ Load "${load.name}" is now ACTIVE (${load.powerRatingKw}kW)`, "success");
                        } else if (status === "Shedded") {
                            addAlarm(`⛔ Load "${load.name}" has been SHEDDED (${load.powerRatingKw}kW)`, "warning");
                        }
                    }
                }
            });
        }

        setPrevLoadStatusMap(currentLoadStatusMap);
    }, [loadStatusMap, zones, loadingDbData, allLoads]);

    // ============================================
    // Telemetry Alarms (Frequency)
    // ============================================
    useEffect(() => {
        if (!telemetry) return;

        setFreqHistory((prev) => [...prev.slice(-19), telemetry.frequency]);

        if (telemetry.frequency < 49.5) {
            addAlarm(`⚠️ Low Frequency Warning: ${telemetry.frequency.toFixed(2)} Hz`, "warning");
        }
    }, [telemetry]);

    // ============================================
    // Load Shedding Decision Alarms
    // ============================================
    useEffect(() => {
        if (!latestDecision) return;

        addAlarm(`⚡ Load Shedding Executed: ${latestDecision.rationale}`, "critical");
    }, [latestDecision]);

    // ============================================
    // Generator Health Alarms
    // ============================================
    useEffect(() => {
        const metrics = generatorMetrics;

        // Fuel Level Alarm
        if (metrics.fuelLevel !== null) {
            if (prevFuelLevel !== null && prevFuelLevel > 20 && metrics.fuelLevel <= 20) {
                addAlarm(`⛽ Fuel level LOW: ${metrics.fuelLevel.toFixed(1)}%`, "warning");
            }
            if (prevFuelLevel !== null && prevFuelLevel > 10 && metrics.fuelLevel <= 10) {
                addAlarm(`⛽ Fuel level CRITICAL: ${metrics.fuelLevel.toFixed(1)}%`, "critical");
            }
            setPrevFuelLevel(metrics.fuelLevel);
        }

        // Runtime Alarm
        if (metrics.runtimeRemaining !== null) {
            if (prevRuntime !== null && prevRuntime > 1 && metrics.runtimeRemaining <= 1) {
                addAlarm(`⏱️ Runtime remaining LOW: ${metrics.runtimeRemaining.toFixed(1)} hrs`, "warning");
            }
            if (prevRuntime !== null && prevRuntime > 0.5 && metrics.runtimeRemaining <= 0.5) {
                addAlarm(`⏱️ Runtime remaining CRITICAL: ${metrics.runtimeRemaining.toFixed(1)} hrs`, "critical");
            }
            setPrevRuntime(metrics.runtimeRemaining);
        }

        // Engine Temperature Alarm
        if (metrics.engineTemp !== null) {
            if (prevEngineTemp !== null && prevEngineTemp <= 120 && metrics.engineTemp > 120) {
                addAlarm(`🌡️ Engine temperature HIGH: ${metrics.engineTemp.toFixed(1)}°C`, "warning");
            }
            if (prevEngineTemp !== null && prevEngineTemp <= 140 && metrics.engineTemp > 140) {
                addAlarm(`🌡️ Engine temperature CRITICAL: ${metrics.engineTemp.toFixed(1)}°C`, "critical");
            }
            setPrevEngineTemp(metrics.engineTemp);
        }
    }, [generatorMetrics]);

    // ============================================
    // System Mode
    // ============================================
    const getSystemMode = () => {
        if (!connected) return { text: "SYSTEM OFFLINE", className: styles.modeOffline };

        if (isGridOnline) {
            return { text: "GRID POWER MODE", className: styles.modeSuccess };
        }

        if (solarCapacityKw > 0 && realTimeLoadKw <= solarCapacityKw) {
            return { text: "SOLAR POWER MODE", className: styles.modeSuccess };
        }

        if (generatorCapacityKw > 0 && realTimeLoadKw <= generatorCapacityKw) {
            return { text: "GENERATOR BACKUP MODE", className: styles.modeWarning };
        }

        if (generatorCapacityKw <= 0 && solarCapacityKw <= 0 && !isGridOnline) {
            return { text: "NO POWER SOURCE AVAILABLE", className: styles.modeDanger };
        }

        if (realTimeLoadKw > generatorCapacityKw && generatorCapacityKw > 0 && !isGridOnline) {
            return { text: "LOAD SHEDDING ACTIVE", className: styles.modeDanger };
        }

        return { text: "SYSTEM UNKNOWN", className: styles.modeOffline };
    };

    const systemMode = getSystemMode();

    // ============================================
    // Priority Breakdown
    // ============================================
    const { p1Kw, p2Kw, p3Kw, p1Pct, p2Pct, p3Pct } = useMemo(() => {
        let p1 = 0, p2 = 0, p3 = 0;

        allLoads.forEach((load) => {
            const priority = getLoadPriorityNum(load);
            const kw = load.powerRatingKw || 0;
            if (priority === 1) p1 += kw;
            else if (priority === 2) p2 += kw;
            else if (priority === 3) p3 += kw;
        });

        const pct1 = totalConfiguredKw > 0 ? Math.round((p1 / totalConfiguredKw) * 100) : 0;
        const pct2 = totalConfiguredKw > 0 ? Math.round((p2 / totalConfiguredKw) * 100) : 0;
        const pct3 = totalConfiguredKw > 0 ? Math.round((p3 / totalConfiguredKw) * 100) : 0;

        return { p1Kw: p1, p2Kw: p2, p3Kw: p3, p1Pct: pct1, p2Pct: pct2, p3Pct: pct3 };
    }, [allLoads, totalConfiguredKw]);

    const getLoadStatus = (load: LoadDto) => {
        return loadStatusMap.get(load.id) || "Shedded";
    };

    // ============================================
    // Clear All Alarms
    // ============================================
    const clearAlarms = () => {
        setAlarms([]);
    };

    // ============================================
    // Zone/Load CRUD Operations
    // ============================================
    const updateZoneLoadsRecursive = (
        zoneList: ZoneDto[],
        targetLoadId: string,
        action: "update" | "delete",
        updatedData?: Partial<LoadDto>
    ): ZoneDto[] => {
        return zoneList.map((zone) => {
            let updatedLoads = zone.loads || [];

            if (action === "delete") {
                updatedLoads = updatedLoads.filter((l) => l.id !== targetLoadId);
            } else if (action === "update" && updatedData) {
                updatedLoads = updatedLoads.map((l) =>
                    l.id === targetLoadId ? { ...l, ...updatedData } : l
                );
            }

            const childKey = zone.children ? "children" : zone.subZones ? "subZones" : null;
            let updatedChildren = zone.children || zone.subZones;

            if (childKey && updatedChildren && updatedChildren.length > 0) {
                updatedChildren = updateZoneLoadsRecursive(
                    updatedChildren,
                    targetLoadId,
                    action,
                    updatedData
                );
            }

            return {
                ...zone,
                loads: updatedLoads,
                ...(childKey === "children" ? { children: updatedChildren } : {}),
                ...(childKey === "subZones" ? { subZones: updatedChildren } : {}),
            };
        });
    };

    const updateZoneRecursive = (
        zoneList: ZoneDto[],
        targetZoneId: string,
        action: "update" | "delete",
        updatedData?: Partial<ZoneDto>
    ): ZoneDto[] => {
        if (action === "delete") {
            return zoneList
                .filter((z) => z.id !== targetZoneId)
                .map((zone) => {
                    const childKey = zone.children ? "children" : zone.subZones ? "subZones" : null;
                    let updatedChildren = zone.children || zone.subZones;

                    if (childKey && updatedChildren && updatedChildren.length > 0) {
                        updatedChildren = updateZoneRecursive(
                            updatedChildren,
                            targetZoneId,
                            "delete"
                        );
                    }

                    return {
                        ...zone,
                        ...(childKey === "children" ? { children: updatedChildren } : {}),
                        ...(childKey === "subZones" ? { subZones: updatedChildren } : {}),
                    };
                });
        }

        return zoneList.map((zone) => {
            let currentZone = zone;
            if (zone.id === targetZoneId && updatedData) {
                currentZone = { ...zone, ...updatedData };
            }

            const childKey = currentZone.children ? "children" : currentZone.subZones ? "subZones" : null;
            let updatedChildren = currentZone.children || currentZone.subZones;

            if (childKey && updatedChildren && updatedChildren.length > 0) {
                updatedChildren = updateZoneRecursive(
                    updatedChildren,
                    targetZoneId,
                    "update",
                    updatedData
                );
            }

            return {
                ...currentZone,
                ...(childKey === "children" ? { children: updatedChildren } : {}),
                ...(childKey === "subZones" ? { subZones: updatedChildren } : {}),
            };
        });
    };

    const handleDeleteLoad = async (loadId: string) => {
        if (!window.confirm("Are you sure you want to delete this load from database?")) return;

        setDeletingLoadId(loadId);
        try {
            await del(`/loads/${loadId}`);
            setZones((prevZones) => updateZoneLoadsRecursive(prevZones, loadId, "delete"));
        } catch (err) {
            console.error("Failed to delete load:", err);
            alert("Error deleting load. Please try again.");
        } finally {
            setDeletingLoadId(null);
        }
    };

    const handleOpenEditModal = (load: LoadDto) => {
        setEditingLoad(load);
        setEditName(load.name);
        setEditPower(load.powerRatingKw);
        setEditPriority(getLoadPriorityNum(load));
    };

    const handleSaveEdit = async (e: FormEvent) => {
        e.preventDefault();
        if (!editingLoad) return;

        setIsSaving(true);
        const payload = {
            name: editName,
            powerRatingKw: editPower,
            priorityLevel: editPriority,
            priority: `P${editPriority}`,
        };

        try {
            await put(`/loads/${editingLoad.id}`, payload);
            setZones((prevZones) =>
                updateZoneLoadsRecursive(prevZones, editingLoad.id, "update", payload)
            );
            setEditingLoad(null);
        } catch (err) {
            console.error("Failed to update load:", err);
            alert("Error updating load details.");
        } finally {
            setIsSaving(false);
        }
    };

    const handleOpenEditZoneModal = (zone: ZoneDto) => {
        setEditingZone(zone);
        setEditZoneName(zone.name);
    };

    const handleSaveZoneEdit = async (e: FormEvent) => {
        e.preventDefault();
        if (!editingZone) return;

        setIsSavingZone(true);

        const payload = {
            name: editZoneName.trim(),
            type: editingZone.type || "building",
            parentZoneId: editingZone.parentZoneId || null,
        };

        try {
            await put(`/zones/${editingZone.id}`, payload);

            setZones((prevZones) =>
                updateZoneRecursive(prevZones, editingZone.id, "update", payload)
            );
            setEditingZone(null);
        } catch (err: any) {
            console.error("Failed to update zone details:", err);
            alert(`Error updating zone: ${err.message || "Validation failed"}`);
        } finally {
            setIsSavingZone(false);
        }
    };

    const handleDeleteZone = async (zone: ZoneDto) => {
        const associatedLoads = getAllZoneLoads(zone);

        if (associatedLoads.length > 0) {
            alert(`Cannot delete zone "${zone.name}". Please delete or reassign all ${associatedLoads.length} load(s) under this zone first.`);
            return;
        }

        if (!window.confirm(`Are you sure you want to delete zone "${zone.name}"?`)) return;

        setDeletingZoneId(zone.id);
        try {
            await del(`/zones/${zone.id}`);
            setZones((prevZones) => updateZoneRecursive(prevZones, zone.id, "delete"));
        } catch (err) {
            console.error("Failed to delete zone:", err);
            alert("Error deleting zone. Please try again.");
        } finally {
            setDeletingZoneId(null);
        }
    };

    const renderFrequencyChart = () => {
        if (freqHistory.length < 2) return null;
        const width = 300;
        const height = 100;
        const minFreq = 48.0;
        const maxFreq = 52.0;

        const points = freqHistory
            .map((val, idx) => {
                const x = (idx / (freqHistory.length - 1)) * width;
                const normalizedY = Math.max(0, Math.min(1, (val - minFreq) / (maxFreq - minFreq)));
                const y = height - normalizedY * height;
                return `${x},${y}`;
            })
            .join(" ");

        return (
            <svg width="100%" height="100%" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none">
                <line x1="0" y1="25" x2={width} y2="25" stroke="var(--border-soft)" strokeDasharray="2,2" opacity="0.3" />
                <line x1="0" y1="50" x2={width} y2="50" stroke="var(--border-soft)" strokeDasharray="4,4" opacity="0.3" />
                <line x1="0" y1="75" x2={width} y2="75" stroke="var(--border-soft)" strokeDasharray="2,2" opacity="0.3" />
                <polyline
                    fill="none"
                    stroke={isUnderFrequency ? "var(--accent-red)" : "var(--success)"}
                    strokeWidth="2.5"
                    points={points}
                />
            </svg>
        );
    };

    return (
        <div className={styles.page}>
            {/* Header */}
            <div className={styles.headerRow}>
                <div className={styles.headerLeft}>
                    <div className={styles.headerIconWrapper}>
                        <Activity size={28} className={styles.headerIcon} />
                    </div>
                    <div>
                        <h1 className={styles.heading}>Live Overview</h1>
                        <p className={styles.headingSub}>Real-time monitoring & control</p>
                    </div>
                </div>
                <div className={`${styles.systemModeBanner} ${systemMode.className}`}>
                    <span className={styles.modeDot}></span>
                    {systemMode.text}
                </div>
            </div>

            <div className={styles.statusRow}>
                {connected ? (
                    <span className={styles.statusConnected}>
                        <Wifi size={14} />
                        <span className={styles.liveDot}></span>
                        Live Stream Connected
                    </span>
                ) : (
                    <span className={styles.statusDisconnected}>
                        <WifiOff size={14} />
                        Offline / Reconnecting...
                    </span>
                )}
            </div>

            {/* KPI Cards */}
            <div className={styles.grid}>
                {/* Total Available Capacity */}
                <div className={`${styles.card} ${styles.cardAnimate}`}>
                    <div className={styles.cardIcon}>
                        <Zap size={24} />
                    </div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Total Available Capacity</h3>
                        <p className={`${styles.cardValue} ${styles.valueBlue} ${styles.valuePulse}`}>
                            {loadingFacility ? "..." : `${totalAvailableCapacity.toFixed(1)} kW`}
                        </p>
                        <span className={styles.cardSubText}>
                            {loadingFacility ? "Loading..." :
                                `Solar: ${solarCapacityKw} kW | Generator: ${generatorCapacityKw} kW`}
                        </span>
                    </div>
                </div>

                {/* Active Real-Time Load */}
                <div className={`${styles.card} ${styles.cardAnimate}`}>
                    <div className={styles.cardIcon}>
                        <Gauge size={24} />
                    </div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Active Real-Time Load</h3>
                        <p className={`${styles.cardValue} ${styles.valueAmber} ${styles.valuePulse}`}>
                            {loadingDbData ? "..." : `${realTimeLoadKw.toFixed(1)} kW`}
                        </p>
                        <span className={styles.cardSubText}>
                            {loadingDbData ? "Loading..." : `${((realTimeLoadKw / totalConfiguredKw) * 100).toFixed(0)}% of total load`}
                        </span>
                    </div>
                </div>

                {/* Power Source */}
                <div className={`${styles.card} ${styles.cardAnimate}`}>
                    <div className={styles.cardIcon}>
                        <Factory size={24} />
                    </div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Power Source</h3>
                        <p className={`${styles.cardValue} ${facility?.isGridOnline ? styles.valueSuccess : styles.valueDanger} ${styles.valuePulse}`}>
                            {loadingFacility ? "..." : facility?.isGridOnline ? "Grid On" : "Grid Off"}
                        </p>
                        <span className={styles.cardSubText}>
                            {loadingFacility ? "Loading..." :
                                facility?.isGridOnline ? "Power is available from grid" :
                                    "Power outage detected - using backup"}
                        </span>
                    </div>
                </div>

                {/* Solar Capacity */}
                <div className={`${styles.card} ${styles.cardAnimate}`}>
                    <div className={styles.cardIcon}>
                        <Activity size={24} />
                    </div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Solar Capacity</h3>
                        <p className={`${styles.cardValue} ${facility?.solarCapacityKw && facility.solarCapacityKw > 0 ? styles.valueSuccess : styles.valueMuted} ${styles.valuePulse}`}>
                            {loadingFacility ? "..." : facility?.solarCapacityKw ? `${facility.solarCapacityKw} kW` : "Not Available"}
                        </p>
                        {facility?.solarCapacityKw && facility.solarCapacityKw > 0 && (
                            <span className={styles.cardSubText}>Solar power available</span>
                        )}
                        {(!facility?.solarCapacityKw || facility.solarCapacityKw === 0) && (
                            <span className={styles.cardSubText}>No solar installed</span>
                        )}
                    </div>
                </div>

                {/* Generator Capacity */}
                <div className={`${styles.card} ${styles.cardAnimate}`}>
                    <div className={styles.cardIcon}>
                        <Plug size={24} />
                    </div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Generator Capacity</h3>
                        <p className={`${styles.cardValue} ${facility?.generatorCapacityKw && facility.generatorCapacityKw > 0 ? styles.valueAmber : styles.valueMuted} ${styles.valuePulse}`}>
                            {loadingFacility ? "..." : facility?.generatorCapacityKw ? `${facility.generatorCapacityKw} kW` : "Not Available"}
                        </p>
                        {facility?.generatorCapacityKw && facility.generatorCapacityKw > 0 && (
                            <span className={styles.cardSubText}>Backup generator ready</span>
                        )}
                        {(!facility?.generatorCapacityKw || facility.generatorCapacityKw === 0) && (
                            <span className={styles.cardSubText}>No generator installed</span>
                        )}
                    </div>
                </div>

                {/* ============================================ */}
                {/* TOTAL LOAD - REPLACED ENGINE TEMPERATURE */}
                {/* ============================================ */}
                <div className={`${styles.card} ${styles.cardAnimate}`}>
                    <div className={styles.cardIcon}>
                        <Layers size={24} />
                    </div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Total Load</h3>
                        <p className={`${styles.cardValue} ${styles.valueBlue} ${styles.valuePulse}`}>
                            {loadingDbData ? "..." : `${totalConfiguredKw.toFixed(1)} kW`}
                        </p>
                        <span className={styles.cardSubText}>
                            {loadingDbData ? "Loading..." : `${allLoads.length} loads configured`}
                        </span>
                    </div>
                </div>

                {/* Fuel Level */}
                <div className={`${styles.card} ${styles.cardAnimate}`}>
                    <div className={styles.cardIcon}>
                        <Fuel size={24} />
                    </div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Fuel Level</h3>
                        <p className={`${styles.cardValue} ${generatorMetrics.fuelLevel !== null
                            ? generatorMetrics.fuelLevel < 20 ? styles.valueDanger : styles.valueBlue
                            : styles.valueMuted
                            } ${styles.valuePulse}`}>
                            {generatorMetrics.fuelLevel !== null
                                ? `${generatorMetrics.fuelLevel.toFixed(0)} %`
                                : "—"}
                        </p>
                        {generatorMetrics.fuelLevel !== null && generatorMetrics.fuelLevel < 20 && (
                            <span className={styles.warning}>⚠️ Low fuel!</span>
                        )}
                        {generatorMetrics.fuelLevel !== null && generatorMetrics.fuelLevel >= 20 && (
                            <span className={styles.cardSubText}>Fuel level adequate</span>
                        )}
                        {generatorMetrics.fuelLevel === null && (
                            <span className={styles.cardSubText}>
                                {isGridOnline ? "Grid is ON" : "No generator available"}
                            </span>
                        )}
                    </div>
                </div>

                {/* Est Runtime */}
                <div className={`${styles.card} ${styles.cardAnimate}`}>
                    <div className={styles.cardIcon}>
                        <Clock size={24} />
                    </div>
                    <div className={styles.cardContent}>
                        <h3 className={styles.cardLabel}>Est. Runtime</h3>
                        <p className={`${styles.cardValue} ${generatorMetrics.runtimeRemaining !== null
                            ? generatorMetrics.runtimeRemaining < 1 ? styles.valueDanger : styles.valueSuccess
                            : styles.valueMuted
                            } ${styles.valuePulse}`}>
                            {generatorMetrics.runtimeRemaining !== null
                                ? `${generatorMetrics.runtimeRemaining.toFixed(1)} hrs`
                                : "—"}
                        </p>
                        {generatorMetrics.runtimeRemaining !== null && generatorMetrics.runtimeRemaining < 1 && (
                            <span className={styles.warning}>⚠️ Low runtime!</span>
                        )}
                        {generatorMetrics.runtimeRemaining !== null && generatorMetrics.runtimeRemaining >= 1 && (
                            <span className={styles.cardSubText}>Runtime remaining</span>
                        )}
                        {generatorMetrics.runtimeRemaining === null && (
                            <span className={styles.cardSubText}>
                                {isGridOnline ? "Grid is ON" : "No generator available"}
                            </span>
                        )}
                    </div>
                </div>
            </div>

            {/* System Status Map */}
            <div className={`${styles.sectionCard} ${styles.sectionAnimate}`}>
                <div className={styles.sectionHeader}>
                    <div className={styles.sectionHeaderLeft}>
                        <Layers size={20} className={styles.sectionIcon} />
                        <h2 className={styles.sectionTitle}>System Status Map</h2>
                    </div>
                    <span className={styles.sectionBadge}>{zones.length} Zones</span>
                </div>
                {loadingDbData ? (
                    <div className={styles.loadingState}>
                        <Loader2 size={20} className={styles.spinner} />
                        Loading database hierarchy...
                    </div>
                ) : zones.length === 0 ? (
                    <div className={styles.emptyState}>
                        <Database size={40} className={styles.emptyIcon} />
                        <p>No zones found in database. Create a Zone to view telemetry mapping.</p>
                    </div>
                ) : (
                    <div className={styles.zoneList}>
                        {zones.map((zone) => {
                            const zoneLoads = getAllZoneLoads(zone);
                            const hasLoads = zoneLoads.length > 0;

                            return (
                                <div key={zone.id} className={`${styles.zoneCard} ${styles.zoneAnimate}`}>
                                    <div className={styles.zoneHeader}>
                                        <div className={styles.zoneTitle}>
                                            <span className={styles.zoneIcon}>
                                                <Home size={18} />
                                            </span>
                                            <h3>{zone.name}</h3>
                                        </div>
                                        <div className={styles.zoneActionsWrapper}>
                                            <span className={styles.zoneLoadCount}>
                                                <Server size={12} />
                                                {zoneLoads.length} loads
                                            </span>
                                            <div className={styles.actionButtons}>
                                                <button
                                                    className={styles.editBtn}
                                                    onClick={() => handleOpenEditZoneModal(zone)}
                                                    title="Edit Zone Name"
                                                >
                                                    <Edit size={14} />
                                                </button>
                                                <button
                                                    className={styles.deleteBtn}
                                                    onClick={() => handleDeleteZone(zone)}
                                                    disabled={deletingZoneId === zone.id || hasLoads}
                                                    title={hasLoads ? "Cannot delete zone with existing loads" : "Delete Zone"}
                                                >
                                                    <Trash2 size={14} />
                                                </button>
                                            </div>
                                        </div>
                                    </div>

                                    <div className={styles.nodeGrid}>
                                        {hasLoads ? (
                                            zoneLoads.map((load) => {
                                                const status = getLoadStatus(load);
                                                const isShedded = status === "Shedded";
                                                const currentPriority = getLoadPriorityNum(load);
                                                return (
                                                    <div
                                                        key={load.id}
                                                        className={`${styles.nodeCard} ${isShedded ? styles.nodeShedded : styles.nodeNormal} ${styles.nodeAnimate}`}
                                                    >
                                                        {isShedded && <div className={styles.shedPulseBg} />}
                                                        {!isShedded && <div className={styles.normalGlowBg} />}

                                                        <div className={styles.nodeIcon}>
                                                            <Cpu size={18} />
                                                        </div>
                                                        <div className={styles.nodeInfo}>
                                                            <div className={styles.nodeName}>{load.name}</div>
                                                            <div className={styles.nodeMeta}>
                                                                <span>{load.powerRatingKw} kW</span>
                                                                <span className={styles.sep}>·</span>
                                                                <span className={`${styles.priorityBadge} 
                                                                    ${currentPriority === 1 ? styles.priorityP1 :
                                                                        currentPriority === 2 ? styles.priorityP2 :
                                                                            styles.priorityP3
                                                                    }`}>
                                                                    P{currentPriority}
                                                                </span>
                                                            </div>
                                                        </div>

                                                        <div className={styles.nodeActionsWrapper}>
                                                            <div className={`${styles.nodeBadge} ${isShedded ? styles.badgeShedded : styles.badgeNormal}`}>
                                                                <span className={styles.badgeDot} />
                                                                {isShedded ? "Shedded" : "Active"}
                                                            </div>

                                                            <div className={styles.actionButtons}>
                                                                <button
                                                                    className={styles.editBtn}
                                                                    onClick={() => handleOpenEditModal(load)}
                                                                    title="Edit Load"
                                                                >
                                                                    <Edit size={14} />
                                                                </button>
                                                                <button
                                                                    className={styles.deleteBtn}
                                                                    onClick={() => handleDeleteLoad(load.id)}
                                                                    disabled={deletingLoadId === load.id}
                                                                    title="Delete Load"
                                                                >
                                                                    <Trash2 size={14} />
                                                                </button>
                                                            </div>
                                                        </div>
                                                    </div>
                                                );
                                            })
                                        ) : (
                                            <p className={styles.noLoads}>No loads registered under this zone</p>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>

            {/* Edit Zone Modal */}
            {editingZone && (
                <div className={styles.modalOverlay}>
                    <div className={styles.modalCard}>
                        <div className={styles.modalHeader}>
                            <div className={styles.modalIconWrapper}>
                                <Settings2 size={22} />
                            </div>
                            <h3>Edit Zone Details</h3>
                            <button
                                type="button"
                                className={styles.modalClose}
                                onClick={() => setEditingZone(null)}
                            >
                                ✕
                            </button>
                        </div>
                        <p className={styles.modalSubtitle}>Update zone name</p>
                        <form onSubmit={handleSaveZoneEdit} className={styles.modalForm}>
                            <label>
                                Zone Name
                                <input
                                    type="text"
                                    value={editZoneName}
                                    onChange={(e) => setEditZoneName(e.target.value)}
                                    required
                                />
                            </label>

                            <div className={styles.modalActions}>
                                <button
                                    type="button"
                                    className={styles.cancelBtn}
                                    onClick={() => setEditingZone(null)}
                                >
                                    Cancel
                                </button>
                                <button type="submit" className={styles.saveBtn} disabled={isSavingZone}>
                                    {isSavingZone ? "Saving..." : "Save Changes"}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Edit Load Modal */}
            {editingLoad && (
                <div className={styles.modalOverlay}>
                    <div className={styles.modalCard}>
                        <div className={styles.modalHeader}>
                            <div className={styles.modalIconWrapper}>
                                <Cpu size={22} />
                            </div>
                            <h3>Edit Load Details</h3>
                            <button
                                type="button"
                                className={styles.modalClose}
                                onClick={() => setEditingLoad(null)}
                            >
                                ✕
                            </button>
                        </div>
                        <p className={styles.modalSubtitle}>Update load name, power rating, or priority level</p>
                        <form onSubmit={handleSaveEdit} className={styles.modalForm}>
                            <label>
                                Load Name
                                <input
                                    type="text"
                                    value={editName}
                                    onChange={(e) => setEditName(e.target.value)}
                                    required
                                />
                            </label>
                            <label>
                                Power Rating (kW)
                                <input
                                    type="number"
                                    step="0.1"
                                    value={editPower}
                                    onChange={(e) => setEditPower(parseFloat(e.target.value) || 0)}
                                    required
                                />
                            </label>
                            <label>
                                Priority Level
                                <select
                                    value={editPriority}
                                    onChange={(e) => setEditPriority(parseInt(e.target.value, 10))}
                                >
                                    <option value={1}>P1 (Critical)</option>
                                    <option value={2}>P2 (Essential)</option>
                                    <option value={3}>P3 (Non-Essential)</option>
                                </select>
                            </label>

                            <div className={styles.modalActions}>
                                <button
                                    type="button"
                                    className={styles.cancelBtn}
                                    onClick={() => setEditingLoad(null)}
                                >
                                    Cancel
                                </button>
                                <button type="submit" className={styles.saveBtn} disabled={isSaving}>
                                    {isSaving ? "Saving..." : "Save Changes"}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Charts Row */}
            <div className={`${styles.chartsRow} ${styles.chartAnimate}`}>
                <div className={styles.chartCard}>
                    <div className={styles.chartHeader}>
                        <Layers size={18} className={styles.chartIcon} />
                        <h3 className={styles.chartTitle}>Priority Breakdown</h3>
                    </div>
                    <div className={styles.loadBreakdownContainer}>
                        <div className={styles.loadBarRow}>
                            <span>P1 (Critical)</span>
                            <span>{p1Kw.toFixed(1)} kW ({p1Pct}%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={`${styles.progressFillP1} ${styles.progressAnimate}`} style={{ width: `${p1Pct}%` }}></div>
                        </div>

                        <div className={styles.loadBarRow}>
                            <span>P2 (Essential)</span>
                            <span>{p2Kw.toFixed(1)} kW ({p2Pct}%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={`${styles.progressFillP2} ${styles.progressAnimate}`} style={{ width: `${p2Pct}%` }}></div>
                        </div>

                        <div className={styles.loadBarRow}>
                            <span>P3 (Non-Essential)</span>
                            <span>{p3Kw.toFixed(1)} kW ({p3Pct}%)</span>
                        </div>
                        <div className={styles.progressBg}>
                            <div className={`${styles.progressFillP3} ${styles.progressAnimate}`} style={{ width: `${p3Pct}%` }}></div>
                        </div>
                    </div>
                </div>
            </div>

            {/* ============================================ */}
            {/* ALARMS & EVENTS */}
            {/* ============================================ */}
            <div className={`${styles.alarmsSection} ${styles.sectionAnimate}`}>
                <div className={styles.sectionHeader}>
                    <div className={styles.sectionHeaderLeft}>
                        <Bell size={20} className={styles.sectionIcon} />
                        <h2 className={styles.sectionTitle}>Recent Alarms & Events</h2>
                        <span className={styles.alarmCount}>{alarms.length} events</span>
                    </div>
                    {alarms.length > 0 && (
                        <button className={styles.clearAlarmsBtn} onClick={clearAlarms}>
                            <X size={16} />
                            Clear All
                        </button>
                    )}
                </div>
                {alarms.length === 0 ? (
                    <div className={styles.noAlarms}>
                        <CheckCircle size={20} className={styles.noAlarmsIcon} />
                        System operating normally. No active alarms.
                    </div>
                ) : (
                    <div className={styles.alarmList}>
                        {alarms.map((alarm, index) => (
                            <div
                                key={alarm.id}
                                className={`${styles.alarmItem} ${alarm.type === "critical"
                                    ? styles.alarmCritical
                                    : alarm.type === "warning"
                                        ? styles.alarmWarning
                                        : alarm.type === "success"
                                            ? styles.alarmSuccess
                                            : styles.alarmInfo
                                    } ${styles.alarmAnimate}`}
                                style={{ animationDelay: `${index * 0.05}s` }}
                            >
                                <span className={styles.alarmTime}>{alarm.time}</span>
                                <span className={styles.alarmMessage}>{alarm.message}</span>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}