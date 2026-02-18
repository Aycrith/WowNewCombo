================================================================================
WowClassicGrindBot - Complete Setup & Operation Guide
================================================================================

Welcome to WowClassicGrindBot! This guide will get you up and running in minutes.

================================================================================
TABLE OF CONTENTS
================================================================================

1. Quick Start (2 minutes)
2. System Requirements
3. Installation & First Run
4. Detailed Operation
5. Configuration
6. Features
7. Keyboard Shortcuts
8. Troubleshooting Links
9. Support & Community

================================================================================
1. QUICK START (JUST 2 MINUTES!)
================================================================================

STEP 1: Prerequisites
  □ Have Windows 10 or newer
  □ Have World of Warcraft (TBC Anniversary) installed
  □ Have .NET 10.0+ installed (download if needed)

STEP 2: Launch
  1. Open World of Warcraft
  2. Log in to a character (must be in game world)
  3. Double-click: C:\WowClassicGrindBot\Start.bat
  4. Wait for validation messages (30 seconds)
  5. Browser will open automatically to http://localhost:5000
  6. Bot UI will appear - start grinding!

DONE! That's it. The bot handles the rest.

================================================================================
2. SYSTEM REQUIREMENTS
================================================================================

MINIMUM REQUIREMENTS:
  • Windows 10 (Windows 11 recommended)
  • Intel i5 or equivalent AMD processor
  • 8 GB RAM (12 GB+ recommended)
  • SSD (faster bot startup)
  • Internet connection (for initial setup)

REQUIRED SOFTWARE:
  • World of Warcraft (TBC Anniversary or later)
  • .NET SDK 10.0 or later (FREE from Microsoft)

OPTIONAL:
  • Navigation Server (for advanced pathfinding)
  • MMAP files (for better navigation - will work without)

================================================================================
3. INSTALLATION & FIRST RUN
================================================================================

INSTALLATION PROCESS:

Step 1: Extract Bot Files
  1. Download WowClassicGrindBot
  2. Extract to: C:\WowClassicGrindBot
  3. You should see folders: BlazorServer, Core, Frontend, Navigation, etc.

Step 2: Install .NET (if needed)
  1. Open Command Prompt
  2. Run: dotnet --version
  3. If you see 10.0 or higher, skip to Step 3
  4. If not found, download from: https://dotnet.microsoft.com/download
  5. Install .NET SDK 10.0 or newer
  6. Restart computer
  7. Verify: dotnet --version (should work now)

Step 3: Prepare WoW
  1. Open World of Warcraft
  2. Click Play button
  3. Enter your credentials
  4. Select realm and character
  5. Click Play to enter game
  6. IMPORTANT: Wait until you see the game world
  7. Don't use bot until character is fully loaded

Step 4: Launch Bot
  1. With WoW running (character in game), double-click Start.bat
  2. You'll see colorful startup messages
  3. System will validate:
     ✓ .NET SDK installed
     ✓ Bot files present
     ✓ WoW installation location
     ✓ Navigation server (optional)
     ✓ Addon files
  4. System will build the bot (takes 30-60 seconds on first run)
  5. Press any key when prompted
  6. Bot will launch and open browser automatically
  7. Bot UI appears at http://localhost:5000

FIRST RUN SPECIAL HANDLING:
  • Addon will auto-install to WoW
  • Bot will auto-configure UI frames
  • This may take 1-2 minutes
  • You'll see status messages in console
  • Bot will be ready when UI appears

================================================================================
4. DETAILED OPERATION
================================================================================

STARTING THE BOT:
  Standard Start:
    • Double-click: C:\WowClassicGrindBot\Start.bat
    • Or: Right-click -> Run as Administrator (if permission issues)
    
  Command Line (advanced):
    • Open Command Prompt in bot directory
    • Run: BlazorServer\bin\Release\net10.0\BlazorServer.exe
    • Or: dotnet run --project BlazorServer/BlazorServer.csproj

ACCESSING THE BOT UI:
  • Browser opens automatically to: http://localhost:5000
  • If not, manually open browser and go to: http://localhost:5000
  • Supported browsers: Chrome, Firefox, Edge, Safari
  • Best performance: Chrome or Edge (Chromium-based)

STOPPING THE BOT:
  • Close the launcher window (the dark console with bot name)
  • Or press Ctrl+C in the launcher window
  • Or close browser tab and wait 30 seconds for shutdown
  • Always close WoW AFTER bot stops

UNDERSTANDING THE CONSOLE:
  [HH:MM:SS] [LOG LEVEL] [Component] Message
  
  Log Levels:
    [DBG] Debug info (ignore these in normal operation)
    [INF] Informational (normal bot operations)
    [WRN] Warning (usually non-critical issues)
    [ERR] Error (something went wrong)
    [FTL] Fatal (bot stopping, serious issue)
  
  Check out.log file for full details:
    • Located in: C:\WowClassicGrindBot\out.log
    • Updated daily with rolling backups
    • Contains all console messages since bot started

MONITORING THE BOT:
  • Watch browser UI for status and actions
  • Watch console for error messages
  • Open out.log to check for issues
  • Use Windows Task Manager to check CPU/Memory usage

================================================================================
5. CONFIGURATION
================================================================================

BROWSER AUTO-LAUNCH:
  • Enabled by default (browser opens automatically)
  • To disable:
    1. Edit: C:\WowClassicGrindBot\BlazorServer\appsettings.json
    2. Find: "AutoOpenBrowser": true
    3. Change to: "AutoOpenBrowser": false
    4. Restart bot
  • If browser doesn't open, manually visit http://localhost:5000

PORT CONFIGURATION:
  • Bot uses Port 5000 by default (for web UI)
  • Navigation Server uses Port 47110
  • To change bot port:
    1. Edit appsettings.json
    2. Change: "WebUIPort": 5000
    3. Use new port: http://localhost:5001 (or your new port)

WOW PATH:
  • Bot auto-detects WoW installation
  • Usually finds it at: C:\Program Files (x86)\World of Warcraft\_anniversary_
  • If not found, manually set in appsettings.json:
    "WoWPath": "C:\Program Files (x86)\World of Warcraft\_anniversary_"

ADDON CONFIGURATION:
  • DataToColor addon auto-installs to WoW
  • Configuration created: C:\WowClassicGrindBot\addon_config.json
  • Usually requires no manual setup
  • If addon not installing, see TROUBLESHOOTING.txt

NAVIGATION SERVER:
  • Optional feature for advanced pathfinding
  • If available, bot uses it automatically
  • MMAP files required for full functionality
  • Works without it (just simpler navigation)
  • To disable: Edit appsettings.json, set AutoStartNavigationServer: false

FRAME CONFIGURATION:
  • Bot auto-configures WoW UI frame positions on first run
  • Configuration saved to: C:\WowClassicGrindBot\FrameConfig.xml
  • Re-configure in bot UI if needed
  • Usually requires no manual intervention

================================================================================
6. FEATURES
================================================================================

WEB-BASED INTERFACE:
  • Real-time bot status
  • Grinding profiles
  • Character management
  • Route planning
  • Combat settings
  • Loot management
  • Statistics & analytics

AUTOMATION:
  • Automated grinding routes
  • Intelligent combat
  • Loot handling (vendor, disenchant, keep)
  • Potion/buff usage
  • Deaths recovery
  • Low-level grinding profiles

GAME INTEGRATION:
  • Direct WoW process reading
  • Real-time character data
  • Frame positioning
  • Movement control
  • Ability execution
  • Game world pathing

SAFETY FEATURES:
  • Graceful error handling
  • Automatic recovery from crashes
  • Health monitoring
  • Anti-detection measures
  • Clean shutdown

================================================================================
7. KEYBOARD SHORTCUTS
================================================================================

WHILE RUNNING BOT:

Alt+Tab      Switch between WoW and Browser
Ctrl+C       Stop bot (in launcher window)
F5           Refresh browser UI

IN GAME (if configured):
  • Your profile determines in-game controls
  • Numpad keys typically used for emergency actions
  • See bot UI for specific key bindings

================================================================================
8. TROUBLESHOOTING LINKS
================================================================================

If you encounter issues:

1. READ: C:\WowClassicGrindBot\TROUBLESHOOTING.txt
   • 10 sections covering common problems
   • Step-by-step solutions
   • Command-line diagnostics
   • Advanced debugging

2. CHECK: C:\WowClassicGrindBot\out.log
   • Search for "[ERR]" or "[FTL]" lines
   • Use Ctrl+F in Notepad to search
   • Share relevant lines when asking for help

3. QUICK FIXES:
   • Bot won't start → Ensure WoW is running and logged in
   • Browser won't open → Visit http://localhost:5000 manually
   • Build fails → Run as Administrator
   • Port in use → Change port in appsettings.json
   • Addon issues → See TROUBLESHOOTING.txt section 7

4. GITHUB:
   • https://github.com/FreeHongKongMMO/WowClassicGrindBot
   • Open issue with error details
   • Include out.log contents
   • Include your system specs

================================================================================
9. SUPPORT & COMMUNITY
================================================================================

GITHUB REPOSITORY:
  https://github.com/FreeHongKongMMO/WowClassicGrindBot
  • Report bugs and issues
  • Request features
  • View source code
  • Download latest version

WIKI & DOCUMENTATION:
  https://github.com/FreeHongKongMMO/WowClassicGrindBot/wiki
  • Setup guides
  • Configuration examples
  • Profile documentation
  • Advanced features

COMMUNITY:
  • GitHub Discussions (if available)
  • GitHub Issues (for bugs)
  • Community Discord (if available)

GETTING HELP:
  When reporting issues, include:
  1. Exact error message
  2. Your WoW version and realm
  3. Your Windows version
  4. .NET version (run: dotnet --version)
  5. Last 30 lines from out.log
  6. Your system specs (RAM, CPU, SSD)
  7. Steps to reproduce the issue

================================================================================
ADVANCED TOPICS
================================================================================

RUNNING FROM COMMAND LINE:
  cd C:\WowClassicGrindBot
  
  # Debug build (slower startup, full logging)
  dotnet run --project BlazorServer/BlazorServer.csproj
  
  # Release build (faster, optimized)
  BlazorServer\bin\Release\net10.0\BlazorServer.exe

ACCESSING BOT REMOTELY:
  • By default, bot only accessible from localhost
  • To access from other machines:
    1. Edit appsettings.json
    2. Change: "http://localhost:5000"
    3. To: "http://0.0.0.0:5000" (all interfaces)
    4. Or: "http://[your-ip]:5000"
    5. Access from other PC at that address
  • WARNING: Only do this on trusted networks

DEVELOPMENT MODE:
  • Run with Debug build for more logging
  • Check out.log for detailed information
  • Use browser DevTools (F12) for UI debugging
  • View bot logs in real-time in console

PERFORMANCE OPTIMIZATION:
  • Use Release build instead of Debug
  • Close other applications
  • Reduce WoW graphics settings
  • Disable Discord overlay
  • Use SSD (faster boot)

================================================================================
COMMON QUESTIONS
================================================================================

Q: Can I run the bot while playing manually?
A: No, bot controls character movement and abilities. Bot must be in control.

Q: Is it safe to use on my main character?
A: Use at own risk. Some games ban for automation. Use throwaway/farm characters.

Q: Can I run multiple bots?
A: Currently designed for single instance. Each bot needs separate WoW instance.

Q: Why does bot need WoW running before starting?
A: Bot reads game data from WoW process. Can't work without it. (Architectural)

Q: Can I run bot on macOS or Linux?
A: No, requires Windows and .NET. Some components are Windows-specific.

Q: Does bot work offline?
A: No, WoW requires online connection to Blizzard servers.

Q: What about antivirus warnings?
A: Bot downloads .NET components. Whitelist if trusted. Some AV flags legitimate code.

Q: Can I use VPN/Proxy?
A: Should work through most VPNs. May cause lag. Test with VPN on.

Q: How much CPU/Memory does bot use?
A: Usually 3-8% CPU, 150-300 MB memory (plus WoW usage).

Q: How long can bot run?
A: Designed for 24/7 operation. Monitor memory periodically.

Q: Why are frame configs needed?
A: Bot reads screen data from WoW UI elements. Positions must be accurate.

================================================================================
FINAL TIPS
================================================================================

1. ALWAYS START WOW FIRST
   Bot cannot start without WoW running. Start WoW, log in, THEN start bot.

2. WATCH THE FIRST RUN
   First startup takes longer and configures files. Monitor console for progress.

3. KEEP IT UPDATED
   Check GitHub for updates. New fixes and features released regularly.

4. MONITOR PERFORMANCE
   Keep Task Manager open while testing. Check CPU and memory usage.

5. READ LOGS ON ERROR
   out.log contains detailed information. Search for [ERR] or [FTL] lines.

6. USE TROUBLESHOOTING GUIDE
   TROUBLESHOOTING.txt has solutions for 95% of issues.

7. JOIN COMMUNITY
   GitHub Issues is most responsive way to get help.

8. REPORT BUGS PROPERLY
   Include error message, out.log, system specs, and reproduction steps.

9. BACKUP YOUR SETTINGS
   Save addon_config.json and FrameConfig.xml periodically.

10. RESPECT THE GAME
    Be aware this tool can be flagged by anticheat. Use responsibly.

================================================================================
VERSION & BUILD INFO
================================================================================

Version: TBC Anniversary Edition
Build: .NET 10.0
Release: Production Ready
Last Updated: 2/2/2026

For latest version info, check GitHub.

================================================================================
LICENSE & DISCLAIMER
================================================================================

This bot is provided as-is. Use at your own risk.

• Modifying WoW game data violates Terms of Service
• Accounts using bots may be banned
• Use farm/throwaway characters at your own discretion
• No warranty provided - use at own risk
• Keep software updated for security

================================================================================
QUICK REFERENCE CARD
================================================================================

Task                          Command/Action
─────────────────────────────────────────────────────────────────
Start bot (normal)            Double-click Start.bat
Start bot (admin)             Right-click Start.bat → Run as admin
Stop bot                      Close launcher window or Ctrl+C
Access bot UI                 Open browser to http://localhost:5000
Check .NET version            dotnet --version
View bot logs                 Open C:\WowClassicGrindBot\out.log
Get help                      Read TROUBLESHOOTING.txt
Reset configuration           Delete addon_config.json and FrameConfig.xml
Change web port               Edit appsettings.json, change WebUIPort
Disable auto browser open     Edit appsettings.json, set AutoOpenBrowser: false
View running processes        tasklist | find "Wow"
Kill bot process              Close launcher window or taskkill /PID [number] /F

================================================================================
END OF GUIDE
================================================================================

Questions? Check TROUBLESHOOTING.txt or open an issue on GitHub.
Good luck and happy grinding!

https://github.com/FreeHongKongMMO/WowClassicGrindBot
