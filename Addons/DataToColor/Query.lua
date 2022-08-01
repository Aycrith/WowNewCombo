local Load = select(2, ...)
local DataToColor = unpack(Load)
local Range = DataToColor.Libs.RangeCheck

local bit = bit
local band = bit.band
local date = date

local tonumber = tonumber
local sub = string.sub
local gsub = string.gsub
local find = string.find
local len = string.len
local upper = string.upper
local byte = string.byte
local strsplit = strsplit

local C_Map = C_Map
local UnitExists = UnitExists
local GetUnitName = GetUnitName
local UnitReaction = UnitReaction
local GetInventorySlotInfo = GetInventorySlotInfo
local GetInventoryItemCount = GetInventoryItemCount
local CheckInteractDistance = CheckInteractDistance
local UnitGUID = UnitGUID

local GetActionInfo = GetActionInfo
local GetMacroSpell = GetMacroSpell
local GetSpellPowerCost = GetSpellPowerCost
local GetInventoryItemLink = GetInventoryItemLink
local IsSpellInRange = IsSpellInRange
local GetSpellInfo = GetSpellInfo
local GetActionCooldown = GetActionCooldown
local IsUsableAction = IsUsableAction
local GetActionTexture = GetActionTexture
local IsCurrentAction = IsCurrentAction
local IsAutoRepeatAction = IsAutoRepeatAction

local IsUsableSpell = IsUsableSpell

local GetNumSkillLines = GetNumSkillLines
local GetSkillLineInfo = GetSkillLineInfo

local UnitIsGhost = UnitIsGhost
local C_DeathInfo = C_DeathInfo
local UnitAttackSpeed = UnitAttackSpeed

-- bits

local UnitAffectingCombat = UnitAffectingCombat
local GetWeaponEnchantInfo = GetWeaponEnchantInfo
local UnitIsDead = UnitIsDead
local UnitClassification = UnitClassification
local UnitIsPlayer = UnitIsPlayer
local UnitName = UnitName
local UnitIsDeadOrGhost = UnitIsDeadOrGhost
local UnitCharacterPoints = UnitCharacterPoints
local GetShapeshiftForm = GetShapeshiftForm
local GetShapeshiftFormInfo = GetShapeshiftFormInfo
local GetInventoryItemBroken = GetInventoryItemBroken
local UnitOnTaxi = UnitOnTaxi
local IsSwimming = IsSwimming
local IsFalling = IsFalling
local IsIndoors = IsIndoors
local IsStealthed = IsStealthed
local GetMirrorTimerInfo = GetMirrorTimerInfo
local IsMounted = IsMounted
local IsInGroup = IsInGroup

local UnitIsTapDenied = UnitIsTapDenied
local IsAutoRepeatSpell = IsAutoRepeatSpell
local IsCurrentSpell = IsCurrentSpell
local UnitIsVisible = UnitIsVisible
local GetPetHappiness = GetPetHappiness

local ammoSlot = GetInventorySlotInfo("AmmoSlot")

-- Use Astrolabe function to get current player position
function DataToColor:GetPosition()
    if DataToColor.map ~= nil then
        local pos = C_Map.GetPlayerMapPosition(DataToColor.map, DataToColor.C.unitPlayer)
        if pos ~= nil then
            return pos:GetXY()
        end
    end
    return 0, 0
end

-- Returns bitmask values.
-- base2(1, 4) --> returns 16
-- base2(0, 9) --> returns 0
local function base2(number, power)
    return number > 0 and 2 ^ power or 0
end

local function sum24(number)
    return number % 0x1000000
end

-- Base 2 converter for up to 24 boolean values to a single pixel square.
function DataToColor:Bits1()
    -- 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384

    local mainHandEnchant, _, _, _, offHandEnchant = GetWeaponEnchantInfo()

    return base2(UnitAffectingCombat(DataToColor.C.unitTarget) and 1 or 0, 0) +
        base2(UnitIsDead(DataToColor.C.unitTarget) and 1 or 0, 1) +
        base2(UnitIsDeadOrGhost(DataToColor.C.unitPlayer) and 1 or 0, 2) +
        base2(UnitCharacterPoints(DataToColor.C.unitPlayer) > 0 and 1 or 0, 3) +
        base2(UnitExists(DataToColor.C.unitTarget) and CheckInteractDistance(DataToColor.C.unitTarget, 2) and 1 or 0, 4) +
        base2(DataToColor:isHostile(DataToColor.C.unitTarget), 5) +
        base2(UnitIsVisible(DataToColor.C.unitPet) and not UnitIsDead(DataToColor.C.unitPet) and 1 or 0, 6) +
        base2(mainHandEnchant and 1 or 0, 7) +
        base2(offHandEnchant and 1 or 0, 8) +
        base2(DataToColor:GetInventoryBroken(), 9) +
        base2(UnitOnTaxi(DataToColor.C.unitPlayer) and 1 or 0, 10) +
        base2(IsSwimming() and 1 or 0, 11) +
        base2(GetPetHappiness() == 3 and 1 or 0, 12) +
        base2(GetInventoryItemCount(DataToColor.C.unitPlayer, ammoSlot) > 0 and 1 or 0, 13) +
        base2(UnitAffectingCombat(DataToColor.C.unitPlayer) and 1 or 0, 14) +
        base2(DataToColor:IsTargetOfTargetPlayerOrPet(), 15) +
        base2(IsAutoRepeatSpell(DataToColor.C.Spell.AutoShotId) and 1 or 0, 16) +
        base2(UnitExists(DataToColor.C.unitTarget) and 1 or 0, 17) +
        base2(IsMounted() and 1 or 0, 18) +
        base2(IsAutoRepeatSpell(DataToColor.C.Spell.ShootId) and 1 or 0, 19) +
        base2(IsCurrentSpell(DataToColor.C.Spell.AttackId) and 1 or 0, 20) +
        base2(DataToColor:targetIsNormal(), 21) +
        base2(UnitIsTapDenied(DataToColor.C.unitTarget) and 1 or 0, 22) +
        base2(IsFalling() and 1 or 0, 23)
end

function DataToColor:Bits2()
    local type, _, _, scale = GetMirrorTimerInfo(2)
    return
        base2(type == DataToColor.C.MIRRORTIMER.BREATH and scale < 0 and 1 or 0, 0) +
        base2(DataToColor.corpseInRange, 1) +
        base2(IsIndoors() and 1 or 0, 2) +
        base2(UnitExists(DataToColor.C.unitFocus) and 1 or 0, 3) +
        base2(UnitAffectingCombat(DataToColor.C.unitFocus) and 1 or 0, 4) +
        base2(UnitExists(DataToColor.C.unitFocusTarget) and 1 or 0, 5) +
        base2(UnitAffectingCombat(DataToColor.C.unitFocusTarget) and 1 or 0, 6) +
        base2(DataToColor:isHostile(DataToColor.C.unitFocusTarget), 7) +
        base2(UnitExists(DataToColor.C.unitFocusTarget) and CheckInteractDistance(DataToColor.C.unitFocusTarget, 2) and 1 or 0, 8) +
        base2(UnitIsDead(DataToColor.C.unitPetTarget) and 1 or 0, 9) +
        base2(IsStealthed() and 1 or 0, 10)
end

function DataToColor:CustomTrigger(t)
    local v = 0
    for i = 0, 23 do
        v = v + base2(t[i], i)
    end
    return v
end

function DataToColor:Set(trigger, input)
    if input == true then input = 1 end
    local v = tonumber(input) or 0
    if v > 0 then v = 1 end
    if trigger >= 0 and trigger <= 23 then
        DataToColor.customTrigger1[trigger] = v
    end
end

function DataToColor:getAuraMaskForClass(func, unitId, table, queue)
    local num = 0
    for k, v in pairs(table) do
        for i = 1, 16 do
            local name, texture, _, _, _, expirationTime, source = func(unitId, i)
            if name == nil then
                break
            end

            if expirationTime > 0 then
                if not queue:exists(texture) then
                    queue:set(texture, expirationTime)
                    --DataToColor:Print(texture, " added ", expirationTime)
                elseif queue:value(texture) < expirationTime then
                    queue:set(texture, expirationTime)
                    --DataToColor:Print(texture, " updated ", expirationTime)
                end
            end

            if v[texture] or find(name, v[1]) then
                num = num + base2(1, k)
                break
            end
        end
    end
    return num
end

-- player debuffs cant be higher than 16!
function DataToColor:getAuraCount(func, unitId)
    local num = 0
    for i = 1, 16 do
        local name = func(unitId, i)
        if name == nil then
            break
        end
        num = num + 1
    end
    return num
end

-- Pass in a string to get the upper case ASCII values. Converts any special character with ASCII values below 100
local function StringToASCIIHex(str)
    -- Converts string to upper case so only 2 digit ASCII values
    -- All lowercase letters have a decimal ASCII value >100, so we only uppercase numbers which are a mere 2 digits long.
    str = sub(upper(str), 0, 6)
    -- Sets string to an empty string
    local ASCII = ''
    -- Loops through all of string passed to it and converts to upper case ASCII values
    for i = 1, len(str) do
        -- Assigns the specific value to a character to then assign to the ASCII string/number
        local c = sub(str, i, i)
        -- Concatenation of old string and new character
        ASCII = ASCII .. byte(c)
    end
    return tonumber(ASCII)
end

-- Grabs current targets name
function DataToColor:GetTargetName(partition)
    if UnitExists(DataToColor.C.unitTarget) then
        local target = GetUnitName(DataToColor.C.unitTarget)
        target = StringToASCIIHex(target)
        if partition < 3 then
            return tonumber(sub(target, 0, 6))
        else if target > 999999 then
                return tonumber(sub(target, 7, 12))
            end
        end
    end
    return 0
end

function DataToColor:CastingInfoSpellId(unitId)

    local UnitCastingInfo = { DataToColor.UnitCastingInfo(unitId) }
    local startTime = UnitCastingInfo[4]
    local endTime = UnitCastingInfo[5]
    local spellID = UnitCastingInfo[#UnitCastingInfo]

    if spellID ~= nil then
        if unitId == DataToColor.C.unitPlayer and startTime ~= DataToColor.lastCastStartTime then
            DataToColor.lastCastStartTime = startTime
            DataToColor.lastCastEndTime = endTime
            DataToColor.CastNum = DataToColor.CastNum + 1
        end
        return spellID
    end

    local UnitChannelInfo = { DataToColor.UnitChannelInfo(unitId) }
    startTime = UnitChannelInfo[4]
    endTime = UnitChannelInfo[5]
    spellID = UnitChannelInfo[#UnitChannelInfo]

    if spellID ~= nil then
        if unitId == DataToColor.C.unitPlayer and startTime ~= DataToColor.lastCastStartTime then
            DataToColor.lastCastStartTime = startTime
            DataToColor.lastCastEndTime = endTime
            DataToColor.CastNum = DataToColor.CastNum + 1
        end
        return spellID
    end

    if unitId == DataToColor.C.unitPlayer then
        DataToColor.lastCastEndTime = 0
    end

    return 0
end

--

function DataToColor:isHostile(unit)
    local hostile = UnitReaction(DataToColor.C.unitPlayer, unit)
    if hostile ~= nil and hostile <= 4 then
        return 1
    end
    return 0
end

function DataToColor:getRange()
    if UnitExists(DataToColor.C.unitTarget) then
        local min, max = Range:GetRange(DataToColor.C.unitTarget)
        if max == nil then
            max = 99
        end
        return min * 100000 + max * 100
    end
    return 0
end

function DataToColor:targetNpcId()
    local guid = UnitGUID(DataToColor.C.unitTarget) or ""
    local id = tonumber(guid:match("-(%d+)-%x+$"), 10)
    if id and guid:match("%a+") ~= "Player" then
        return id
    end
    return 0
end

function DataToColor:getGuidFromUnit(unit)
    -- Player-4731-02AAD4FF
    -- Creature-0-4488-530-222-19350-000005C0D70
    -- Pet-0-4448-530-222-22123-15004E200E
    if UnitExists(unit) then
        return DataToColor:uniqueGuid(select(-2, strsplit('-', UnitGUID(unit))))
    end
    return 0
end

function DataToColor:getGuidFromUUID(uuid)
    return DataToColor:uniqueGuid(select(-2, strsplit('-', uuid)))
end

function DataToColor:uniqueGuid(npcId, spawn)
    local spawnEpochOffset = band(tonumber(sub(spawn, 5), 16), 0x7fffff)
    local spawnIndex = band(tonumber(sub(spawn, 1, 5), 16), 0xffff8)

    local dd = date("*t", spawnEpochOffset)
    return sum24(
        dd.day +
        dd.hour +
        dd.min +
        dd.sec +
        npcId +
        spawnIndex
    )
end

local offsetEnumPowerType = 2
function DataToColor:populateActionbarCost(slot)
    if slot == nil then
        return
    end
    local actionType, id = GetActionInfo(slot)
    if actionType == DataToColor.C.ActionType.Macro then
        id = GetMacroSpell(id)
    end
    if id and actionType == DataToColor.C.ActionType.Spell or actionType == DataToColor.C.ActionType.Macro then
        local costTable = GetSpellPowerCost(id)
        if costTable ~= nil then
            for index, costInfo in ipairs(costTable) do
                --print(slot, actionType, index, costInfo.type, costInfo.cost, GetSpellLink(id))

                -- cost negative means it produces that type of powertype...
                if(costInfo.cost > 0) then
                    DataToColor.actionBarCostQueue:set(DataToColor.C.COST_MAX_COST_IDX * index + DataToColor.C.COST_MAX_POWER_TYPE * (costInfo.type + offsetEnumPowerType) + slot, costInfo.cost)
                end
            end
        end
    end
    -- default value mana with zero cost
    DataToColor.actionBarCostQueue:set((offsetEnumPowerType * DataToColor.C.COST_MAX_POWER_TYPE) + slot, 0)
end

function DataToColor:equipSlotItemId(slot)
    if slot == nil then
        return 0
    end
    local equip
    if GetInventoryItemLink(DataToColor.C.unitPlayer, slot) == nil then
        equip = 0
    else _, _, equip = find(GetInventoryItemLink(DataToColor.C.unitPlayer, slot), DataToColor.C.ItemPattern)
        equip = gsub(equip, 'm:', '')
    end
    return tonumber(equip or 0)
end

-- -- Function to tell if a spell is on cooldown and if the specified slot has a spell assigned to it
-- -- Slot ID information can be found on WoW Wiki. Slots we are using: 1-12 (main action bar), Bottom Right Action Bar maybe(49-60), and  Bottom Left (61-72)

function DataToColor:areSpellsInRange()
    local inRange = 0
    for i = 1, #DataToColor.S.spellInRangeList, 1 do
        local isInRange = IsSpellInRange(GetSpellInfo(DataToColor.S.spellInRangeList[i]), DataToColor.C.unitTarget)
        if isInRange == 1 then
            inRange = inRange + (2 ^ (i - 1))
        end
    end
    return inRange
end

function DataToColor:isActionUseable(min, max)
    local isUsableBits = 0
    for i = min, max do
        local start, duration, enabled = GetActionCooldown(i)
        local isUsable, notEnough = IsUsableAction(i)
        local texture = GetActionTexture(i)
        local spellName = DataToColor.S.playerSpellBook[texture]

        if start == 0 and (isUsable == true and notEnough == false or IsUsableSpell(spellName)) and texture ~= 134400 then -- red question mark texture
            isUsableBits = isUsableBits + (2 ^ (i - min))
        end

        -- exclude GCD - everything counts as GCD below 1.5
        if enabled == 1 and start ~= 0 and duration > 1.5 then
            local expireTime = start + duration
            if not DataToColor.actionBarCooldownQueue:exists(i) then
                DataToColor.actionBarCooldownQueue:set(i, expireTime)
            end
        end
    end
    return isUsableBits
end

function DataToColor:isCurrentAction(min, max)
    local isUsableBits = 0
    for i = min, max do
        if IsCurrentAction(i) or IsAutoRepeatAction(i) then
            isUsableBits = isUsableBits + (2 ^ (i - min))
        end
    end
    return isUsableBits
end

-- Finds passed in string to return profession level
function DataToColor:GetProfessionLevel(skill)
    local numskills = GetNumSkillLines()
    for c = 1, numskills do
        local skillname, _, _, skillrank = GetSkillLineInfo(c)
        if (skillname == skill) then
            return tonumber(skillrank)
        end
    end
    return 0
end

function DataToColor:GetCorpsePosition()
    if UnitIsGhost(DataToColor.C.unitPlayer) then
        local corpseMap = C_DeathInfo.GetCorpseMapPosition(DataToColor.map)
        if corpseMap ~= nil then
            return corpseMap:GetXY()
        end
    end
    return 0, 0
end

function DataToColor:getMeleeAttackSpeed(unit)
    local mainHand, offHand = UnitAttackSpeed(unit)
    if not mainHand then
        mainHand = 0
    end

    if not offHand then
        offHand = 0
    end
    return 10000 * math.floor(mainHand * 100) + math.floor(offHand * 100)
end

-----------------------------------------------------------------
-- Boolean functions --------------------------------------------
-- Only put functions here that are part of a boolean sequence --
-- Sew BELOW for examples ---------------------------------------
-----------------------------------------------------------------

function DataToColor:targetIsNormal()
    local classification = UnitClassification(DataToColor.C.unitTarget)
    if classification == DataToColor.C.unitNormal then
        if (UnitIsPlayer(DataToColor.C.unitTarget)) then
            return 0
        end

        if UnitName(DataToColor.C.unitPet) == UnitName(DataToColor.C.unitTarget) then
            return 0
        end

        return 1
        -- if target is not in combat, return 1 for bitmask
    else
        return 0
    end
end

function DataToColor:shapeshiftForm()
    local index = GetShapeshiftForm(false)
    if index == nil or index == 0 then
        return 0
    end

    local _, _, _, spellId = GetShapeshiftFormInfo(index)
    local form = DataToColor.S.playerAuraMap[spellId]
    if form ~= nil then
        return form
    end
    return index
end

function DataToColor:GetInventoryBroken()
    for i = 1, 18 do
        if GetInventoryItemBroken(DataToColor.C.unitPlayer, i) then
            return 1
        end
    end
    return 0
end

function DataToColor:TargetOfTargetAsNumber()
    if not (UnitName(DataToColor.C.unitTargetTarget)) then return 2 end -- target has no target
    if DataToColor.C.CHARACTER_NAME == UnitName(DataToColor.C.unitTarget) then return 0 end -- targeting self
    if UnitName(DataToColor.C.unitPet) == UnitName(DataToColor.C.unitTargetTarget) then return 4 end -- targetting my pet
    if DataToColor.playerPetSummons[UnitGUID(DataToColor.C.unitTargetTarget)] then return 4 end
    if DataToColor.C.CHARACTER_NAME == UnitName(DataToColor.C.unitTargetTarget) then return 1 end -- targetting me
    if UnitName(DataToColor.C.unitPet) == UnitName(DataToColor.C.unitTarget) and
        UnitName(DataToColor.C.unitTargetTarget) ~= nil then return 5 end
    if IsInGroup() and DataToColor:TargetTargetsPartyOrPet() then return 6 end
    return 3
end

function DataToColor:TargetTargetsPartyOrPet()
    for i = 1, 4 do
        local unit = DataToColor.C.unitParty .. i
        if UnitExists(unit) == false then
            return false
        end
        local name = UnitName(unit)
        if name == UnitName(DataToColor.C.unitTargetTarget) then return true end

        unit = DataToColor.C.unitParty .. i .. DataToColor.C.unitPet
        if UnitExists(unit) == false then
            return false
        end
        name = UnitName(unit)
        if name == UnitName(DataToColor.C.unitTargetTarget) then return true end
    end
    return false
end

-- Returns true if target of our target is us
function DataToColor:IsTargetOfTargetPlayerOrPet()
    local x = DataToColor:TargetOfTargetAsNumber()
    if x == 1 or x == 4 then return 1 else return 0 end
end
