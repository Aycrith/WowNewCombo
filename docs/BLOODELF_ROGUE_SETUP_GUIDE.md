# Blood Elf Rogue Starter Guide

## Profile Created
**Location**: `C:\WowClassicGrindBot\Json\class\BloodElf_Rogue_Starter_Test.json`

## Character Info
- **Race**: Blood Elf (TBC)
- **Class**: Rogue
- **Status**: Detected and validated by bot

## Profile Features

### Combat Rotation
1. **Pull Sequence**:
   - Stealth (Key `1`) - when energy > 30 and not swimming
   - Cheap Shot (Key `3`) - stun from stealth when in melee range
   - Approach - move to target

2. **Combat Sequence**:
   - Evasion (Key `5`) - when health < 30%
   - Gouge (Key `4`) - when health < 40% and multiple mobs
   - Slice and Dice (Key `6`) - with 2+ combo points, target > 40% health
   - Eviscerate (Key `7`) - with 3+ combo points
   - Sinister Strike (Key `2`) - when energy >= 45
   - Auto Attack
   - Approach

3. **Healing**:
   - Food (Key `=`) - when health < 50%

4. **Out of Combat**:
   - Stealth automatically when not in combat/swimming

### Grinding Paths
The profile will automatically select paths based on character level:

1. **Level 1-6**: `1-6_Eversong Woods.json`
   - Sunstrider Isle starting area
   - 47 waypoints covering beginner mob areas

2. **Level 6-12**: `6-12_Eversong Woods.json`
   - Main Eversong Woods zone
   - Extended grinding route

3. **Level 9-12**: `9-12_Ghostlands.json`
   - Ghostlands zone
   - Alternative for level 9+

### Settings
- **Loot**: Enabled
- **Gather Corpse**: Enabled (will walk to corpses)
- **Mount**: Disabled (low level character)
- **PvP**: Disabled
- **NPC Max Levels**: +2 above, -5 below
- **Path Mode**: There and Back (circular routes)

## Required Action Bars Setup

You need to bind these abilities in WoW for the bot to use them:

| Ability | Key | Notes |
|---------|-----|-------|
| Stealth | `1` | Main stealth ability |
| Sinister Strike | `2` | Basic combo point builder |
| Cheap Shot | `3` | Opener from stealth |
| Gouge | `4` | CC ability for emergencies |
| Evasion | `5` | Defensive cooldown |
| Slice and Dice | `6` | Finisher for attack speed |
| Eviscerate | `7` | Damage finisher |
| Food | `=` | Eating for health regen |
| Sell/Repair NPC | `C` | Vendor interaction macro |

## How to Use

### Step 1: Verify Bot & WoW Running
```powershell
tasklist | findstr "BlazorServer WowClassic"
```

Both should be running.

### Step 2: Check Web UI
- Navigate to: http://localhost:5000
- Verify frame detection shows 324/324 frames
- Check character info displays correctly

### Step 3: Load Profile (Via Web UI)
1. Go to "Class Configuration" or "Settings" page
2. Click "Load Profile"
3. Select: `BloodElf_Rogue_Starter_Test.json`
4. Profile should load and display current settings

### Step 4: Position Character
- Log into your Blood Elf Rogue
- Position near the grinding path start point
  - Level 1-6: Sunstrider Isle in Eversong Woods
  - Level 6+: Main Eversong Woods area
- Ensure:
  - Health and mana are full
  - Inventory has space
  - Not in combat
  - Not in a building/indoor area

### Step 5: Bind Abilities
Make sure all the abilities above are bound to the correct keys in your action bars.

### Step 6: Start Bot
- In Web UI, click "Start" or enable bot
- Monitor for first 5-10 minutes
- Check logs for errors

### Step 7: Monitor & Troubleshoot
Watch for:
- Does it target mobs?
- Does combat rotation execute?
- Does it move along waypoints?
- Does it loot?
- Does it eat food when low?

## Expected Behavior

1. **Idle**: Bot will enter stealth when not in combat
2. **Target Acquisition**: Will find nearby mobs within level range
3. **Pull**: Approaches target, cheap shots from stealth
4. **Combat**: Builds combo points with Sinister Strike, finishes with Eviscerate
5. **Loot**: After kill, loots corpse
6. **Recovery**: Eats food if health < 50%
7. **Pathing**: Follows waypoint route, kills along the path

## Troubleshooting

### Bot doesn't move
- Check navigation server isn't crashing (known issue)
- Try simple waypoint routes
- Verify character is not stuck in geometry

### Abilities don't fire
- Verify keybinds match profile
- Check action bars have correct abilities
- Look for "Key not found" errors in logs

### Character dies frequently
- Lower NPC level range
- Increase health% thresholds for defensives
- Reduce pull radius

### Won't loot
- Check "Gather Corpse" is enabled
- Verify mob corpses are lootable
- Check inventory isn't full

## Log Files
- **Current log**: `C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0\out20260203.log`
- **View real-time**:
  ```powershell
  Get-Content "C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0\out20260203.log" -Wait -Tail 50
  ```

## Next Steps After Testing

If basic grinding works:
1. Fine-tune combat thresholds
2. Add vendor paths for repair/sell
3. Test longer grindsessions (30+ minutes)
4. Add bandages and other consumables
5. Optimize ability priorities
6. Test different level ranges and zones

## Known Limitations

- Navigation server crashes (uses fallback simple movement)
- No complex pathfinding (keep routes simple)
- May get stuck on terrain (manual intervention needed)
- No death recovery yet (manual corpse run)

## Safety Notes

- **Do NOT leave bot unattended for long periods**
- Monitor regularly for stuck states
- Have manual control ready to take over
- Test in low-traffic areas first
- Be aware of TOS violations with botting

---

**Created**: February 3, 2026  
**Profile**: BloodElf_Rogue_Starter_Test.json  
**For**: WowClassicGrindBot - TBC Blood Elf Rogue
