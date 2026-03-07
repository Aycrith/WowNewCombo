# MEGA Download Workaround Guide

## The Problem
MEGAcmd and automated tools are having connectivity issues. The MEGA API servers may be temporarily unavailable or blocking automated access.

## Recommended Solution: Browser Download with Management

### Step 1: Open Download Links
Run this script to open both download pages:
```cmd
C:\WowClassicGrindBot\Scripts\Open-Downloads.bat
```

This will open two browser tabs:
1. **common-2.MPQ** (1.7GB) - Vanilla pathfinding data
2. **mmaps archive** (~2GB) - V3 pathfinding data

### Step 2: Download Each File

**On each MEGA page:**
1. Click the blue **"Download"** button
2. Select **"Standard Download"** (free option)
3. If prompted, click **"Download via your browser"**
4. Choose save location:
   - For common-2.MPQ → `C:\WowClassicGrindBot\Json\MPQ\common-2.MPQ`
   - For mmaps archive → `C:\WowClassicGrindBot\Navigation\mmaps.7z`

**Important:** Use your browser's download manager features:
- Chrome: Click ⋮ → Downloads → Shows progress and allows pause/resume
- Edge: Click ⚙ → Downloads → Similar features
- Firefox: Ctrl+J → Downloads panel

### Step 3: Handle Rate Limits

If you see "Transfer quota exceeded":

**Option A: Wait and Resume (Free)**
- MEGA shows the countdown timer (usually 6 hours)
- After timer expires, revisit the link and click Download again
- Modern browsers can resume interrupted downloads

**Option B: VPN Method**
1. Install a free VPN (ProtonVPN, Windscribe, etc.)
2. Connect to a different country
3. Retry the download (new IP = new quota)

**Option C: MEGA Account (Recommended)**
1. Create free MEGA account at https://mega.nz/register
2. Log in
3. Downloads from your account get higher bandwidth

**Option D: MEGA Pro Trial**
- 30-day free trial with 4TB transfer/month
- Sign up at https://mega.nz/pro
- Download everything, then cancel before trial ends

### Step 4: Verify Downloads

After both downloads complete:

```powershell
# Check files exist
Test-Path "C:\WowClassicGrindBot\Json\MPQ\common-2.MPQ"
Test-Path "C:\WowClassicGrindBot\Navigation\mmaps.7z"

# Check sizes (should be > 1GB each)
(Get-Item "C:\WowClassicGrindBot\Json\MPQ\common-2.MPQ").Length / 1GB
(Get-Item "C:\WowClassicGrindBot\Navigation\mmaps.7z").Length / 1GB
```

### Step 5: Extract MMAP Archive

Once the .7z file downloads:

```powershell
# Install 7-Zip if needed
winget install 7zip.7zip

# Extract mmaps
cd C:\WowClassicGrindBot\Navigation
& "C:\Program Files\7-Zip\7z.exe" x mmaps.7z

# Verify extraction
Get-ChildItem mmaps\*.map | Measure-Object | Select-Object -ExpandProperty Count
```

---

## Alternative: Download Manager Software

If browser downloads are unreliable:

### Internet Download Manager (IDM) - Trial
1. Download from: https://www.internetdownloadmanager.com/
2. 30-day free trial
3. Excellent resume capability
4. Sometimes bypasses MEGA rate limits
5. Configure: Options → Downloads → Save files to correct paths

### Free Download Manager (FDM)
1. Download from: https://www.freedownloadmanager.org/
2. Completely free
3. Resume support
4. Add MEGA links directly to FDM
5. Set download path to WowClassicGrindBot folders

---

## Manual Download URLs

If you need to copy-paste:

**common-2.MPQ (Vanilla, 1.7GB):**
```
https://mega.nz/file/vXQCBCha#m7COhB9HQd86a5iNAT0-fMLsc-BtoTRO1eIBJNrdTH8
```
Save to: `C:\WowClassicGrindBot\Json\MPQ\common-2.MPQ`

**mmaps archive (AmeisenNavigation, ~2GB):**
```
https://mega.nz/file/7HgkHIyA#c_gzUeTadecWY0JDY3KT39ktfPGLs2vzt_90bMvhszk
```
Save to: `C:\WowClassicGrindBot\Navigation\mmaps.7z`

---

## Why MEGAcmd Isn't Working

Based on the error "Unexpected failure to access server: 5":
- MEGA's API servers may be blocking datacenter IPs
- MEGAcmd needs authentication for large downloads
- Server connectivity issues on MEGA's end

The browser method is actually more reliable because:
✅ MEGA prioritizes browser downloads
✅ Better rate limit handling
✅ Shows progress and estimated time
✅ Built-in resume capability
✅ Works without extra software

---

## After Downloads Complete

1. **Test V1 Pathfinding** (uses common-2.MPQ):
   ```cmd
   C:\WowClassicGrindBot\Scripts\Configure-Pathfinder.ps1 -Backend Local
   ```

2. **Test V3 Pathfinding** (uses mmaps):
   ```cmd
   C:\WowClassicGrindBot\Navigation\StartNavigationServer.bat
   ```

3. **Start the bot:**
   ```cmd
   C:\WowClassicGrindBot\StartAll.bat
   ```

---

## Progress Checklist

- [x] MEGAcmd installed
- [ ] common-2.MPQ downloaded (1.7GB)
- [ ] mmaps.7z downloaded (~2GB)
- [ ] mmaps.7z extracted to Navigation\mmaps\
- [ ] Pathfinder configuration tested
- [ ] Bot ready to run

---

## Need Help?

If downloads continue to fail:
1. Check your internet connection stability
2. Temporarily disable antivirus/firewall
3. Try downloading from different network (mobile hotspot, etc.)
4. Join WowClassicGrindBot Discord for community support
5. Check GitHub issues: https://github.com/Xian55/WowClassicGrindBot/issues
