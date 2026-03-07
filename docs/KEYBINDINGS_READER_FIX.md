# Testing Infrastructure: KeyBindingsReader Fix and Workarounds

## Issue Summary
KeyBindingsReader may show `IsInitialized: false` and `TotalBindings: 0` even after running `/dcbindings` in WoW.

## Root Cause
The KeyBindingsReader reads bindings from addon frame slot 106 via a queue system:
1. Addon pushes bindings to `bindingQueue` (Lua side)
2. C# reads from slot 106 each frame
3. When queue is exhausted (returns 0) AND at least one binding was received, it marks as initialized

**Why it might fail:**
- Bindings were pushed before C# started reading (queue already exhausted)
- GlobalTime check gate (<=3 causes reader reset)
- Bot not actively reading frames when bindings were pushed

## Workarounds

### Quick Fix: Force Re-initialization
Run this command in WoW chat to force the addon to re-push bindings:
```
/dc flush
```

This will call `FlushState()` which triggers `InitBindingQueue()` to repopulate the binding queue.

### API Endpoint (Future Enhancement)
Could add an endpoint to trigger this via API:
```http
POST /api/Diagnostics/fix/reinit-bindings
```

## Diagnostic Logging Added

### KeyBindingsReader.cs
- **Debug logging**: Shows `encodedValue` read from slot 106 each frame
- **Info logging**: Confirms when bindings are initialized
- **Debug logging**: Shows when waiting for bindings (count = 0)

Set logging level to `Debug` in `appsettings.json` to see detailed flow:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Core.KeyBindingsReader": "Trace"
      }
    }
  }
}
```

### ActionBarPopulator.cs
- **Debug logging**: Shows when spells are skipped (no slot, duplicate slot)
- **Warning logging**: Shows when food/drink/trinkets can't be resolved
- **Debug logging**: Shows placement validation failures

## Investigation Results

### File: `Core\Addon\KeyBindingsReader.cs`
- **Binding slot**: 106 (pixel data from addon)
- **Dependencies**: 
  - AddonReader must be running
  - GlobalTime must be > 3
  - Frame must be visible and being read

### File: `Addons\DataToColor\SetupDefaultBindings.lua`
- **Queue lifetime**: 5 ticks (`tickLifetime = 5`)
- **Initial population**: On `PLAYER_ENTERING_WORLD` event
- **Change detection**: On `UPDATE_BINDINGS` event
- **Manual trigger**: `/dcbindings` command + `CheckBindingChanges()`

### Timing Issue
If C# isn't reading fast enough during startup:
- Queue items expire after 5 frames
- Bindings are lost before C# can read them
- Result: `IsInitialized` stays `false`

## Recommendations
1. **Always run `/dc flush` after `/dcbindings`** to ensure fresh queue push
2. **Check logs** for `"Waiting for bindings"` messages (indicates C# is reading but queue is empty)
3. **Verify bot is running** when setting bindings (AddonReader must be active)

## Related Files
- `Core\Addon\KeyBindingsReader.cs:36-106` - Update() method
- `Core\Addon\AddonReader.cs:76-80` - Reader orchestration
- `Addons\DataToColor\SetupDefaultBindings.lua:207-322` - Binding queue management
- `Addons\DataToColor\DataToColor.lua:943-944` - Pixel output (slot 106)
