local Load = select(2, ...)
local DataToColor = unpack(Load)

local UnitName = UnitName
local UnitGUID = UnitGUID
local UnitClass = UnitClass
local UnitRace = UnitRace

DataToColor.C.unitPlayer = "player"
DataToColor.C.unitTarget = "target"
DataToColor.C.unitParty = "party"
DataToColor.C.unitRaid = "raid"
DataToColor.C.unitPet = "pet"
DataToColor.C.unitFocus = "focus"
DataToColor.C.unitFocusTarget = "focustarget"
DataToColor.C.unitPetTarget = "pettarget"
DataToColor.C.unitTargetTarget = "targettarget"
DataToColor.C.unitNormal = "normal"

-- Character's name
DataToColor.C.CHARACTER_NAME = UnitName(DataToColor.C.unitPlayer)
DataToColor.C.CHARACTER_GUID = UnitGUID(DataToColor.C.unitPlayer)
_, DataToColor.C.CHARACTER_CLASS, DataToColor.C.CHARACTER_CLASS_ID = UnitClass(DataToColor.C.unitPlayer)
_, _, DataToColor.C.CHARACTER_RACE_ID = UnitRace(DataToColor.C.unitPlayer)

-- Actionbar power cost
DataToColor.C.COST_MAX_COST_IDX = 100000
DataToColor.C.COST_MAX_POWER_TYPE = 1000

-- Spells
DataToColor.C.Spell.AutoShotId = 75
DataToColor.C.Spell.ShootId = 5019
DataToColor.C.Spell.AttackId = 6603

-- Item / Inventory
DataToColor.C.ItemPattern = "(m:%d+)"

-- Loot
DataToColor.C.Loot.Corpse = 0
DataToColor.C.Loot.Ready = 1
DataToColor.C.Loot.Closed = 2

-- Gossips
DataToColor.C.Gossip = {
    ["banker"] = 0,
    ["battlemaster"] = 1,
    ["binder"] = 2,
    ["gossip"] = 3,
    ["healer"] = 4,
    ["petition"] = 5,
    ["tabard"] = 6,
    ["taxi"] = 7,
    ["trainer"] = 8,
    ["unlearn"] = 9,
    ["vendor"] = 10,
}

-- Mirror timer labels
DataToColor.C.MIRRORTIMER.BREATH = "BREATH"

DataToColor.C.ActionType.Spell = "spell"
DataToColor.C.ActionType.Macro = "macro"
