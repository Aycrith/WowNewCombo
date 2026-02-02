# WowClassicGrindBot Setup - Critical Handoff Summary

**Date**: February 2, 2026  
**Status**: BLOCKED - Frame Configuration Cannot Complete  
**Priority**: HIGH - Core functionality not working

---

## Executive Summary

The WowClassicGrindBot installation is nearly complete but **cannot finish the frame configuration process**. The core blocker is that the `BindPadMinimal` addon (a custom replacement for the broken BindPad addon) is failing to load in WoW, which prevents the `/dcactions` command from working, which in turn prevents the Auto configuration from functioning.

---

## System Configuration

### Paths
| Component | Path |
|-----------|------|
| Bot Installation | `C:\WowClassicGrindBot` |
| WoW Installation | `C:\Program Files (x86)\World of Warcraft\_anniversary_` |
| BlazorServer Release | `C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0` |
| Navigation Server | `C:\WowClassicGrindBot\Navigation` |
| MMAPS Files | `C:\WowClassicGrindBot\Navigation\mmaps` (2054 files) |
| MPQ Files | `C:\WowClassicGrindBot\Json\MPQ` |
| Addons | `C:\Program Files (x86)\World of Warcraft\_anniversary_\Interface\AddOns` |

### Versions
| Component | Version |
|-----------|---------|
| WoW Client | 2.5.5.65534 (TBC Classic Anniversary) |
| .NET Runtime | 10.0.102 |
| DataToColor Addon | 1.9.2 |
| Interface Version | 20505 (TBC Classic) |

### WoW Process
- **PID**: 30100
- **Resolution**: 1920x1080 (changed from 2560x1440 during testing)
- **Mode**: Fullscreen

---

## Current Problem Chain

```
┌─────────────────────────────────────────────────────────────────┐
│ PROBLEM 1: BindPadMinimal addon fails to load in WoW           │
│   ↓                                                             │
│ PROBLEM 2: /dcactions command shows nothing (no output)         │
│   ↓                                                             │
│ PROBLEM 3: SHIFT-PAGEUP keybinding not created                  │
│   ↓                                                             │
│ PROBLEM 4: Auto config button can't toggle config mode          │
│   ↓                                                             │
│ PROBLEM 5: frame_config.json never gets created                 │
│   ↓                                                             │
│ RESULT: Bot cannot read addon data, completely non-functional   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Detailed Problem Analysis

### CRITICAL BLOCKER: BindPadMinimal Addon Not Loading

**Current Error Message** (displayed when WoW launches):
```
Message: Interface/AddOns/BindPadMinimal/BindPadMinimal.xml:1 
Interface/AddOns/BindPadMinimal/BindPadMinimal.xml(5): error: not well-formed (invalid token)
Time: Mon Feb  2 14:20:36 2026
Count: 2
```

**Root Cause**: The XML file has encoding or formatting issues that WoW's XML parser rejects.

**Current File Contents**:

`BindPadMinimal.toc`:
```
## Interface: 20505
## Title: BindPadMinimal
## Version: 1.0
## Author: WowGrindBot
## Notes: Minimal BindPad replacement providing BindPadMacro button for DataToColor

BindPadMinimal.xml
```

`BindPadMinimal.xml` (PROBLEMATIC):
```xml
<Ui xmlns="http://www.blizzard.com/wow/ui/">
    <Button name="BindPadMacro" inherits="SecureActionButtonTemplate" />
    <Button name="BindPadKey" inherits="SecureActionButtonTemplate" />
</Ui>
```

**Why This Addon Exists**: The original BindPad addon bundled with the bot uses deprecated XML elements (`<Backdrop>`, `<AbsValue>`) that cause 48 LUA warnings in TBC Classic 2.5.5. It was renamed to `BindPad_DISABLED` and this minimal replacement was created.

**What BindPadMinimal Must Provide**: 
- A global frame named `BindPadMacro` that inherits `SecureActionButtonTemplate`
- This button is used by DataToColor's `/dcactions` command to set up secure action bindings

---

### PROBLEM 2: /dcactions Command Shows Nothing

**Expected Behavior**: When user types `/dcactions` in WoW chat, should see:
```
DataToColor: Bound: SHIFT-PAGEUP -> config
DataToColor: Bound: DELETE -> stopattack  
DataToColor: Bound: INSERT -> cleartarget
DataToColor: Bound: CTRL-PAGEDOWN -> flush
```

**Actual Behavior**: Nothing happens, no output at all.

**Cause**: The `SetupMacroButton()` function in `SetupDefaultBindings.lua` checks for `BindPadMacro`:
```lua
local function SetupMacroButton()
  local btn = BindPadMacro
  if not btn then
    DataToColor:Print("ERROR: BindPadMacro not found! Install BindPad addon.")
    return false
  end
  -- ... rest of setup
end
```

Since BindPadMinimal fails to load, `BindPadMacro` is nil, and the function returns false without printing anything (the error message should print but doesn't for unknown reasons).

---

### PROBLEM 3: SHIFT-PAGEUP Keybinding Not Created

**Purpose**: The bot's Auto config process sends SHIFT-PAGEUP to WoW to toggle the addon between Normal Mode and Config Mode.

**Code Location**: `Core\Configurator\FrameConfigurator.cs` line 414-418:
```csharp
private void ToggleInGameConfiguration()
{
    // Press SHIFT-PAGEUP to trigger CUSTOM_CONFIG (/dc)
    input.PressRandomWithModifier(ConsoleKey.PageUp, ModifierKey.Shift, 50);
}
```

**Dependency**: This keybinding is set up by `/dcactions` which requires BindPadMacro to exist.

---

### PROBLEM 4: Web UI Auto Config Button Does Nothing

**Symptoms**:
1. User clicks "Auto → Start" button on http://localhost:5000/FrameConfiguration
2. Nothing visible happens
3. Web UI shows "An unhandled error has occurred. This app may no longer respond until reloaded." at top
4. Log still shows: `DataFrames 0 - Texture: Size [ Width=1, Height=1 ]`

**Why It Fails**:
1. The button calls `frameConfigurator.StartAutoConfig()`
2. This sends SHIFT-PAGEUP to WoW
3. But the keybinding doesn't exist (see Problem 3)
4. So nothing happens in WoW
5. The bot waits for config mode pixels but they never appear
6. Eventually times out or errors

---

### PROBLEM 5: frame_config.json Never Created

**File Location**: `C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0\frame_config.json`  
**Current Status**: FILE DOES NOT EXIST

**How It Should Be Created**:
1. User enters Config Mode in WoW (`/dc` or SHIFT-PAGEUP)
2. DataToColor addon outputs metadata to pixel [0,0]
3. Bot reads the RGB values and extracts: cell size, spacing, row count, frame count
4. Bot scans screen to find all pixel frame locations
5. Bot saves this data to `frame_config.json`

**Config Mode Pixel Format**: At pixel [0,0], the addon outputs:
```
hash = CELL_SPACING * 10000000 + CELL_SIZE * 100000 + 1000 * FRAME_ROWS + NUMBER_OF_FRAMES
     = 1 * 10000000 + 1 * 100000 + 1000 * 1 + 111
     = 10101111
```
This is encoded as RGB values that the bot reads via DXGI screen capture.

---

## What Has Been Verified Working

| Component | Status | Notes |
|-----------|--------|-------|
| .NET 10.0 Runtime | ✅ OK | Version 10.0.102 |
| BlazorServer Build | ✅ OK | Release build compiles and runs |
| Navigation Server | ✅ OK | AmeisenNavigation starts on port 47111 |
| MMAPS Files | ✅ OK | 2054 files in Navigation\mmaps |
| MPQ Files | ✅ OK | common-2.MPQ (1.7GB), expansion.MPQ (1.8GB) |
| WoW Process Detection | ✅ OK | PID 30100 detected correctly |
| DXGI Screen Capture | ✅ OK | Captures screen at correct resolution |
| DataToColor Addon | ✅ OK | Version 1.9.2 loads without errors |
| `/dc` command | ✅ OK | Toggles between "Config mode" and "Normal mode" |
| `/dcbindings` command | ✅ OK | Reports "Bindings applied" |
| data_config.json | ✅ OK | Root points to Json folder |
| addon_config.json | ✅ OK | Configured for DataToColor |
| Navigation config.cfg | ✅ OK | mmaps path corrected |

---

## What Has Been Fixed During This Session

1. **Navigation mmaps path** - Changed from `C:\shady stuff\mmaps\` to `C:\WowClassicGrindBot\Navigation\mmaps\`

2. **Anti-Aliasing setting** - Added `SET ffxAntiAliasingMode "0"` to Config.wtf

3. **BindPad disabled** - Renamed broken `BindPad` to `BindPad_DISABLED` (had 48 LUA warnings from deprecated XML)

4. **BindPadMinimal created** - Attempted to create minimal replacement addon (STILL FAILING)

---

## WoW Graphics Settings (Config.wtf)

Current critical settings:
```
SET Contrast "50"          ✅ Correct
SET Brightness "50"        ✅ Correct  
SET Gamma "1"              ✅ Correct
SET RenderScale "1"        ✅ Correct (100%)
SET ffxGlow "0"            ✅ Correct (disabled)
SET ffxAntiAliasingMode "0" ✅ Added (disabled)
SET vsync "0"              ✅ Correct (disabled)
```

---

## Files That Need Attention

### 1. BindPadMinimal XML (CRITICAL)
**Path**: `C:\Program Files (x86)\World of Warcraft\_anniversary_\Interface\AddOns\BindPadMinimal\BindPadMinimal.xml`

**Required Valid Content**:
```xml
<Ui xmlns="http://www.blizzard.com/wow/ui/">
    <Button name="BindPadMacro" inherits="SecureActionButtonTemplate" />
    <Button name="BindPadKey" inherits="SecureActionButtonTemplate" />
</Ui>
```

**Issues to Check**:
- BOM (Byte Order Mark) at start of file
- Windows vs Unix line endings
- Any invisible/special characters
- File encoding (should be UTF-8 without BOM, or ASCII)

### 2. frame_config.json (MISSING)
**Path**: `C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0\frame_config.json`

Cannot be manually created - must be generated by the Auto config process.

---

## Recommended Resolution Steps

### Step 1: Fix BindPadMinimal XML
The XML file is syntactically correct but WoW rejects it. Try:

1. **Use a hex editor** to inspect for hidden characters
2. **Save as ANSI/ASCII** (not UTF-8, not UTF-8 with BOM)
3. **Use Unix line endings** (LF only, no CR)
4. **Test with WoW's built-in XML** - copy structure from a working addon's XML

### Step 2: Alternative - Use Original BindPad with Fixes
Instead of BindPadMinimal, fix the original BindPad:
1. Rename `BindPad_DISABLED` back to `BindPad`
2. Edit `BindPad.xml` to remove deprecated elements:
   - Replace `<Backdrop>` with `SetBackdrop()` in Lua
   - Replace `<AbsValue>` with direct values
3. The warnings are annoying but the addon might still work

### Step 3: Verify BindPadMacro Exists
After fixing the addon, in WoW:
```lua
/run print(BindPadMacro and "EXISTS" or "NIL")
```
Should print "EXISTS".

### Step 4: Run Setup Commands
```
/reload
/dcbindings
/dcactions   <-- Should now show binding messages
/dc          <-- Enter config mode
```

### Step 5: Complete Auto Config
1. Refresh web UI at http://localhost:5000/FrameConfiguration
2. Click Auto → Start
3. Watch WoW window - should see config mode toggle
4. frame_config.json should be created

---

## Technical Reference

### Key Source Files

| File | Purpose |
|------|---------|
| `Core\Configurator\FrameConfigurator.cs` | Auto config logic, sends SHIFT-PAGEUP |
| `Core\DataFrame\FrameConfig.cs` | Parses pixel [0,0] for metadata |
| `Frontend\Pages\FrameConfiguration.razor` | Web UI for frame config |
| `Interface\AddOns\DataToColor\DataToColor.lua` | Main addon, creates pixel frames |
| `Interface\AddOns\DataToColor\SetupDefaultBindings.lua` | `/dcactions` implementation |

### Config Mode Detection
The bot checks pixel [0,0] and calls `FrameConfig.GetMeta()`:
```csharp
public static DataFrameMeta GetMeta(Bgra32 color)
{
    int hash = color.R * 65536 + color.G * 256 + color.B;
    if (hash == 0)
        return DataFrameMeta.Empty;  // Not in config mode
    
    int spacing = hash / 10000000;
    int size = hash / 100000 % 100;
    int rows = hash / 1000 % 100;
    int count = hash % 1000;
    
    return new DataFrameMeta(hash, spacing, size, rows, count);
}
```

### Expected Config Mode Values
- CELL_SIZE = 1
- CELL_SPACING = 1
- FRAME_ROWS = 1
- NUMBER_OF_FRAMES = 111
- Hash = 10101111
- RGB ≈ (154, 42, 143)

---

## Screenshots Reference

The user provided screenshots showing:

1. **WoW UI Screenshot**: 
   - ElvUI was enabled (thin blue bar at top potentially blocking pixels)
   - Later disabled for testing
   - Character in Orgrimmar at Valley of Strength
   - Resolution was 2560x1440, later 1920x1080

2. **Web UI Screenshot**:
   - Shows "Frame configuration not found !"
   - Auto and Manual Start buttons visible
   - Log shows: `DataFrames 0 - Texture: Size [ Width=1, Height=1 ]`
   - Error banner: "An error has occurred. This app may no longer respond until reloaded."
   - Confirms addon is NOT in config mode

---

## Session Commands History

Key commands that were run:
- `/reload` - Multiple times to reload addons
- `/dc` - Works, shows "Config mode" / "Normal mode"
- `/dcbindings` - Works, shows "Bindings applied"
- `/dcactions` - FAILS, shows nothing

---

## Environment Notes

- **OS**: Windows
- **User has admin access**: Required for writing to Program Files
- **ElvUI**: Installed but was disabled during testing (not the cause)
- **Other addons**: Many installed (Details, Questie, etc.) but shouldn't interfere

---

## Next Agent Checklist

- [ ] Fix BindPadMinimal.xml encoding/format issue
- [ ] Verify `BindPadMacro` global exists in WoW after /reload
- [ ] Confirm `/dcactions` shows binding output
- [ ] Test SHIFT-PAGEUP keybind works in WoW
- [ ] Run Auto config and verify frame_config.json created
- [ ] Test bot can read player data after config

---

## Contact/Context

This is a personal project setup, not a production environment. The user has been working through setup issues for an extended session. All changes made are reversible.

---

*End of Handoff Summary*
