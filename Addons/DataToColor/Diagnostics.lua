local Load = select(2, ...)
local DataToColor = unpack(Load)

local format = format

local CreateFrame = CreateFrame
local GetCVarBool = GetCVarBool
local ResetCPUUsage = ResetCPUUsage
local debugprofilestop = debugprofilestop
local UpdateAddOnCPUUsage = UpdateAddOnCPUUsage
local GetAddOnCPUUsage = GetAddOnCPUUsage
local GetAddOnInfo = GetAddOnInfo or C_AddOns.GetAddOnInfo
local GetAddOnMetadata = GetAddOnMetadata or C_AddOns.GetAddOnMetadata
local GetBindingKey = GetBindingKey
local GetCVar = GetCVar

-- ============================================================================
-- CPU Impact Measurement (existing functionality)
-- ============================================================================

local num_frames = 0
local function OnUpdate()
	num_frames = num_frames + 1
end

local fcpu = CreateFrame('Frame')
fcpu:Hide()
fcpu:SetScript('OnUpdate', OnUpdate)

local toggleMode, debugTimer, cpuImpactMessage = false, 0, 'Consumed %sms per frame. Each frame took %sms to render.'
function DataToColor:GetCPUImpact()
	if not GetCVarBool('scriptProfile') then
		DataToColor:Print('For `/dccpu` to work, you need to enable script profiling via: `/console scriptProfile 1` then reload. Disable after testing by setting it back to 0.')
		return
	end

	if not toggleMode then
		ResetCPUUsage()
		toggleMode, num_frames, debugTimer = true, 0, debugprofilestop()
		DataToColor:Print('CPU Impact being calculated, type /dccpu to get results when you are ready.')
		fcpu:Show()
	else
		fcpu:Hide()
		local ms_passed = debugprofilestop() - debugTimer
		UpdateAddOnCPUUsage()

		local per, passed = ((num_frames == 0 and 0) or (GetAddOnCPUUsage('DataToColor') / num_frames)),
			((num_frames == 0 and 0) or (ms_passed / num_frames))
		DataToColor:Print(format(cpuImpactMessage, per and per > 0 and format('%.3f', per) or 0,
			passed and passed > 0 and format('%.3f', passed) or 0))
		toggleMode = false
	end
end

-- ============================================================================
-- System Diagnostics (/dccheck command)
-- ============================================================================

-- Check result constants
local CHECK_OK = "|cff00ff00OK|r"
local CHECK_WARN = "|cffffff00WARN|r"
local CHECK_FAIL = "|cffff0000FAIL|r"

-- Check if BindPadMacro button exists (provided by SecureButtons.xml)
local function CheckBindPadMacro()
    if BindPadMacro then
        return CHECK_OK, "BindPadMacro button exists (from SecureButtons.xml)"
    else
        return CHECK_FAIL, "BindPadMacro NOT FOUND - SecureButtons.xml may not be loaded"
    end
end

-- Check if BindPadKey button exists
local function CheckBindPadKey()
    if BindPadKey then
        return CHECK_OK, "BindPadKey button exists"
    else
        return CHECK_WARN, "BindPadKey not found (optional)"
    end
end

-- Check if custom bindings are set up
local function CheckCustomBindings()
    local bindings = {
        { key = "SHIFT-PAGEUP", action = "CLICK BindPadMacro:config", name = "Config toggle" },
        { key = "ALT-DELETE", action = "CLICK BindPadMacro:stopattack", name = "Stop attack" },
        { key = "ALT-INSERT", action = "CLICK BindPadMacro:cleartarget", name = "Clear target" },
        { key = "SHIFT-PAGEDOWN", action = "CLICK BindPadMacro:flush", name = "Flush state" },
    }
    
    local allOk = true
    local results = {}
    
    for _, binding in ipairs(bindings) do
        local key1, key2 = GetBindingKey(binding.action)
        if key1 or key2 then
            table.insert(results, "  " .. CHECK_OK .. " " .. binding.name .. " (" .. (key1 or key2) .. ")")
        else
            table.insert(results, "  " .. CHECK_FAIL .. " " .. binding.name .. " NOT BOUND")
            allOk = false
        end
    end
    
    return allOk and CHECK_OK or CHECK_FAIL, results
end

-- Check graphics settings for pixel reading
local function CheckGraphicsSettings()
    local issues = {}
    
    local aa = DataToColor.SafeGetCVar("ffxAntiAliasingMode", "0")
    if aa ~= "0" then
        table.insert(issues, "  " .. CHECK_WARN .. " Anti-aliasing is ON (should be 0, is " .. aa .. ")")
    else
        table.insert(issues, "  " .. CHECK_OK .. " Anti-aliasing disabled")
    end
    
    local scale = DataToColor.SafeGetCVar("renderScale", "1")
    if scale ~= "1" then
        table.insert(issues, "  " .. CHECK_WARN .. " Render scale is " .. scale .. " (should be 1)")
    else
        table.insert(issues, "  " .. CHECK_OK .. " Render scale 100%")
    end
    
    local glow = DataToColor.SafeGetCVar("ffxGlow", "0")
    if glow ~= "0" then
        table.insert(issues, "  " .. CHECK_WARN .. " Glow effect is ON")
    else
        table.insert(issues, "  " .. CHECK_OK .. " Glow effect disabled")
    end
    
    return #issues > 0 and issues or nil, issues
end

-- Check addon versions
local function CheckAddonVersions()
    local results = {}
    
    local dcVersion = GetAddOnMetadata("DataToColor", "Version") or "unknown"
    table.insert(results, "  DataToColor: v" .. dcVersion)
    
    -- Check if legacy BindPad addon is loaded (optional, for backwards compat)
    local name, _, _, loadable, reason = GetAddOnInfo("BindPad")
    if name and loadable then
        local bpVersion = GetAddOnMetadata("BindPad", "Version") or "unknown"
        table.insert(results, "  BindPad (legacy): v" .. bpVersion .. " (can be removed)")
    end
    
    return results
end

-- Main diagnostic function
function DataToColor:RunDiagnostics()
    DataToColor:Print("=== DataToColor Diagnostics ===")
    
    -- 1. Check BindPadMacro (from SecureButtons.xml)
    local status, msg = CheckBindPadMacro()
    DataToColor:Print(status .. " " .. msg)
    
    -- 2. Check BindPadKey
    status, msg = CheckBindPadKey()
    DataToColor:Print(status .. " " .. msg)
    
    -- 3. Check custom bindings
    DataToColor:Print("Custom Bindings:")
    local bindStatus, bindResults = CheckCustomBindings()
    for _, line in ipairs(bindResults) do
        DataToColor:Print(line)
    end
    
    -- 4. Check graphics settings
    DataToColor:Print("Graphics Settings:")
    local _, gfxResults = CheckGraphicsSettings()
    for _, line in ipairs(gfxResults) do
        DataToColor:Print(line)
    end
    
    -- 5. Addon versions
    DataToColor:Print("Addon Versions:")
    local versionResults = CheckAddonVersions()
    for _, line in ipairs(versionResults) do
        DataToColor:Print(line)
    end
    
    DataToColor:Print("=== End Diagnostics ===")
    
    -- Summary
    if not BindPadMacro then
        DataToColor:Print(CHECK_FAIL .. " CRITICAL: BindPadMacro missing!")
        DataToColor:Print("  This button should come from SecureButtons.xml in DataToColor")
        DataToColor:Print("  Try: /reload or reinstall the addon")
    elseif bindStatus == CHECK_FAIL then
        DataToColor:Print(CHECK_WARN .. " Bindings not set. Run: /dcactions")
    else
        DataToColor:Print(CHECK_OK .. " All systems operational!")
    end
end

-- ============================================================================
-- Startup Diagnostics (runs automatically on login)
-- ============================================================================

function DataToColor:RunStartupDiagnostics()
    -- Quick silent check - only print if there's a problem
    local hasProblems = false
    local problems = {}
    
    -- Check BindPadMacro (from SecureButtons.xml)
    if not BindPadMacro then
        hasProblems = true
        table.insert(problems, "BindPadMacro button not found - SecureButtons.xml may not be loaded")
    end
    
    -- Check if bindings exist
    local configKey = GetBindingKey("CLICK BindPadMacro:config")
    if not configKey and BindPadMacro then
        -- BindPadMacro exists but bindings not set - we'll auto-fix this
        table.insert(problems, "Custom bindings not configured - will attempt auto-setup")
    end
    
    -- Report problems
    if hasProblems then
        DataToColor:Print(CHECK_WARN .. " Startup check found issues:")
        for _, problem in ipairs(problems) do
            DataToColor:Print("  - " .. problem)
        end
        DataToColor:Print("Run /dccheck for full diagnostics")
    end
    
    -- Return status for other code to use
    return not hasProblems
end

-- ============================================================================
-- Quick Status (for other code to query)
-- ============================================================================

function DataToColor:GetSystemStatus()
    return {
        bindPadMacroExists = BindPadMacro ~= nil,
        bindPadKeyExists = BindPadKey ~= nil,
        configBindingSet = GetBindingKey("CLICK BindPadMacro:config") ~= nil,
        stopAttackBindingSet = GetBindingKey("CLICK BindPadMacro:stopattack") ~= nil,
    }
end

-- ============================================================================
-- Register slash command
-- ============================================================================

function DataToColor:RegisterDiagnosticCommands()
    DataToColor:RegisterChatCommand('dccheck', 'RunDiagnostics')
end
