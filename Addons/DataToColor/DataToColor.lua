----------------------------------------------------------------------------
--  DataToColor
----------------------------------------------------------------------------

-- Trigger between emitting game data and frame location data
local SETUP_SEQUENCE = false
-- Total number of data frames generated
local NUMBER_OF_FRAMES = 100
-- Set number of pixel rows
local FRAME_ROWS = 1
-- Size of data squares in px. Varies based on rounding errors as well as dimension size. Use as a guideline, but not 100% accurate.
local CELL_SIZE = 1 -- 1-9
-- Spacing in px between data squares.
local CELL_SPACING = 1 -- 0 or 1

-- Dont modify values below

local Load = select(2, ...)
local DataToColor = unpack(Load)

local band = bit.band
local rshift = bit.rshift
local floor = math.floor
local max = math.max

local strjoin = strjoin
local strfind = strfind
local debugstack = debugstack
local ceil = ceil
local GetTime = GetTime

local UIParent = UIParent
local BackdropTemplateMixin = BackdropTemplateMixin
local C_Map = C_Map

local GetNetStats = GetNetStats

local CreateFrame = CreateFrame
local SetCVar = SetCVar
local GetAddOnMetadata = GetAddOnMetadata

local UIErrorsFrame = UIErrorsFrame
local DEFAULT_CHAT_FRAME = DEFAULT_CHAT_FRAME

local HasAction = HasAction
local GetSpellBookItemName = GetSpellBookItemName
local GetNumTalentTabs = GetNumTalentTabs
local GetNumTalents = GetNumTalents
local GetTalentInfo = GetTalentInfo

local GetPlayerFacing = GetPlayerFacing
local UnitLevel = UnitLevel
local UnitHealthMax = UnitHealthMax
local UnitHealth = UnitHealth
local UnitPowerMax = UnitPowerMax
local UnitPower = UnitPower

local GetContainerNumFreeSlots = GetContainerNumFreeSlots
local GetContainerItemInfo = GetContainerItemInfo
local GetRuneCooldown = GetRuneCooldown
local GetRuneType = GetRuneType

local UnitBuff = UnitBuff
local UnitDebuff = UnitDebuff
local UnitXP = UnitXP
local UnitXPMax = UnitXPMax
local UnitExists = UnitExists
local UnitGUID = UnitGUID

local PowerType = Enum.PowerType

local GetMoney = GetMoney

local GetContainerNumSlots = GetContainerNumSlots
local GetComboPoints = GetComboPoints

local GetContainerItemLink = GetContainerItemLink
local PickupContainerItem = PickupContainerItem
local DeleteCursorItem = DeleteCursorItem
local GetMerchantItemLink = GetMerchantItemLink
local GetItemInfo = GetItemInfo
local GetCoinTextureString = GetCoinTextureString
local UseContainerItem = UseContainerItem

-- initialization
local globalCounter = 0
local initPhase = 10

DataToColor.DATA_CONFIG = {
    ACCEPT_PARTY_REQUESTS = false, -- O
    DECLINE_PARTY_REQUESTS = false, -- O
    AUTO_REPAIR_ITEMS = true, -- O
    AUTO_RESURRECT = true,
    AUTO_SELL_GREY_ITEMS = true
}

-- How often item frames change
local ITEM_ITERATION_FRAME_CHANGE_RATE = 5
-- How often the actionbar frames change
local ACTION_BAR_ITERATION_FRAME_CHANGE_RATE = 5
-- How often the gossip frames change
local GOSSIP_ITERATION_FRAME_CHANGE_RATE = 5
-- How often the spellbook frames change
local SPELLBOOK_ITERATION_FRAME_CHANGE_RATE = 5
-- How often the spellbook frames change
local TALENT_ITERATION_FRAME_CHANGE_RATE = 5
-- How often the spellbook frames change
local COMBAT_LOG_ITERATION_FRAME_CHANGE_RATE = 5
-- How often the check network latency
local LATENCY_ITERATION_FRAME_CHANGE_RATE = 500 -- 500ms * refresh rate in ms
-- How often the lastLoot return from Closed to Corpse
local LOOT_RESET_RATE = 5
-- How often the Player Buff / target Debuff frames change
local AURA_DURATION_ITERATION_FRAME_CHANGE_RATE = 5

-- Action bar configuration for which spells are tracked
local MAX_ACTIONBAR_SLOT = 120

-- Timers
DataToColor.globalTime = 0
DataToColor.lastLoot = 0
DataToColor.lastLootResetStart = 0

DataToColor.map = C_Map.GetBestMapForUnit(DataToColor.C.unitPlayer)
DataToColor.uiMapId = 0
DataToColor.uiErrorMessage = 0
DataToColor.gcdExpirationTime = 0

DataToColor.lastAutoShot = 0
DataToColor.lastMainHandMeleeSwing = 0
DataToColor.lastCastEvent = 0
DataToColor.lastCastSpellId = 0
DataToColor.lastCastGCD = 0

DataToColor.lastCastStartTime = 0
DataToColor.lastCastEndTime = 0
DataToColor.CastNum = 0

DataToColor.targetChanged = true

DataToColor.playerGUID = UnitGUID(DataToColor.C.unitPlayer)
DataToColor.petGUID = UnitGUID(DataToColor.C.unitPet)

DataToColor.corpseInRange = 0

local bagCache = {}

DataToColor.equipmentQueue = DataToColor.Queue:new()
DataToColor.bagQueue = DataToColor.Queue:new()
DataToColor.inventoryQueue = DataToColor.Queue:new()
DataToColor.gossipQueue = DataToColor.Queue:new()
DataToColor.actionBarCostQueue = DataToColor.struct:new()
DataToColor.actionBarCooldownQueue = DataToColor.struct:new()
DataToColor.spellBookQueue = DataToColor.Queue:new()
DataToColor.talentQueue = DataToColor.Queue:new()

DataToColor.CombatDamageDoneQueue = DataToColor.Queue:new()
DataToColor.CombatDamageTakenQueue = DataToColor.Queue:new()
DataToColor.CombatCreatureDiedQueue = DataToColor.Queue:new()
DataToColor.CombatMissTypeQueue = DataToColor.Queue:new()

DataToColor.playerPetSummons = {}

DataToColor.playerBuffTime = DataToColor.struct:new()
DataToColor.targetDebuffTime = DataToColor.struct:new()

DataToColor.customTrigger1 = {}

function DataToColor:RegisterSlashCommands()
    DataToColor:RegisterChatCommand('dc', 'StartSetup')
    DataToColor:RegisterChatCommand('dccpu', 'GetCPUImpact')
    DataToColor:RegisterChatCommand('dcflush', 'FushState')
end

function DataToColor:StartSetup()
    if not SETUP_SEQUENCE then
        SETUP_SEQUENCE = true
    else
        SETUP_SEQUENCE = false
    end
end

function DataToColor:Print(...)
    DEFAULT_CHAT_FRAME:AddMessage(strjoin('', '|cff00b3ff', 'DataToColor:|r ', ...))
end

function DataToColor:error(msg)
    DataToColor:log("|cff0000ff" .. msg .. "|r")
    DataToColor:log(msg)
    DataToColor:log(debugstack())
    error(msg)
end

-- This function runs when addon is initialized/player logs in
function DataToColor:OnInitialize()
    DataToColor:SetupRequirements()
    DataToColor:CreateFrames(NUMBER_OF_FRAMES)
    DataToColor:RegisterSlashCommands()

    DataToColor:InitStorage()

    UIErrorsFrame:UnregisterEvent("UI_ERROR_MESSAGE")

    DataToColor:RegisterEvents()

    local version = GetAddOnMetadata('DataToColor', 'Version')
    DataToColor:Print("Welcome. Using " .. version)

    DataToColor:InitUpdateQueues()
    DataToColor:InitTrigger(DataToColor.customTrigger1)
end

function DataToColor:SetupRequirements()
    SetCVar("autoInteract", 1)
    SetCVar("autoLootDefault", 1)
    -- /run SetCVar("cameraSmoothStyle", 2) -- always
    SetCVar('Contrast', 50, '[]')
    SetCVar('Brightness', 50, '[]')
    SetCVar('Gamma', 1, '[]')
end

function DataToColor:Reset()
    DataToColor.S.playerSpellBook = {}

    DataToColor.playerGUID = UnitGUID(DataToColor.C.unitPlayer)
    DataToColor.petGUID = UnitGUID(DataToColor.C.unitPet)
    DataToColor.map = C_Map.GetBestMapForUnit(DataToColor.C.unitPlayer)

    DataToColor.globalTime = 0
    DataToColor.lastLoot = 0
    DataToColor.uiErrorMessage = 0
    DataToColor.gcdExpirationTime = 0

    DataToColor.lastAutoShot = 0
    DataToColor.lastMainHandMeleeSwing = 0
    DataToColor.lastCastEvent = 0
    DataToColor.lastCastSpellId = 0
    DataToColor.lastCastGCD = 0

    DataToColor.lastCastStartTime = 0
    DataToColor.CastNum = 0

    DataToColor.corpseInRange = 0

    globalCounter = 0

    bagCache = {}

    DataToColor.actionBarCooldownQueue = DataToColor.struct:new()

    DataToColor.playerBuffTime = DataToColor.struct:new()
    DataToColor.targetDebuffTime = DataToColor.struct:new()

    DataToColor.playerPetSummons = {}
end

function DataToColor:Update()
    DataToColor.globalTime = DataToColor.globalTime + 1
    if DataToColor.globalTime > (256 * 256 * 256 - 1) then
        -- overflow wont trigger init state at backend
        DataToColor.globalTime = initPhase
    end
end

function DataToColor:FushState()
    DataToColor.targetChanged = true

    DataToColor:Reset()

    DataToColor:InitEquipmentQueue()
    DataToColor:InitBagQueue()

    DataToColor:InitInventoryQueue(4)
    DataToColor:InitInventoryQueue(3)
    DataToColor:InitInventoryQueue(2)
    DataToColor:InitInventoryQueue(1)
    DataToColor:InitInventoryQueue(0)

    DataToColor:InitActionBarCostQueue()
    DataToColor:InitSpellBookQueue()
    DataToColor:InitTalentQueue()

    DataToColor:Print('Flush State')
end

function DataToColor:ConsumeChanges()
    if DataToColor.targetChanged then
        DataToColor.targetChanged = false
    end
end

function DataToColor:InitUpdateQueues()
    DataToColor:InitEquipmentQueue()
    DataToColor:InitBagQueue()

    DataToColor:InitInventoryQueue(4)
    DataToColor:InitInventoryQueue(3)
    DataToColor:InitInventoryQueue(2)
    DataToColor:InitInventoryQueue(1)
    DataToColor:InitInventoryQueue(0)

    DataToColor:InitActionBarCostQueue()
    DataToColor:InitSpellBookQueue()
    DataToColor:InitTalentQueue()
end

function DataToColor:InitEquipmentQueue()
    for eqNum = 1, 23 do
        DataToColor.equipmentQueue:push(eqNum)
    end
end

function DataToColor:InitInventoryQueue(containerID)
    if containerID >= 0 and containerID <= 4 then
        for i = 1, GetContainerNumSlots(containerID) do
            if DataToColor:BagSlotChanged(containerID, i) then
                DataToColor.inventoryQueue:push(containerID * 1000 + i)
            end
        end
    end
end

function DataToColor:BagSlotChanged(container, slot)
    local _, count, _, _, _, _, _, _, _, id = GetContainerItemInfo(container, slot)

    if id == nil then
        count = 0
        id = 0
    end

    local index = container * 1000 + slot
    if bagCache[index] == nil or bagCache[index].id ~= id or bagCache[index].count ~= count then
        bagCache[index] = { id = id, count = count }
        return true
    end

    return false
end

function DataToColor:InitBagQueue(min, max)
    min = min or 0
    max = max or 4
    for bag = min, max do
        DataToColor.bagQueue:push(bag)
    end
end

function DataToColor:InitActionBarCostQueue()
    for slot = 1, MAX_ACTIONBAR_SLOT do
        if HasAction(slot) then
            DataToColor:populateActionbarCost(slot)
        end
    end
end

function DataToColor:InitSpellBookQueue()
    local num, type = 1, 1
    while true do
        local name, _, id = GetSpellBookItemName(num, type)
        if not name then
            break
        end

        local texture = GetSpellBookItemTexture(num, type)
        DataToColor.S.playerSpellBook[texture] = name

        DataToColor.spellBookQueue:push(id)
        num = num + 1
    end
end

function DataToColor:InitTalentQueue()
    for tab = 1, GetNumTalentTabs(false, false) do
        for i = 1, GetNumTalents(tab) do
            local _, _, tier, column, currentRank = GetTalentInfo(tab, i)
            if currentRank > 0 then
                --                     1-3 +         1-11 +          1-4 +         1-5
                local hash = tab * 1000000 + tier * 10000 + column * 10 + currentRank
                DataToColor.talentQueue:push(hash)
                --DataToColor:Print("talentQueue tab:"..tab.." | tier: "..tier.." | column: "..column.." | rank: "..currentRank)
            end
        end
    end
end

function DataToColor:InitTrigger(t)
    for i = 0, 23 do
        t[i] = 0
    end
end

-- Function to mass generate all of the initial frames for the pixel reader
function DataToColor:CreateFrames(n)

    local valueCache = {}
    local frames = {}

    -- This function is able to pass numbers in range 0 to 16777215
    -- r,g,b are integers in range 0-255
    -- then we turn them into 0-1 range
    local function int(self, i)
        return band(rshift(i, 16), 255) / 255, band(rshift(i, 8), 255) / 255, band(i, 255) / 255, 1
    end

    -- This function is able to pass numbers in range 0 to 9.99999 (6 digits)
    -- converting them to a 6-digit integer.
    local function float(self, f)
        return int(self, floor(f * 100000))
    end

    local function Pixel(func, value, slot)
        if valueCache[slot + 1] ~= value then
            valueCache[slot + 1] = value
            frames[slot + 1]:SetBackdropColor(func(self, value))
        end
    end

    local function UpdateGlobalTime(slot)
        Pixel(int, DataToColor.globalTime, slot)
    end

    local function updateFrames()
        if not SETUP_SEQUENCE and globalCounter >= initPhase then

            Pixel(int, 0, 0)
            -- The final data square, reserved for additional metadata.
            Pixel(int, 2000001, n - 1)

            local x, y = DataToColor:GetPosition()
            Pixel(float, x * 10, 1)
            Pixel(float, y * 10, 2)

            Pixel(float, GetPlayerFacing() or 0, 3)
            Pixel(int, DataToColor.map or 0, 4) -- MapUIId
            Pixel(int, UnitLevel(DataToColor.C.unitPlayer), 5)

            local cx, cy = DataToColor:GetCorpsePosition()
            Pixel(float, cx * 10, 6)
            Pixel(float, cy * 10, 7)

            -- Boolean variables
            Pixel(int, DataToColor:Bits1(), 8)
            Pixel(int, DataToColor:Bits2(), 9)

            Pixel(int, UnitHealthMax(DataToColor.C.unitPlayer), 10)
            Pixel(int, UnitHealth(DataToColor.C.unitPlayer), 11)

            Pixel(int, UnitPowerMax(DataToColor.C.unitPlayer, nil), 12) -- either mana, rage, energy
            Pixel(int, UnitPower(DataToColor.C.unitPlayer, nil), 13) -- either mana, rage, energy

            if(DataToColor.C.CHARACTER_CLASS_ID == 6) then -- death Knight

                local bloodRunes = 0
                local unholyRunes = 0
                local frostRunes = 0
                local deathRunes = 0
                local numRunes = 0

                for index = 1, 6 do
                  local startTime = GetRuneCooldown(index)
                  if startTime == 0 then
                    numRunes = numRunes + 1
                    local runeType = GetRuneType(index)
                    if runeType == 1 then
                      bloodRunes = bloodRunes + 1
                    elseif runeType == 2 then
                      frostRunes = frostRunes + 1
                    elseif runeType == 3 then
                      unholyRunes = unholyRunes + 1
                    elseif runeType == 4 then
                        deathRunes = deathRunes + 1
                    end
                  end
                end

                bloodRunes  = bloodRunes  + deathRunes
                unholyRunes = unholyRunes + deathRunes
                frostRunes  = frostRunes  + deathRunes

                Pixel(int, numRunes, 14)
                Pixel(int, bloodRunes * 100 + frostRunes * 10 + unholyRunes, 15)
            else
                Pixel(int, UnitPowerMax(DataToColor.C.unitPlayer, PowerType.Mana), 14)
                Pixel(int, UnitPower(DataToColor.C.unitPlayer, PowerType.Mana), 15)
            end

            if DataToColor.targetChanged then
                Pixel(int, DataToColor:GetTargetName(0), 16) -- Characters 1-3 of targets name
                Pixel(int, DataToColor:GetTargetName(3), 17) -- Characters 4-6 of targets name

                Pixel(int, UnitHealthMax(DataToColor.C.unitTarget), 18)
            end

            Pixel(int, UnitHealth(DataToColor.C.unitTarget), 19)

            if globalCounter % ITEM_ITERATION_FRAME_CHANGE_RATE == 0 then
                -- 20
                local bagNum = DataToColor.bagQueue:shift()
                if bagNum then
                    local freeSlots, bagType = GetContainerNumFreeSlots(bagNum)
                    if not bagType then
                        bagType = 0
                    end

                    -- BagType + Index + FreeSpace + BagSlots
                    Pixel(int, bagType * 1000000 + bagNum * 100000 + freeSlots * 1000 + GetContainerNumSlots(bagNum), 20)
                    --DataToColor:Print("bagQueue bagType:", bagType, " | bagNum: ", bagNum, " | freeSlots: ", freeSlots, " | BagSlots: ", GetContainerNumSlots(bagNum))
                else
                    Pixel(int, 0, 20)
                end

                -- 21 22
                local bagSlotNum = DataToColor.inventoryQueue:shift()
                if bagSlotNum then

                    bagNum = floor(bagSlotNum / 1000)
                    bagSlotNum = bagSlotNum - (bagNum * 1000)

                    local _, itemCount, _, _, _, _, _, _, _, itemID = GetContainerItemInfo(bagNum, bagSlotNum)

                    if itemID == nil then
                        itemCount = 0
                        itemID = 0
                    end

                    --DataToColor:Print("inventoryQueue: "..bagNum.. " "..bagSlotNum.." -> id:"..itemID.." c:"..itemCount)

                    -- 0-4 bagNum + 1-21 itenNum + 1-1000 quantity
                    Pixel(int, bagNum * 1000000 + bagSlotNum * 10000 + itemCount, 21)

                    -- itemId 1-999999
                    Pixel(int, itemID, 22)
                else
                    Pixel(int, 0, 21)
                    Pixel(int, 0, 22)
                end

                -- 23 24
                local equipmentSlot = DataToColor.equipmentQueue:shift()
                Pixel(int, equipmentSlot or 0, 23)
                Pixel(int, DataToColor:equipSlotItemId(equipmentSlot), 24)
                --DataToColor:Print("equipmentQueue "..equipmentSlot.." -> "..itemId)
            end

            Pixel(int, DataToColor:isCurrentAction(1, 24), 25)
            Pixel(int, DataToColor:isCurrentAction(25, 48), 26)
            Pixel(int, DataToColor:isCurrentAction(49, 72), 27)
            Pixel(int, DataToColor:isCurrentAction(73, 96), 28)
            Pixel(int, DataToColor:isCurrentAction(97, 120), 29)

            Pixel(int, DataToColor:isActionUseable(1, 24), 30)
            Pixel(int, DataToColor:isActionUseable(25, 48), 31)
            Pixel(int, DataToColor:isActionUseable(49, 72), 32)
            Pixel(int, DataToColor:isActionUseable(73, 96), 33)
            Pixel(int, DataToColor:isActionUseable(97, 120), 34)

            if globalCounter % ACTION_BAR_ITERATION_FRAME_CHANGE_RATE == 0 then
                local costMeta, costValue = DataToColor.actionBarCostQueue:get()
                if costMeta and costValue then
                    --DataToColor:Print("actionBarCostQueue: ", costMeta, " ", costValue)
                    DataToColor.actionBarCostQueue:remove(costMeta)
                end
                Pixel(int, costMeta or 0, 35)
                Pixel(int, costValue or 0, 36)

                local actionCDSlot, actionCDExpireTime = DataToColor.actionBarCooldownQueue:get()
                if actionCDSlot then
                    DataToColor.actionBarCooldownQueue:setDirty(actionCDSlot)

                    local duration = max(0, ceil(actionCDExpireTime - GetTime()))
                    local valueMs = duration * 100

                    --DataToColor:Print("actionBarCooldownQueue: ", actionCDSlot, " ", valueMs)
                    Pixel(int, actionCDSlot * 100000 + valueMs, 37)

                    if duration == 0 then
                        DataToColor.actionBarCooldownQueue:remove(actionCDSlot)
                        --DataToColor:Print("actionBarCooldownQueue: expired ", actionCDSlot, " ", valueMs)
                    end
                else
                    Pixel(int, 0, 37)
                end
            end

            Pixel(int, UnitHealthMax(DataToColor.C.unitPet), 38)
            Pixel(int, UnitHealth(DataToColor.C.unitPet), 39)

            Pixel(int, DataToColor:areSpellsInRange(), 40)
            Pixel(int, DataToColor:getAuraMaskForClass(UnitBuff, DataToColor.C.unitPlayer, DataToColor.S.playerBuffs, DataToColor.playerBuffTime), 41)
            Pixel(int, DataToColor:getAuraMaskForClass(UnitDebuff, DataToColor.C.unitTarget, DataToColor.S.targetDebuffs, DataToColor.targetDebuffTime), 42)
            Pixel(int, UnitLevel(DataToColor.C.unitTarget), 43)

            -- Amount of money in coppers
            Pixel(int, GetMoney() % 1000000, 44) -- Represents amount of money held (in copper)
            Pixel(int, floor(GetMoney() / 1000000), 45) -- Represents amount of money held (in gold) 

            Pixel(int, DataToColor.C.CHARACTER_RACE_ID * 100 + DataToColor.C.CHARACTER_CLASS_ID, 46)
            -- 47 empty
            Pixel(int, DataToColor:shapeshiftForm(), 48) -- Shapeshift id https://wowwiki.fandom.com/wiki/API_GetShapeshiftForm
            Pixel(int, DataToColor:getRange(), 49) -- Represents minRange-maxRange ex. 0-5 5-15

            Pixel(int, UnitXP(DataToColor.C.unitPlayer), 50)
            Pixel(int, UnitXPMax(DataToColor.C.unitPlayer), 51)
            Pixel(int, DataToColor.uiErrorMessage, 52) -- Last UI Error message
            DataToColor.uiErrorMessage = 0

            Pixel(int, DataToColor:CastingInfoSpellId(DataToColor.C.unitPlayer), 53) -- SpellId being cast
            Pixel(int, GetComboPoints(DataToColor.C.unitPlayer, DataToColor.C.unitTarget) or 0, 54)

            local playerDebuffCount = DataToColor:getAuraCount(UnitDebuff, DataToColor.C.unitPlayer)
            local playerBuffCount = DataToColor:getAuraCount(UnitBuff, DataToColor.C.unitPlayer)

            local targetDebuffCount = 0
            local targetBuffCount = 0

            if UnitExists(DataToColor.C.unitTarget) then
                targetDebuffCount = DataToColor:getAuraCount(UnitDebuff, DataToColor.C.unitTarget)
                targetBuffCount = DataToColor:getAuraCount(UnitBuff, DataToColor.C.unitTarget)
            end

            -- player/target buff and debuff counts
            -- formula playerDebuffCount + playerBuffCount + targetDebuffCount + targetBuffCount
            Pixel(int, playerDebuffCount * 1000000 + playerBuffCount * 10000 + targetDebuffCount * 100 + targetBuffCount, 55)

            if DataToColor.targetChanged then
                Pixel(int, DataToColor:targetNpcId(), 56) -- target id
                Pixel(int, DataToColor:getGuidFromUnit(DataToColor.C.unitTarget), 57)
            end

            Pixel(int, DataToColor:CastingInfoSpellId(DataToColor.C.unitTarget), 58) -- SpellId being cast by target

            Pixel(int, DataToColor:TargetOfTargetAsNumber(), 59)

            Pixel(int, DataToColor.lastAutoShot, 60)
            Pixel(int, DataToColor.lastMainHandMeleeSwing, 61)
            Pixel(int, DataToColor.lastCastEvent, 62)
            Pixel(int, DataToColor.lastCastSpellId, 63)

            if globalCounter % COMBAT_LOG_ITERATION_FRAME_CHANGE_RATE == 0 then
                Pixel(int, DataToColor.CombatDamageDoneQueue:shift() or 0, 64)
                Pixel(int, DataToColor.CombatDamageTakenQueue:shift() or 0, 65)
                Pixel(int, DataToColor.CombatCreatureDiedQueue:shift() or 0, 66)
                Pixel(int, DataToColor.CombatMissTypeQueue:shift() or 0, 67)
            end

            Pixel(int, DataToColor:getGuidFromUnit(DataToColor.C.unitPet), 68)
            Pixel(int, DataToColor:getGuidFromUnit(DataToColor.C.unitPetTarget), 69)
            Pixel(int, DataToColor.CastNum, 70)

            if globalCounter % SPELLBOOK_ITERATION_FRAME_CHANGE_RATE == 0 then
                Pixel(int, DataToColor.spellBookQueue:shift() or 0, 71)
            end

            if globalCounter % TALENT_ITERATION_FRAME_CHANGE_RATE == 0 then
                Pixel(int, DataToColor.talentQueue:shift() or 0, 72)
            end

            if globalCounter % GOSSIP_ITERATION_FRAME_CHANGE_RATE == 0 then
                local gossipNum = DataToColor.gossipQueue:shift()
                if gossipNum then
                    --DataToColor:Print("gossipQueue:" .. gossipNum)
                    Pixel(int, gossipNum, 73)
                end
            end

            Pixel(int, DataToColor:CustomTrigger(DataToColor.customTrigger1), 74)
            Pixel(int, DataToColor:getMeleeAttackSpeed(DataToColor.C.unitPlayer), 75)

            -- 76 rem cast time
            local remainCastTime = floor(DataToColor.lastCastEndTime - GetTime() * 1000)
            Pixel(int, max(0, remainCastTime), 76)

            if UnitExists(DataToColor.C.unitFocus) then
                Pixel(int, DataToColor:getGuidFromUnit(DataToColor.C.unitFocus), 77)
                Pixel(int, DataToColor:getGuidFromUnit(DataToColor.C.unitFocusTarget), 78)
            end

            if globalCounter % AURA_DURATION_ITERATION_FRAME_CHANGE_RATE == 0 then
                local textureId, expireTime = DataToColor.playerBuffTime:get()
                if textureId then
                    DataToColor.playerBuffTime:setDirty(textureId)

                    local durationSec = max(0, ceil(expireTime - GetTime()))
                    --DataToColor:Print("player buff update ", textureId, " ", durationSec)
                    Pixel(int, textureId, 79)
                    Pixel(int, durationSec, 80)

                    if durationSec == 0 then
                        DataToColor.playerBuffTime:remove(textureId)
                        --DataToColor:Print("player buff expired ", textureId, " ", durationSec)
                    end
                else
                    Pixel(int, 0, 79)
                    Pixel(int, 0, 80)
                end

                if UnitExists(DataToColor.C.unitTarget) then
                    textureId, expireTime = DataToColor.targetDebuffTime:get()
                else
                    textureId, expireTime = DataToColor.targetDebuffTime:getForced()
                    expireTime = GetTime()
                end

                if textureId then
                    DataToColor.targetDebuffTime:setDirty(textureId)

                    local durationSec = max(0, ceil(expireTime - GetTime()))
                    --DataToColor:Print("target debuff update ", textureId, " ", durationSec)
                    Pixel(int, textureId, 81)
                    Pixel(int, durationSec, 82)

                    if durationSec == 0 then
                        DataToColor.targetDebuffTime:remove(textureId)
                        --DataToColor:Print("target debuff expired ", textureId, " ", durationSec)
                    end
                else
                    Pixel(int, 0, 81)
                    Pixel(int, 0, 82)
                end
            end

            -- 94 last cast GCD
            Pixel(int, DataToColor.lastCastGCD, 94)

            -- 95 gcd
            local gcd = floor((DataToColor.gcdExpirationTime - GetTime()) * 1000)
            Pixel(int, max(0, gcd), 95)

            if globalCounter % LATENCY_ITERATION_FRAME_CHANGE_RATE == 0 then
                local _, _, lagHome, lagWorld = GetNetStats()
                Pixel(int, max(lagHome, lagWorld), 96)
            end

            -- Timers
            if DataToColor.lastLoot == DataToColor.C.Loot.Closed and
                DataToColor.globalTime - DataToColor.lastLootResetStart > LOOT_RESET_RATE then
                DataToColor.lastLoot = DataToColor.C.Loot.Corpse
            end
            Pixel(int, DataToColor.lastLoot, 97)
            UpdateGlobalTime(98)
            -- 99 Reserved

            DataToColor:ConsumeChanges()

            DataToColor:HandlePlayerInteractionEvents()

            DataToColor:Update()
        elseif not SETUP_SEQUENCE then
            if globalCounter < initPhase then
                for i = 1, n - 1 do
                    Pixel(int, 0, i)
                end
            end
            UpdateGlobalTime(98)
        end

        if SETUP_SEQUENCE then
            -- Emits meta data in data square index 0 concerning our estimated cell size, number of rows, and the numbers of frames
            Pixel(int, CELL_SPACING * 10000000 + CELL_SIZE * 100000 + 1000 * FRAME_ROWS + n, 0)
            -- Assign pixel squares a value equivalent to their respective indices.
            for i = 1, n - 1 do
                Pixel(int, i, i)
            end
        end

        globalCounter = globalCounter + 1
    end

    local function genFrame(name, x, y)
        local f = CreateFrame("Frame", name, UIParent, BackdropTemplateMixin and "BackdropTemplate") or CreateFrame("Frame", name, UIParent)

        local xx = x * floor(CELL_SIZE + CELL_SPACING)
        local yy = floor(-y * (CELL_SIZE + CELL_SPACING))
        --DataToColor:Print(name, " ", xx, " ", yy)

        f:SetPoint("TOPLEFT", xx, yy)
        f:SetHeight(CELL_SIZE)
        f:SetWidth(CELL_SIZE)
        f:SetBackdrop({
            bgFile = "Interface\\AddOns\\DataToColor\\white.tga",
            insets = { top = 0, left = 0, bottom = 0, right = 0 },
        })
        f:SetFrameStrata("TOOLTIP")
        f:SetBackdropColor(0, 0, 0, 1)
        return f
    end

    -- background frame
    local backgroundframe = genFrame("frame_0", 0, 0)
    backgroundframe:SetHeight(FRAME_ROWS * (CELL_SIZE + CELL_SPACING))
    backgroundframe:SetWidth(ceil(n / FRAME_ROWS) * (CELL_SIZE + CELL_SPACING))
    backgroundframe:SetFrameStrata("FULLSCREEN_DIALOG")
    backgroundframe:SetBackdropColor(0, 0, 0, 1)

    for frame = 0, n - 1 do
        -- those are grid coordinates (1,2,3,4 by  1,2,3,4 etc), not pixel coordinates
        local y = frame % FRAME_ROWS
        local x = floor(frame / FRAME_ROWS)
        frames[frame + 1] = genFrame("frame_" .. tostring(frame), x, y)
        valueCache[frame + 1] = -1
    end

    frames[1]:SetScript("OnUpdate", updateFrames)
end

function DataToColor:delete(items)
    for b = 0, 4 do
        for s = 1, GetContainerNumSlots(b) do
            local n = GetContainerItemLink(b, s)
            if n then
                for i = 1, #items, 1 do
                    if strfind(n, items[i]) then
                        DataToColor:Print("Delete: ", items[i])
                        PickupContainerItem(b, s)
                        DeleteCursorItem()
                    end
                end
            end
        end
    end
end

function DataToColor:sell(items)
    if UnitExists(DataToColor.C.unitTarget) then
        local item = GetMerchantItemLink(1)
        if item ~= nil then
            DataToColor:Print("Selling items...")
            DataToColor:OnMerchantShow()
            local TotalPrice = 0
            for b = 0, 4 do
                for s = 1, GetContainerNumSlots(b) do
                    local CurrentItemLink = GetContainerItemLink(b, s)
                    if CurrentItemLink then
                        for i = 1, #items, 1 do
                            if strfind(CurrentItemLink, items[i]) then
                                local _, _, itemRarity, _, _, _, _, _, _, _, itemSellPrice = GetItemInfo(CurrentItemLink)
                                if (itemRarity < 2) then
                                    local _, itemCount = GetContainerItemInfo(b, s)
                                    TotalPrice = TotalPrice + (itemSellPrice * itemCount)
                                    DataToColor:Print("Selling: ", itemCount, " ", CurrentItemLink,
                                        " for ", GetCoinTextureString(itemSellPrice * itemCount))
                                    UseContainerItem(b, s)
                                else
                                    DataToColor:Print("Item is not gray or common, not selling it: ", items[i])
                                end
                            end
                        end
                    end
                end
            end

            if TotalPrice ~= 0 then
                DataToColor:Print("Total Price for all items: ", GetCoinTextureString(TotalPrice))
            else
                DataToColor:Print("No grey items were sold.")
            end

        else
            DataToColor:Print("Merchant is not open to sell to, please approach and open.")
        end
    else
        DataToColor:Print("Merchant is not targetted.")
    end
end