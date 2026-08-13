export type ZoneType = "building" | "floor" | "room";

export interface ZoneTree {
  id: string;
  facilityId: string;
  name: string;
  type: ZoneType;
  parentZoneId: string | null;
  children: ZoneTree[];
}

export interface ZoneTreeNode extends ZoneTree {
  children: ZoneTreeNode[];
}
