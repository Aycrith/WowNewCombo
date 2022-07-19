local Load = select(2, ...)
local DataToColor = unpack(Load)

local band = bit.band

local UIErrorsFrame = UIErrorsFrame
local GetGameMessageInfo = GetGameMessageInfo
local CombatLogGetCurrentEventInfo = CombatLogGetCurrentEventInfo
local GetSpellInfo = GetSpellInfo
local GetGossipOptions = GetGossipOptions
local HasAction = HasAction
local CanMerchantRepair = CanMerchantRepair
local GetRepairAllCost = GetRepairAllCost
local GetMoney = GetMoney
local RepairAllItems = RepairAllItems

local DeclineGroup = DeclineGroup
local AcceptGroup = AcceptGroup
local StaticPopup_Hide = StaticPopup_Hide

local UnitGUID = UnitGUID
local UnitIsDeadOrGhost = UnitIsDeadOrGhost
local UnitIsGhost = UnitIsGhost
local C_Map = C_Map
local C_DeathInfo = C_DeathInfo
local DEFAULT_CHAT_FRAME = DEFAULT_CHAT_FRAME
local RepopMe = RepopMe
local RetrieveCorpse = RetrieveCorpse
local GetCorpseRecoveryDelay = GetCorpseRecoveryDelay

local SPELL_FAILED_UNIT_NOT_INFRONT = SPELL_FAILED_UNIT_NOT_INFRONT
local ERR_BADATTACKFACING = ERR_BADATTACKFACING
local SPELL_FAILED_MOVING = SPELL_FAILED_MOVING
local SPELL_FAILED_STUNNED = SPELL_FAILED_STUNNED

local CAST_START = 999998
local CAST_SUCCESS = 999999

local MERCHANT_SHOW_V = 9999999
local MERCHANT_CLOSED_V = 9999998

local GOSSIP_START = 69
local GOSSIP_END = 9999994

local ignoreErrorList = {
    "ERR_ABILITY_COOLDOWN",
    "ERR_OUT_OF_RAGE",
    "ERR_NO_ATTACK_TARGET",
    "ERR_OUT_OF_MANA",
    "ERR_SPELL_FAILED_SHAPESHIFT_FORM_S",
    "ERR_GENERIC_NO_TARGET",
    "ERR_ATTACK_PREVENTED_BY_MECHANIC_S",
    "ERR_ATTACK_STUNNED",
    "ERR_NOEMOTEWHILERUNNING",
}

local errorList = {
    "ERR_BADATTACKFACING", --1 "You are facing the wrong way!"
    "ERR_SPELL_FAILED_S", --2 -- like a printf
    "ERR_SPELL_OUT_OF_RANGE", --3 "Out of range."
    "ERR_BADATTACKPOS", --4 "You are too far away!"
    "ERR_AUTOFOLLOW_TOO_FAR", --5 "Target is too far away."
    "SPELL_FAILED_MOVING", --6 "Can't do that while moving"
    "ERR_SPELL_COOLDOWN", --7 "Spell is not ready yet."
    "ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS", --8 "Another action is in progress"
    "ERR_SPELL_FAILED_STUNNED", -- 9 "Can't do that while stunned"
    "SPELL_FAILED_INTERRUPTED", -- 10 "Interrupted"
    "SPELL_FAILED_ITEM_NOT_READY" -- 11 "Item is not ready yet"
}

function DataToColor:RegisterEvents()
    DataToColor:RegisterEvent("UI_ERROR_MESSAGE", 'OnUIErrorMessage')
    DataToColor:RegisterEvent("COMBAT_LOG_EVENT_UNFILTERED", 'UnfilteredCombatEvent')
    DataToColor:RegisterEvent('LOOT_CLOSED', 'OnLootClosed')
    DataToColor:RegisterEvent('BAG_UPDATE', 'OnBagUpdate')
    DataToColor:RegisterEvent('BAG_CLOSED', 'OnBagUpdate')
    DataToColor:RegisterEvent('MERCHANT_SHOW', 'OnMerchantShow')
    DataToColor:RegisterEvent('MERCHANT_CLOSED', 'OnMerchantClosed')
    DataToColor:RegisterEvent('PLAYER_TARGET_CHANGED', 'OnPlayerTargetChanged')
    DataToColor:RegisterEvent('PLAYER_EQUIPMENT_CHANGED', 'OnPlayerEquipmentChanged')
    DataToColor:RegisterEvent('GOSSIP_SHOW', 'OnGossipShow')
    DataToColor:RegisterEvent('SPELLS_CHANGED', 'OnSpellsChanged')
    DataToColor:RegisterEvent('ACTIONBAR_SLOT_CHANGED', 'ActionbarSlotChanged')
    DataToColor:RegisterEvent('CORPSE_IN_RANGE', 'CorpseInRangeEvent')
    DataToColor:RegisterEvent('CORPSE_OUT_OF_RANGE', 'CorpseOutOfRangeEvent')
    DataToColor:RegisterEvent('CHAT_MSG_OPENING', 'ChatMessageOpeningEvent')
    DataToColor:RegisterEvent('UNIT_PET', 'OnPetChanged')

    DataToColor:RegisterEvent('ZONE_CHANGED', 'OnZoneChanged')
    DataToColor:RegisterEvent('ZONE_CHANGED_INDOORS', 'OnZoneChanged')
    DataToColor:RegisterEvent('ZONE_CHANGED_NEW_AREA', 'OnZoneChanged')
end

function DataToColor:OnUIErrorMessage(event, messageType, message)
    local code, ignored, foundMessage, message = DataToColor:GetErrorCode(messageType, message)

    if ignored then
        UIErrorsFrame:AddMessage(message, 0.7, 0.7, 0.7) -- show as grey messasge
    elseif foundMessage and code ~= 0 then
        DataToColor.uiErrorMessage = code
        UIErrorsFrame:AddMessage(message, 0, 1, 0) -- show as green messasge
    else
        UIErrorsFrame:AddMessage(message, 0, 0, 1) -- show as blue message (unknown message)
    end
end

function DataToColor:GetErrorCode(messageType, message)

    local errorName
    local foundMessage = false
    local ignored = false
    local code = 0

    if messageType ~= nil then
        errorName = GetGameMessageInfo(messageType)
    end

    for i = 1, #ignoreErrorList, 1 do
        if ignoreErrorList[i] == errorName then
            foundMessage = true
            ignored = true
        end
    end

    if not ignored and not foundMessage then
        for i = 1, #errorList, 1 do
            if errorList[i] == errorName or
                (_G[errorList[i]] ~= nil and string.find(_G[errorList[i]], message)) then
                code = i
                foundMessage = true
            end
        end
    end

    -- ERR_SPELL_FAILED_S
    -- find by message ex combatlog
    if not ignored and (not foundMessage or errorName == errorList[2]) then
        if string.find(message, SPELL_FAILED_UNIT_NOT_INFRONT) then
            code = 1
            foundMessage = true
            message = message .. " (" .. ERR_BADATTACKFACING .. ")"
        elseif string.find(message, SPELL_FAILED_MOVING) then
            foundMessage = true
            code = 6
        elseif string.find(message, SPELL_FAILED_STUNNED) then
            foundMessage = true
            code = 9
        end
    end

    return code, ignored, foundMessage, message
end

local watchedSpells = {
    [DataToColor.C.Spell.AutoShotId] = function()
        --DataToColor:Print("Auto Shot detected")
        DataToColor.lastAutoShot = DataToColor.globalTime
    end
}

local swing_reset_spells = {
    --[[ Maul ]]
    [132136] = true,
    --[[ Raptor Strike ]]
    [132223] = true,
    --[[ Cleave ]]
    [132338] = true,
    --[[ Heroic Strike ]]
    [132282] = true,
    --[[ Slam ]]
    [132340] = true
}

local miss_type = {
    ["ABSORB"] = 0,
    ["BLOCK"] = 1,
    ["DEFLECT"] = 2,
    ["DODGE"] = 3,
    ["EVADE"] = 4,
    ["IMMUNE"] = 5,
    ["MISS"] = 6,
    ["PARRY"] = 7,
    ["REFLECT"] = 8,
    ["RESIST"] = 9,
}

function DataToColor:UnfilteredCombatEvent()
    DataToColor:OnCombatEvent(CombatLogGetCurrentEventInfo())
end

local COMBATLOG_OBJECT_TYPE_NPC = COMBATLOG_OBJECT_TYPE_NPC
local COMBATLOG_OBJECT_TYPE_PLAYER_OR_PET = COMBATLOG_OBJECT_TYPE_PLAYER + COMBATLOG_OBJECT_TYPE_PET


local playerDamageTakenEvents = {
    SWING_DAMAGE = true,
    SPELL_DAMAGE = true
}

local playerSpellCastSuccess = {
    SPELL_CAST_SUCCESS = true
}

local playerSpellCastStarted = {
    SPELL_CAST_START = true
}

local playerSpellCastFinished = {
    SPELL_CAST_SUCCESS = true,
    SPELL_CAST_FAILED = true
}

local playerSpellFailed = {
    SPELL_CAST_FAILED = true
}

local playerDamageDone = {
    SWING_DAMAGE = true,
    RANGE_DAMAGE = true,
    SPELL_DAMAGE = true
}

local playerDamageMiss = {
    SWING_MISSED = true,
    RANGE_MISSED = true,
    SPELL_MISSED = true
}

local playerMeleeSwing = {
    SWING_DAMAGE = true,
    SWING_MISSED = true
}

local unitDied = {
    UNIT_DIED = true
}

function DataToColor:OnCombatEvent(...)
    local _, subEvent, _, sourceGUID, _, _, _, destGUID, _, destFlags, _, spellId, _, _ = ...
    --print(...)

    if playerDamageTakenEvents[subEvent] and
        band(destFlags, COMBATLOG_OBJECT_TYPE_PLAYER_OR_PET) and
        (destGUID == DataToColor.playerGUID or destGUID == DataToColor.petGUID) then
        DataToColor.CombatDamageTakenQueue:push(DataToColor:getGuidFromUUID(sourceGUID))
        --DataToColor:Print("Damage Taken ", sourceGUID)
    end

    if sourceGUID == DataToColor.playerGUID then
        if playerSpellCastSuccess[subEvent] then
            if watchedSpells[spellId] then watchedSpells[spellId]() end

            local _, _, icon = GetSpellInfo(spellId)
            if swing_reset_spells[icon] then
                --DataToColor:Print("Special Melee Swing detected ", spellId)
                DataToColor.lastMainHandMeleeSwing = DataToColor.globalTime
            end
        end

        if playerSpellCastStarted[subEvent] then
            DataToColor.lastCastEvent = CAST_START
            DataToColor.lastCastSpellId = spellId
            --DataToColor:Print(subEvent, " ", spellId)
        end

        if playerSpellCastFinished[subEvent] then
            DataToColor.lastCastSpellId = spellId

            if playerSpellFailed[subEvent] then
                --local lastCastEvent = DataToColor.lastCastEvent
                local failedType = select(15, ...)
                DataToColor.lastCastEvent = DataToColor:GetErrorCode(nil, failedType)
                --DataToColor:Print(subEvent, " ", lastCastEvent, " -> ", DataToColor.lastCastEvent, " ", failedType, " ", spellId)
            else
                DataToColor.lastCastEvent = CAST_SUCCESS
                --DataToColor:Print(subEvent, " ", spellId)

                local hasGCD = true

                local _, gcdMS = GetSpellBaseCooldown(spellId)
                if (gcdMS == 0) then
                    hasGCD = false
                end

                local _, _, _, castTime = GetSpellInfo(spellId)
                if castTime > 0 then
                    hasGCD = false
                end

                if spellId == DataToColor.C.Spell.ShootId then
                    hasGCD = true
                end

                if hasGCD then
                    -- Rogues and Druid Cat have 1s gcd.
                    if DataToColor.C.CHARACTER_CLASS_ID == 4 or
                    (DataToColor.C.CHARACTER_CLASS_ID == 11 and DataToColor:shapeshiftForm() == 3) then
                        castTime = 1000
                    elseif spellId == DataToColor.C.Spell.ShootId then
                        castTime = floor(UnitRangedDamage(DataToColor.C.unitPlayer) * 1000)
                    else
                        castTime = 1500
                    end

                    DataToColor.gcdExpirationTime = GetTime() + (castTime / 1000)
                    --DataToColor:Print(subEvent, " ", spellId, " ", castTime)
                else
                    --DataToColor:Print(subEvent, " ", spellId)
                end
            end
        end

        -- matches SWING_ RANGE_ SPELL_ but not SPELL_PERIODIC
        if playerDamageDone[subEvent] or playerDamageMiss[subEvent] then
            DataToColor.CombatDamageDoneQueue:push(DataToColor:getGuidFromUUID(destGUID))
            --DataToColor:Print(subEvent, " ", destGUID)

            if playerDamageMiss[subEvent] then
                local missType = select(-2, ...)
                if type(missType) == "boolean" then -- some spells has 3 args like Charge Stun
                    missType = select(-3, ...)
                end
                DataToColor.CombatMissTypeQueue:push(miss_type[missType])
                --DataToColor:Print(subEvent, " ", miss_type[missType])
            end
        end

        if playerMeleeSwing[subEvent] then
            local _, _, _, _, _, _, _, _, _, isOffHand = select(12, ...)
            if not isOffHand then
                --DataToColor:Print("Normal Main Hand Melee Swing detected")
                DataToColor.lastMainHandMeleeSwing = DataToColor.globalTime
            end
        end
    end

    if unitDied[subEvent] then
        if band(destFlags, COMBATLOG_OBJECT_TYPE_NPC) > 0 then
            DataToColor.CombatCreatureDiedQueue:push(DataToColor:getGuidFromUUID(destGUID))
            --DataToColor:Print(subEvent, " ", destGUID, " ", DataToColor:getGuidFromUUID(destGUID))
        else
            --DataToColor:Print(subEvent, " ignored ", destGUID)
        end
    end
end

function DataToColor:OnLootClosed(event)
    DataToColor.lastLoot = DataToColor.globalTime
    --DataToColor:Print("OnLootClosed:"..DataToColor.lastLoot)
end

function DataToColor:OnBagUpdate(event, containerID)
    if containerID >= 0 and containerID <= 4 then
        DataToColor.bagQueue:push(containerID)
        DataToColor:InitInventoryQueue(containerID)

        if containerID >= 1 then
            DataToColor.equipmentQueue:push(19 + containerID) -- from tabard
        end
    end
    --DataToColor:Print("OnBagUpdate "..containerID)
end

function DataToColor:OnMerchantShow(event)
    DataToColor.gossipQueue:push(MERCHANT_SHOW_V)
end

function DataToColor:OnMerchantClosed(event)
    DataToColor.gossipQueue:push(MERCHANT_CLOSED_V)
end

function DataToColor:OnPlayerTargetChanged(event)
    DataToColor.targetChanged = true
end

function DataToColor:OnPlayerEquipmentChanged(event, equipmentSlot, hasCurrent)
    DataToColor.equipmentQueue:push(equipmentSlot)
    --local c = hasCurrent and 1 or 0
    --DataToColor:Print("OnPlayerEquipmentChanged "..equipmentSlot.." -> "..c)
end

function DataToColor:OnGossipShow(event)
    local options = GetGossipOptions()
    if not options then
        return
    end

    DataToColor.gossipQueue:push(GOSSIP_START)

    -- returns variable string - format of one entry
    -- [1] localized name
    -- [2] gossip_type
    local GossipOptions = { GetGossipOptions() }
    local count = #GossipOptions / 2
    for k, v in pairs(GossipOptions) do
        if k % 2 == 0 then
            DataToColor.gossipQueue:push(10000 * count + 100 * (k / 2) + DataToColor.C.Gossip[v])
        end
    end
    DataToColor.gossipQueue:push(GOSSIP_END)
end

function DataToColor:OnSpellsChanged(event)
    DataToColor:InitTalentQueue()
    DataToColor:InitSpellBookQueue()
    DataToColor:InitActionBarCostQueue()
end

function DataToColor:ActionbarSlotChanged(event, slot)
    if slot and HasAction(slot) then
        DataToColor.actionBarCostQueue:push(slot)
    end
end

function DataToColor:CorpseInRangeEvent(event)
    DataToColor.corpseInRange = 1
end

function DataToColor:CorpseOutOfRangeEvent(event)
    DataToColor.corpseInRange = 0
end

function DataToColor:ChatMessageOpeningEvent(event, ...)
    local _, playerName, _, _, playerName2 = ...
    local function isempty(s)
        return s == nil or s == ''
    end

    if isempty(playerName) and isempty(playerName2) then
        DataToColor.lastCastEvent = CAST_SUCCESS
        DataToColor.uiErrorMessage = CAST_SUCCESS
    end
end

function DataToColor:OnPetChanged(event, unit)
    if unit == DataToColor.C.unitPlayer then
        DataToColor.petGUID = UnitGUID(DataToColor.C.unitPet)
    end
end

function DataToColor:OnZoneChanged(event)
    DataToColor.map = C_Map.GetBestMapForUnit(DataToColor.C.unitPlayer)
end

local CORPSE_RETRIEVAL_DISTANCE = 40

-----------------------------------------------------------------------------
-- Begin Event Section -- -- Begin Event Section -- -- Begin Event Section --
-- Begin Event Section -- -- Begin Event Section -- -- Begin Event Section --
-- Begin Event Section -- -- Begin Event Section -- -- Begin Event Section --
-- Begin Event Section -- -- Begin Event Section -- -- Begin Event Section --
-----------------------------------------------------------------------------
function DataToColor:HandlePlayerInteractionEvents()
    -- Handles group accept/decline
    if DataToColor.DATA_CONFIG.ACCEPT_PARTY_REQUESTS or DataToColor.DATA_CONFIG.DECLINE_PARTY_REQUESTS then
        DataToColor:HandlePartyInvite()
    end
    -- Handles item repairs when talking to item repair NPC
    if DataToColor.DATA_CONFIG.AUTO_REPAIR_ITEMS then
        DataToColor:RepairItems()
    end
    -- Resurrect player
    if DataToColor.DATA_CONFIG.AUTO_RESURRECT then
        DataToColor:ResurrectPlayer()
    end
end

-- Declines/Accepts Party Invites.
function DataToColor:HandlePartyInvite()
    if DataToColor.globalTime % 50 == 1 then
        -- Declines party invite if configured to decline
        if DataToColor.DATA_CONFIG.DECLINE_PARTY_REQUESTS then
            DeclineGroup()
        else if DataToColor.DATA_CONFIG.ACCEPT_PARTY_REQUESTS then
                AcceptGroup()
            end
        end
        -- Hides the party invite pop-up regardless of whether we accept it or not
        StaticPopup_Hide("PARTY_INVITE")
    end
end

-- Repairs items if they are broken
function DataToColor:RepairItems()
    if DataToColor.globalTime % 50 == 1 then
        local cost = GetRepairAllCost()
        if CanMerchantRepair() and cost > 0 and GetMoney() >= cost then
            RepairAllItems()
        end
    end
end

--the x and y is 0 if not dead
--runs the RetrieveCorpse() function to ressurrect
function DataToColor:ResurrectPlayer()
    if DataToColor.globalTime % 700 == 1 then
        if UnitIsDeadOrGhost(DataToColor.C.unitPlayer) then

            -- Accept Release Spirit immediately after dying
            if not UnitIsGhost(DataToColor.C.unitPlayer) and UnitIsGhost(DataToColor.C.unitPlayer) ~= nil then
                RepopMe()
            end
            if UnitIsGhost(DataToColor.C.unitPlayer) then
                local cX, cY = DataToColor:GetCorpsePosition()
                local x, y = DataToColor:GetPosition()
                -- Waits so that we are in range of specified retrieval distance, and ensures there is no delay timer before attemtping to resurrect
                if cX ~= 0 and cY ~= 0 and
                    math.abs(cX - x) < CORPSE_RETRIEVAL_DISTANCE / 1000 and
                    math.abs(cY - y) < CORPSE_RETRIEVAL_DISTANCE / 1000 and
                    GetCorpseRecoveryDelay() == 0 then
                    DEFAULT_CHAT_FRAME:AddMessage('Attempting to retrieve corpse')
                    -- Accept Retrieve Corpsse when near enough
                    RetrieveCorpse()
                end
            end
        end
    end
end
