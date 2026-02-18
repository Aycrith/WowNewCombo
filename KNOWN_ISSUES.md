# Known Issues and Troubleshooting Guide

## ✅ RESOLVED: Frame Detection Limited to 256 Frames (Fixed Feb 3, 2026)

**Previous Symptom**: "Only found 256/324 frames" in logs

**Cause**: Byte overflow bug in `Core/DataFrame/FrameConfig.cs` - frame indices ≥256 couldn't be detected

**Status**: ✅ **FIXED** - All 324 frames now detect correctly

**Details**: See `CRITICAL_BUG_FIX_FRAME_DETECTION.md` for complete technical breakdown

---

## Issue 1: BindPadMinimal XML Not Loading

**Symptom**: LUA error on WoW startup mentioning "not well-formed (invalid token)"

**Possible Causes**:
1. UTF-8 BOM (Byte Order Mark) at start of file
2. Windows line endings (CR+LF) instead of Unix (LF)
3. Special/invisible characters in the file
4. Encoding issues from text editors

**Solution**: Run `Fix-BindPadMinimal.ps1` which creates the file with ASCII encoding

**Alternative Solution**: Manually create the file using Notepad:
1. Open Notepad (not Notepad++, not VS Code)
2. Paste this EXACTLY:
```
<Ui xmlns="http://www.blizzard.com/wow/ui/">
<Button name="BindPadMacro" inherits="SecureActionButtonTemplate"/>
<Button name="BindPadKey" inherits="SecureActionButtonTemplate"/>
</Ui>
```
3. Save As → select "ANSI" encoding
4. Save to: `C:\Program Files (x86)\World of Warcraft\_anniversary_\Interface\AddOns\BindPadMinimal\BindPadMinimal.xml`

---

## Issue 2: /dcactions Shows Nothing

**Symptom**: Typing `/dcactions` in WoW chat produces no output

**Cause**: The `BindPadMacro` frame doesn't exist

**Diagnosis**: In WoW chat, type:
```
/run print(BindPadMacro and "EXISTS" or "NIL")
```

If it prints "NIL", the BindPadMinimal addon isn't loading.

**Solution**: Fix the BindPadMinimal addon (see Issue 1)

---

## Issue 3: Auto Config Button Does Nothing

**Symptom**: Clicking "Auto → Start" on web UI has no visible effect

**Causes**:
1. Blazor SignalR connection crashed (see error banner at top)
2. SHIFT-PAGEUP keybinding not set up
3. WoW window not in foreground

**Solutions**:
1. Click "Reload" link in error banner, or press F5 to refresh
2. Ensure `/dcactions` worked first
3. Keep WoW window visible when clicking Auto Start

---

## Issue 4: frame_config.json Missing

**Symptom**: Log shows "FrameConfig doesn't exists!"

**Cause**: Auto configuration never completed successfully

**Solution**: Complete the full setup chain:
1. Fix BindPadMinimal addon
2. Run `/reload` in WoW
3. Run `/dcactions` - verify you see binding messages
4. Run `/dc` - should show "Config mode"
5. Refresh web UI
6. Click Auto → Start
7. File will be created automatically

---

## Issue 5: DataFrames 0 - Texture: Size [Width=1, Height=1]

**Symptom**: Log shows only 1x1 texture size

**Meaning**: The addon is in Normal Mode, not Config Mode

**Cause**: Either:
- The `/dc` command wasn't run
- The SHIFT-PAGEUP binding doesn't work
- Something is blocking pixel [0,0]

**Solution**: 
1. Manually type `/dc` in WoW to enter Config Mode
2. The size should change when in config mode
3. If using ElvUI, ensure nothing covers the top-left corner

---

## Issue 6: Original BindPad Has 48 LUA Warnings

**Symptom**: Many warnings about deprecated XML elements

**Cause**: The bundled BindPad addon uses old XML syntax (`<Backdrop>`, `<AbsValue>`)

**Solution**: Use BindPadMinimal instead (already renamed original to BindPad_DISABLED)

---

## Verification Commands (In WoW Chat)

```
/run print(BindPadMacro and "EXISTS" or "NIL")   -- Should print EXISTS
/dcbindings                                        -- Should print "Bindings applied"
/dcactions                                         -- Should print binding messages
/dc                                                -- Toggle config mode
/run print(DataToColor and "LOADED" or "NIL")     -- Should print LOADED
```

---

## File Locations Quick Reference

| File | Path |
|------|------|
| BindPadMinimal TOC | `...\Interface\AddOns\BindPadMinimal\BindPadMinimal.toc` |
| BindPadMinimal XML | `...\Interface\AddOns\BindPadMinimal\BindPadMinimal.xml` |
| frame_config.json | `C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0\frame_config.json` |
| Navigation config | `C:\WowClassicGrindBot\Navigation\config.cfg` |
| WoW Config | `...\WTF\Config.wtf` |

(Where `...` = `C:\Program Files (x86)\World of Warcraft\_anniversary_`)
