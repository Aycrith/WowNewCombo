local Load = select(2, ...)
local DataToColor = unpack(Load)

local GetBuildInfo = GetBuildInfo

local UnitIsUnit = UnitIsUnit

local UnitChannelInfo = UnitChannelInfo
local UnitCastingInfo = UnitCastingInfo

local WOW_PROJECT_ID = WOW_PROJECT_ID
local WOW_PROJECT_CLASSIC = WOW_PROJECT_CLASSIC
local WOW_PROJECT_BURNING_CRUSADE_CLASSIC = WOW_PROJECT_BURNING_CRUSADE_CLASSIC
local WOW_PROJECT_MAINLINE = WOW_PROJECT_MAINLINE

local LE_EXPANSION_LEVEL_CURRENT = LE_EXPANSION_LEVEL_CURRENT
local LE_EXPANSION_NORTHREND = LE_EXPANSION_NORTHREND
local LE_EXPANSION_BURNING_CRUSADE = LE_EXPANSION_BURNING_CRUSADE
local LE_EXPANSION_WRATH_OF_THE_LICH_KING = LE_EXPANSION_WRATH_OF_THE_LICH_KING

function DataToColor.IsClassic()
  return WOW_PROJECT_ID == WOW_PROJECT_CLASSIC
end

function DataToColor.IsClassic_BCC()
  return WOW_PROJECT_ID == WOW_PROJECT_BURNING_CRUSADE_CLASSIC
end

function DataToColor.IsRetail()
  return WOW_PROJECT_ID == WOW_PROJECT_MAINLINE
end

local LibClassicCasterino
if DataToColor.IsClassic() then
  LibClassicCasterino = _G.LibStub("LibClassicCasterino")
end

local TBC253 = DataToColor.IsClassic_BCC() and select(4, GetBuildInfo()) >= 20503
local Wrath340 = DataToColor.IsClassic_BCC() and select(4, GetBuildInfo()) >= 30400

if WOW_PROJECT_ID == WOW_PROJECT_MAINLINE then
	DataToColor.ClientVersion = 1
elseif WOW_PROJECT_ID == WOW_PROJECT_BURNING_CRUSADE_CLASSIC then
	if LE_EXPANSION_LEVEL_CURRENT == LE_EXPANSION_NORTHREND or LE_EXPANSION_LEVEL_CURRENT == LE_EXPANSION_WRATH_OF_THE_LICH_KING then
		DataToColor.ClientVersion = 4
	elseif LE_EXPANSION_LEVEL_CURRENT == LE_EXPANSION_BURNING_CRUSADE then
		DataToColor.ClientVersion = 3
	end
elseif WOW_PROJECT_ID == WOW_PROJECT_CLASSIC then
	DataToColor.ClientVersion = 2
end

if DataToColor.IsRetail() or TBC253 then
  DataToColor.UnitCastingInfo = UnitCastingInfo
elseif DataToColor.IsClassic_BCC() then
  DataToColor.UnitCastingInfo = function(unit)
    local name, text, texture, startTimeMS, endTimeMS, isTradeSkill, castID, spellId = UnitCastingInfo(unit)
    return name, text, texture, startTimeMS, endTimeMS, isTradeSkill, castID, nil, spellId
  end
else
  DataToColor.UnitCastingInfo = function(unit)
    if UnitIsUnit(unit, DataToColor.C.unitPlayer) then
      return UnitCastingInfo(DataToColor.C.unitPlayer)
    else
      return LibClassicCasterino:UnitCastingInfo(unit)
    end
  end
end

if DataToColor.IsRetail() or TBC253 then
  DataToColor.UnitChannelInfo = UnitChannelInfo
elseif DataToColor.IsClassic_BCC() then
  DataToColor.UnitChannelInfo = function(unit)
    local name, text, texture, startTimeMS, endTimeMS, isTradeSkill, spellId = UnitChannelInfo(unit)
    return name, text, texture, startTimeMS, endTimeMS, isTradeSkill, nil, spellId
  end
else
  DataToColor.UnitChannelInfo = function(unit)
    if UnitIsUnit(unit, DataToColor.C.unitPlayer) then
      return UnitChannelInfo(DataToColor.C.unitPlayer)
    else
      return LibClassicCasterino:UnitChannelInfo(unit)
    end
  end
end
