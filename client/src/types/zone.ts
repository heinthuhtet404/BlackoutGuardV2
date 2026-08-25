export type ZoneType = "building" | "floor" | "room";
export type PriorityLevel = "P1" | "P2" | "P3";
export type PriorityMode = "auto" | "manual";

export interface Load {
    id: string;
    facilityId: string;
    zoneId: string;
    name: string;
    relayAddress: number;
    powerRatingKw: number;
    priority: PriorityLevel;
    priorityMode: PriorityMode;
    isActive: boolean;
    isSheddable: boolean;

    // Phase 1 - Wizard Data Fields
    safetyRisk?: number;
    dataLossRisk?: number;
    operationalRisk?: number;
    comfortRisk?: number;
    criticalityScore?: number;
}

export interface ZoneTree {
    id: string;
    facilityId: string;
    name: string;
    type: ZoneType;
    parentZoneId: string | null;
    loads?: Load[];           // Direct loads under this zone
    children: ZoneTree[];     // Sub-zones (Floors / Rooms)
}

export interface ZoneTreeNode extends ZoneTree {
    children: ZoneTreeNode[];
}