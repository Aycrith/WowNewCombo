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
