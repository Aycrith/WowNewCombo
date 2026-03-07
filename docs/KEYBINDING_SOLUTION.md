# Keybinding Initialization - Root Cause & Solution

## Problem Summary
Keybindings were showing as not initialized (`totalBindings: 0`) despite the addon and frames working correctly.

## Root Cause
The keybinding queue in the WoW addon has a **5-tick lifetime**. When the C# server restarts, it needs to trigger the addon to push bindings again. The bindings don't persist automatically.

## Solution

### Automatic Fix (Recommended)
Use the diagnostic API to trigger bindings refresh:

```bash
curl -X POST http://localhost:5000/api/Diagnostics/fix/bindings
```

This executes `//dcbindings` in WoW, which pushes all keybindings to the queue.

### Manual Fix (In-Game)
Type in WoW chat:
```
/dcbindings
```

### Verification
Check if bindings loaded successfully:

```bash
curl http://localhost:5000/api/Diagnostics/keybindings/stats
```

Expected output when working:
```json
{
  "totalReads": 6559,
  "nonZeroReads": 831,
  "consecutiveZeros": 2179,
  "isInitialized": true,
  "bindingCount": 52,
  "percentageNonZero": 12.67
}
```

Key indicators:
- ✅ `isInitialized: true`
- ✅ `bindingCount > 0`
- ✅ `nonZeroReads > 0`

## Why This Happens

### Addon Side (Lua)
File: `Addons/DataToColor/SetupDefaultBindings.lua:145`

```lua
DataToColor.bindingQueue = DataToColor.TimedQueue:new(5, nil)
```

The queue has a **5-tick lifetime** (~0.3 seconds at 60fps). Values expire automatically.

### C# Side
File: `Core/Addon/KeyBindingsReader.cs`

The reader polls slot 106 continuously, but if the server restarts or the addon reloads, the queue is empty until manually triggered.

## Diagnostic Enhancements Added

### 1. Statistics Tracking
`KeyBindingsReader` now tracks:
- `totalReads` - How many times slot 106 was read
- `nonZeroReads` - How many non-zero values detected  
- `consecutiveZeros` - Current streak of zero reads

### 2. New API Endpoints

#### Keybinding Stats
```
GET /api/Diagnostics/keybindings/stats
```

Returns read statistics and initialization status.

#### Slot Monitor
```
GET /api/Diagnostics/monitor/slot106?duration=10
```

Monitors slot 106 in real-time for specified duration (1-30 seconds).

#### Single Slot Reader
```
GET /api/Diagnostics/slot/106
```

Reads current value of any frame slot (0-323).

#### Slot Range Reader
```
GET /api/Diagnostics/slots/range?start=0&end=10
```

Reads multiple slots at once (max 50).

## Best Practices

### Server Startup Sequence
1. Start WoW and log in to character
2. Start C# server (`dotnet run --project BlazorServer`)
3. Wait for server to initialize (~5-10 seconds)
4. Trigger bindings: `curl -X POST http://localhost:5000/api/Diagnostics/fix/bindings`
5. Verify: `curl http://localhost:5000/api/Diagnostics/keybindings/stats`
6. Load profile and start bot

### When Bindings Don't Load
1. **Check WoW is running**: `tasklist | findstr WowClassic`
2. **Check addon loaded**: Look for DataToColor frames in-game
3. **Check screen capture working**: `curl http://localhost:5000/api/Test/frames`
4. **Trigger bindings manually**: `/dcbindings` in WoW chat
5. **Check logs**: `tail -50 BlazorServer/out20260203.log | grep KeyBindings`

### Common Issues

#### "totalBindings: 0" after server restart
**Cause**: Queue expired or server restarted  
**Fix**: Run `/dcbindings` in WoW or use fix endpoint

#### "consecutiveZeros" keeps increasing
**Cause**: Addon not pushing data or WoW not running  
**Fix**: Verify WoW running, addon loaded, then trigger `/dcbindings`

#### "nonZeroReads > 0" but "isInitialized: false"
**Cause**: Partial data received but not enough to initialize  
**Fix**: Trigger `/dcbindings` again

## Technical Details

### Data Flow
```
WoW Addon (DataToColor.lua)
  ↓ User types /dcbindings or addon event fires
SetupDefaultBindings.lua
  ↓ Encodes each binding as integer
bindingQueue:push(encodedValue)
  ↓ Renders to pixel slot 106 (5-tick lifetime)
Pixel(int, bindingQueue:shift(globalTick) or 0, 106)
  ↓ DXGI screen capture (~60Hz)
WowScreenDXGI.UpdateData()
  ↓ C# reads frame slot (~50-60Hz)
IAddonDataProvider.GetInt(106)
  ↓ Decodes binding
KeyBindingsReader.Update(reader)
  ↓ Stores in dictionary
keyActions["MULTIACTIONBAR1BUTTON9"] = new KeyAction(...)
```

### Encoding Format
Slot 106 contains an encoded integer with:
- Bits 0-7: Modifier flags (Shift, Ctrl, Alt)
- Bits 8-31: Binding ID hash

See `SetupDefaultBindings.lua` for encoding details.

## Files Modified

### Core Changes
- `Core/Addon/KeyBindingsReader.cs` - Added statistics tracking
- `Core/Addon/AddonReader.cs` - Added `DataProvider` property
- `CoreTests/NpcNameFinder/MockWoWScreen.cs` - Added `WaitForUpdate` method

### Frontend Changes
- `Frontend/Controllers/DiagnosticsController.cs` - Added 5 diagnostic endpoints

### Documentation
- `DIAGNOSTICS_GUIDE.md` - Complete troubleshooting guide
- `KEYBINDING_SOLUTION.md` - This file

## Success Metrics

Current working state (2026-02-03 22:27):
- ✅ Keybindings initialized: `true`
- ✅ Total bindings: 52
- ✅ Mismatches: 0
- ✅ Non-zero reads: 831
- ✅ Screen latency: 2.58ms

## Next Steps

The keybinding system is now fully functional. Remaining work:
- Action bar texture initialization (separate issue)
- Profile spell validation for low-level characters
- Bot goal testing

## Quick Reference

### Check Status
```bash
curl http://localhost:5000/api/Diagnostics/summary
```

### Fix Bindings
```bash
curl -X POST http://localhost:5000/api/Diagnostics/fix/bindings
```

### Monitor Live
```bash
curl "http://localhost:5000/api/Diagnostics/monitor/slot106?duration=10"
```

### Full Diagnostics
```bash
curl http://localhost:5000/api/Diagnostics/keybindings
curl http://localhost:5000/api/Diagnostics/actionbar  
curl http://localhost:5000/api/Diagnostics/bot/state
```
