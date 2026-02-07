using Core;
using Core.Testing;

using Game;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Frontend.Controllers;

/// <summary>
/// API controller for end-to-end testing of bot systems
/// Tests input simulation, data reading, and full bot cycles
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;
    private readonly IAddonDataProvider _addonData;
    private readonly PlayerReader _playerReader;
    private readonly AddonBits _bits;
    private readonly IWowScreen _screen;
    private readonly WowProcess _wowProcess;
    private readonly WowProcessInput _input;
    private readonly IBotController _botController;

    public TestController(
        ILogger<TestController> logger,
        IAddonDataProvider addonData,
        PlayerReader playerReader,
        AddonBits bits,
        IWowScreen screen,
        WowProcess wowProcess,
        WowProcessInput input,
        IBotController botController)
    {
        _logger = logger;
        _addonData = addonData;
        _playerReader = playerReader;
        _bits = bits;
        _screen = screen;
        _wowProcess = wowProcess;
        _input = input;
        _botController = botController;
    }

    #region Phase 1: Safe Read-Only Tests

    /// <summary>
    /// GET /api/test/status
    /// Validate all bot systems are operational
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];

        try
        {
            // Check 1: WoW Process Running
            bool processRunning = _wowProcess.MainWindowHandle != IntPtr.Zero;
            checks.Add(new TestCheck(
                "WoW Process Running",
                processRunning,
                "Valid handle",
                processRunning ? $"Handle: {_wowProcess.MainWindowHandle}" : "No handle",
                processRunning ? null : "WoW process not found or not running"));

            // Check 2: Frame Configuration Exists
            bool frameConfigExists = FrameConfig.Exists();
            checks.Add(new TestCheck(
                "Frame Configuration",
                frameConfigExists,
                "Exists",
                frameConfigExists ? "frame_config.json found" : "Missing",
                frameConfigExists ? null : "Run frame configuration first"));

            // Check 3: Addon Communication (Validation Marker)
            int validationMarker = _addonData.GetInt(323);
            bool addonCommunicating = validationMarker == 2000001;
            checks.Add(new TestCheck(
                "Addon Communication",
                addonCommunicating,
                "2000001",
                validationMarker.ToString(),
                addonCommunicating ? "DataToColor addon responding" : "Addon not visible or not loaded"));

            // Check 4: Frame Count
            if (frameConfigExists)
            {
                DataFrameMeta meta = FrameConfig.LoadMeta();
                bool correctFrameCount = meta.Count == 324;
                checks.Add(new TestCheck(
                    "Frame Count",
                    correctFrameCount,
                    "324",
                    meta.Count.ToString(),
                    correctFrameCount ? null : "Frame count mismatch"));
            }

            // Check 5: Character Detection
            int raceClassVersion = _addonData.GetInt(46);
            int race = raceClassVersion / 10000;
            int playerClass = (raceClassVersion / 100) % 100;
            bool characterDetected = race > 0 && playerClass > 0;
            checks.Add(new TestCheck(
                "Character Detection",
                characterDetected,
                "Valid race/class",
                characterDetected ? $"{(UnitRace)race} {(UnitClass)playerClass}" : "No character data",
                characterDetected ? $"Level {_playerReader.Level.Value}" : "Character not detected"));

            // Check 6: Screen Capture
            _screen.GetRectangle(out var screenRect);
            bool screenCaptureActive = screenRect.Width > 0 && screenRect.Height > 0;
            checks.Add(new TestCheck(
                "Screen Capture",
                screenCaptureActive,
                "Active",
                screenCaptureActive ? $"{screenRect.Width}x{screenRect.Height}" : "Inactive",
                screenCaptureActive ? null : "Screen capture not initialized"));

            // Check 7: Data Freshness (global time should change)
            int initialTime = _addonData.GetInt(322);
            Thread.Sleep(100);
            _addonData.UpdateData();
            int updatedTime = _addonData.GetInt(322);
            bool dataUpdating = updatedTime != initialTime;
            checks.Add(new TestCheck(
                "Data Freshness",
                dataUpdating,
                "Updating",
                dataUpdating ? $"Changed from {initialTime} to {updatedTime}" : "Stale",
                dataUpdating ? "Frame data updating normally" : "Frame data may be frozen"));

            sw.Stop();

            bool allPassed = checks.All(c => c.Passed);
            string overallStatus = allPassed ? "✅ Ready for testing" : "⚠️ Some checks failed";

            Dictionary<string, object> data = new()
            {
                ["wowPid"] = _wowProcess.Id,
                ["screenSize"] = $"{screenRect.Width}x{screenRect.Height}",
                ["character"] = characterDetected ? $"{_playerReader.Race} {_playerReader.Class} L{_playerReader.Level.Value}" : "Unknown",
                ["addonVersion"] = frameConfigExists ? FrameConfig.LoadMeta().Hash.ToString() : "N/A",
                ["botActive"] = _botController.IsBotActive
            };

            return Ok(TestResult.Pass(
                "System Status Check",
                sw.Elapsed,
                checks,
                data,
                overallStatus));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status check failed with exception");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("System Status Check", sw.Elapsed, ex));
        }
    }

    /// <summary>
    /// GET /api/test/frames
    /// Validate frame data reading from addon
    /// </summary>
    [HttpGet("frames")]
    public IActionResult ValidateFrames()
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];

        try
        {
            // Force frame update
            _addonData.UpdateData();

            // Validate critical frames
            Dictionary<string, object> frameData = new();
            List<int> invalidFrames = [];

            // Frame 323: Validation marker
            int validationMarker = _addonData.GetInt(323);
            checks.Add(TestHelpers.CreateCheck("Frame 323 (Validation)", 2000001, validationMarker));
            frameData["validationMarker"] = validationMarker;

            // Frame 5: Level
            int level = _playerReader.Level.Value;
            checks.Add(TestHelpers.CreateRangeCheck("Frame 5 (Level)", level, 1, 70));
            frameData["level"] = level;

            // Frame 10-11: Health
            int healthMax = _playerReader.HealthMax();
            int healthCurrent = _playerReader.HealthCurrent();
            checks.Add(TestHelpers.CreateBoolCheck("Frame 10 (HealthMax)", healthMax > 0));
            checks.Add(TestHelpers.CreateBoolCheck("Frame 11 (HealthCurrent)", healthCurrent > 0 && healthCurrent <= healthMax));
            frameData["health"] = $"{healthCurrent}/{healthMax} ({_playerReader.HealthPercent()}%)";

            // Frame 12-13: Power
            int powerMax = _playerReader.PTMax();
            int powerCurrent = _playerReader.PTCurrent();
            checks.Add(TestHelpers.CreateBoolCheck("Frame 12 (PowerMax)", powerMax > 0));
            checks.Add(TestHelpers.CreateBoolCheck("Frame 13 (PowerCurrent)", powerCurrent >= 0 && powerCurrent <= powerMax));
            frameData["power"] = $"{powerCurrent}/{powerMax}";

            // Frame 46: Race/Class
            int expectedRace = 10; // BloodElf
            int expectedClass = 4;  // Rogue
            checks.Add(TestHelpers.CreateCheck("Frame 46 (Race)", expectedRace, (int)_playerReader.Race, "Expected BloodElf"));
            checks.Add(TestHelpers.CreateCheck("Frame 46 (Class)", expectedClass, (int)_playerReader.Class, "Expected Rogue"));
            frameData["race"] = _playerReader.Race.ToString();
            frameData["class"] = _playerReader.Class.ToString();

            // Frame 1-2: Position
            float mapX = _playerReader.MapX;
            float mapY = _playerReader.MapY;
            bool validPosition = mapX > 0 && mapY > 0;
            checks.Add(TestHelpers.CreateBoolCheck("Frame 1-2 (Position)", validPosition, $"X={mapX:F2}, Y={mapY:F2}"));
            frameData["position"] = new { x = mapX, y = mapY, mapId = _playerReader.UIMapId.Value };

            // Frame 8-9: State bits
            int bits1 = _addonData.GetInt(8);
            int bits2 = _addonData.GetInt(9);
            bool hasStateData = bits1 != 0 || bits2 != 0;
            checks.Add(TestHelpers.CreateBoolCheck("Frame 8-9 (State Bits)", hasStateData));
            frameData["stateBits"] = new
            {
                inCombat = _bits.Combat(),
                hasTarget = _bits.Target(),
                stealthed = _bits.Stealthed(),
                mounted = _bits.Mounted(),
                dead = _bits.Dead()
            };

            // Sample all 324 frames and count invalid ones
            for (int i = 0; i < 324; i++)
            {
                int value = _addonData.GetInt(i);
                // Check for obvious error values
                if (value == -1 || value == 0xFFFFFF)
                {
                    invalidFrames.Add(i);
                }
            }

            int validFrameCount = 324 - invalidFrames.Count;
            checks.Add(new TestCheck(
                "All Frames Readable",
                invalidFrames.Count == 0,
                "324/324",
                $"{validFrameCount}/324",
                invalidFrames.Count > 0 ? $"Invalid frames: {string.Join(", ", invalidFrames)}" : null));

            frameData["validFrameCount"] = validFrameCount;
            frameData["invalidFrames"] = invalidFrames;

            sw.Stop();

            bool allPassed = checks.All(c => c.Passed);
            return Ok(TestResult.Pass(
                "Frame Data Validation",
                sw.Elapsed,
                checks,
                frameData,
                allPassed ? "✅ All frames valid" : "⚠️ Some frame validations failed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Frame validation failed with exception");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("Frame Data Validation", sw.Elapsed, ex));
        }
    }

    /// <summary>
    /// GET /api/test/snapshot
    /// Capture current player state snapshot
    /// </summary>
    [HttpGet("snapshot")]
    public IActionResult GetSnapshot()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Force fresh data
            _addonData.UpdateData();

            // Capture snapshot
            PlayerStateSnapshot snapshot = PlayerStateSnapshot.Capture(_playerReader, _bits);

            sw.Stop();

            Dictionary<string, object> data = new()
            {
                ["snapshot"] = snapshot,
                ["capturedAt"] = snapshot.CapturedAt,
                ["elapsedMs"] = sw.ElapsedMilliseconds
            };

            List<TestCheck> checks =
            [
                TestHelpers.CreateBoolCheck("Snapshot Captured", true, "Successfully captured player state"),
            ];

            return Ok(TestResult.Pass(
                "Player State Snapshot",
                sw.Elapsed,
                checks,
                data,
                "✅ Snapshot captured successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snapshot capture failed");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("Player State Snapshot", sw.Elapsed, ex));
        }
    }

    #endregion

    #region Phase 2: Input Validation Tests

    /// <summary>
    /// POST /api/test/input/jump
    /// Tests Space key delivery by detecting Falling state change.
    /// Expected: Character jumps and Falling flag becomes true.
    /// </summary>
    [HttpPost("input/jump")]
    public async Task<IActionResult> TestJump()
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];

        try
        {
            // 1. Capture initial state
            _addonData.UpdateData();
            bool initialFalling = _bits.Falling();
            PlayerStateSnapshot before = PlayerStateSnapshot.Capture(_playerReader, _bits);

            checks.Add(TestHelpers.CreateBoolCheck("Initial state captured", true, "Player snapshot captured"));

            // 2. Send Space key (jump)
            _logger.LogInformation("Sending Space key for jump test");
            _input.PressRandom(ConsoleKey.Spacebar, 50);

            // 3. Wait for Falling state to become true (500ms timeout)
            Stopwatch responseTimer = Stopwatch.StartNew();
            bool fallingDetected = await TestHelpers.WaitForCondition(
                () =>
                {
                    _addonData.UpdateData();
                    return _bits.Falling();
                },
                timeoutMs: 500);
            responseTimer.Stop();

            checks.Add(TestHelpers.CreateBoolCheck(
                "Falling state detected",
                fallingDetected,
                fallingDetected ? $"Detected in {responseTimer.ElapsedMilliseconds}ms" : "Not detected within 500ms"));

            // 4. Wait for landing (up to 2 seconds)
            if (fallingDetected)
            {
                bool landed = await TestHelpers.WaitForCondition(
                    () =>
                    {
                        _addonData.UpdateData();
                        return !_bits.Falling();
                    },
                    timeoutMs: 2000);

                checks.Add(TestHelpers.CreateBoolCheck(
                    "Character landed",
                    landed,
                    landed ? "Character returned to ground" : "Still falling after 2s (unusual)"));
            }

            // 5. Capture final state
            _addonData.UpdateData();
            PlayerStateSnapshot after = PlayerStateSnapshot.Capture(_playerReader, _bits);

            // 6. Build result data
            Dictionary<string, object> data = new()
            {
                ["initialFalling"] = initialFalling,
                ["fallingDetected"] = fallingDetected,
                ["responseTimeMs"] = responseTimer.ElapsedMilliseconds,
                ["beforeSnapshot"] = before,
                ["afterSnapshot"] = after
            };

            sw.Stop();

            string message = fallingDetected
                ? $"✅ Jump input validated ({responseTimer.ElapsedMilliseconds}ms response)"
                : "⚠️ Jump not detected - input may not be reaching WoW";

            return Ok(TestResult.Pass(
                "Jump Input Test",
                sw.Elapsed,
                checks,
                data,
                message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jump test failed");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("Jump Input Test", sw.Elapsed, ex));
        }
    }

    /// <summary>
    /// POST /api/test/input/stealth
    /// Tests Stealth ability toggle (Rogue key 1).
    /// Expected: Stealthed state toggles on/off.
    /// </summary>
    [HttpPost("input/stealth")]
    public async Task<IActionResult> TestStealth()
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];

        try
        {
            // 1. Capture initial stealth state
            _addonData.UpdateData();
            bool initialStealthed = _bits.Stealthed();
            bool inCombat = _bits.Combat();
            PlayerStateSnapshot before = PlayerStateSnapshot.Capture(_playerReader, _bits);

            checks.Add(TestHelpers.CreateBoolCheck(
                "Not in combat",
                !inCombat,
                inCombat ? "Cannot stealth while in combat" : "Ready for stealth test"));

            if (inCombat)
            {
                sw.Stop();
                return Ok(TestResult.Pass(
                    "Stealth Input Test",
                    sw.Elapsed,
                    checks,
                    new Dictionary<string, object> { ["error"] = "Character in combat, cannot test stealth" },
                    "⚠️ Test skipped - character in combat"));
            }

            // 2. Send key "1" (Stealth from profile)
            _logger.LogInformation("Sending key '1' for stealth toggle (initial state: {Initial})", initialStealthed);
            _input.PressRandom(ConsoleKey.D1, 50);

            // 3. Wait for stealth state change (2s timeout for GCD)
            Stopwatch responseTimer = Stopwatch.StartNew();
            bool stateChanged = await TestHelpers.WaitForValueChange(
                () =>
                {
                    _addonData.UpdateData();
                    return _bits.Stealthed();
                },
                initialStealthed,
                timeoutMs: 2000);
            responseTimer.Stop();

            _addonData.UpdateData();
            bool finalStealthed = _bits.Stealthed();

            checks.Add(TestHelpers.CreateBoolCheck(
                "Stealth state changed",
                stateChanged,
                stateChanged
                    ? $"Changed from {initialStealthed} to {finalStealthed} in {responseTimer.ElapsedMilliseconds}ms"
                    : $"No change detected (stayed {initialStealthed})"));

            checks.Add(TestHelpers.CreateBoolCheck(
                "Correct toggle direction",
                stateChanged && (finalStealthed == !initialStealthed),
                stateChanged ? "State toggled correctly" : "State did not toggle as expected"));

            // 4. Capture final state
            PlayerStateSnapshot after = PlayerStateSnapshot.Capture(_playerReader, _bits);

            // 5. Build result data
            Dictionary<string, object> data = new()
            {
                ["initialStealthed"] = initialStealthed,
                ["finalStealthed"] = finalStealthed,
                ["stateChanged"] = stateChanged,
                ["responseTimeMs"] = responseTimer.ElapsedMilliseconds,
                ["beforeSnapshot"] = before,
                ["afterSnapshot"] = after
            };

            sw.Stop();

            string message = stateChanged
                ? $"✅ Stealth toggle validated ({responseTimer.ElapsedMilliseconds}ms response)"
                : "⚠️ Stealth state did not change - ability may be on cooldown or input failed";

            return Ok(TestResult.Pass(
                "Stealth Input Test",
                sw.Elapsed,
                checks,
                data,
                message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stealth test failed");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("Stealth Input Test", sw.Elapsed, ex));
        }
    }

    #endregion

    #region Phase 3: Movement Validation Tests

    /// <summary>
    /// Request model for movement tests
    /// </summary>
    public class MovementTestRequest
    {
        public string Direction { get; set; } = "forward";
        public int DurationMs { get; set; } = 500;
        public bool ReturnToStart { get; set; }
    }

    /// <summary>
    /// POST /api/test/movement
    /// Tests WASD movement keys and validates position changes.
    /// </summary>
    [HttpPost("movement")]
    public async Task<IActionResult> TestMovement([FromBody] MovementTestRequest request)
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];

        try
        {
            // 1. Capture start position
            _addonData.UpdateData();
            PlayerStateSnapshot before = PlayerStateSnapshot.Capture(_playerReader, _bits);

            checks.Add(TestHelpers.CreateBoolCheck(
                "Initial position captured",
                true,
                $"Position: ({before.MapX:F2}, {before.MapY:F2})"));

            // 2. Determine key from direction
            // Use hardcoded WASD keys (WoW default) since config may not be loaded when bot is off
            ConsoleKey key = request.Direction.ToLower() switch
            {
                "forward" => ConsoleKey.W,
                "backward" => ConsoleKey.S,
                "left" => ConsoleKey.A,
                "right" => ConsoleKey.D,
                _ => ConsoleKey.W
            };

            _logger.LogInformation("Testing {Direction} movement with key {Key} for {Duration}ms",
                request.Direction, key, request.DurationMs);

            // 3. Press and hold key
            _input.KeyDown(key, forced: true);
            await Task.Delay(request.DurationMs);
            _input.KeyUp(key, forced: true);

            // 4. Wait for movement to settle
            await Task.Delay(200);
            _addonData.UpdateData();

            // 5. Capture end position
            PlayerStateSnapshot after = PlayerStateSnapshot.Capture(_playerReader, _bits);

            // 6. Calculate distance and rotation change
            float distance = after.DistanceFrom(before);
            float rotationChange = after.DirectionChangeFrom(before);

            // 7. Validate movement occurred
            bool movedPosition = distance > 0.01f;  // Moved at least 1cm
            bool changedRotation = Math.Abs(rotationChange) > 0.01f;  // Rotated at least 0.01 radians
            bool anyMovement = movedPosition || changedRotation;

            checks.Add(TestHelpers.CreateBoolCheck(
                "Movement detected",
                anyMovement,
                anyMovement
                    ? $"Distance: {distance:F2} yards, Rotation: {rotationChange:F2} rad"
                    : "No movement detected"));

            if (request.Direction == "forward" || request.Direction == "backward")
            {
                checks.Add(TestHelpers.CreateBoolCheck(
                    "Position changed",
                    movedPosition,
                    movedPosition ? $"Moved {distance:F2} yards" : "Position unchanged"));
            }
            else // left or right (turning)
            {
                checks.Add(TestHelpers.CreateBoolCheck(
                    "Direction changed",
                    changedRotation,
                    changedRotation ? $"Rotated {rotationChange:F2} radians" : "Direction unchanged"));
            }

            // 8. Build result data
            Dictionary<string, object> data = new()
            {
                ["direction"] = request.Direction,
                ["durationMs"] = request.DurationMs,
                ["key"] = key.ToString(),
                ["beforePosition"] = new { x = before.MapX, y = before.MapY, direction = before.Direction },
                ["afterPosition"] = new { x = after.MapX, y = after.MapY, direction = after.Direction },
                ["distance"] = distance,
                ["rotationChange"] = rotationChange,
                ["beforeSnapshot"] = before,
                ["afterSnapshot"] = after
            };

            sw.Stop();

            string message = anyMovement
                ? $"✅ Movement validated ({distance:F2}yd, {rotationChange:F2}rad)"
                : "⚠️ No movement detected - character may be stuck or movement keys not working";

            return Ok(TestResult.Pass(
                $"Movement Test ({request.Direction})",
                sw.Elapsed,
                checks,
                data,
                message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Movement test failed");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("Movement Test", sw.Elapsed, ex));
        }
    }

    /// <summary>
    /// POST /api/test/movement/wasd
    /// Tests all four WASD keys in sequence.
    /// </summary>
    [HttpPost("movement/wasd")]
    public async Task<IActionResult> TestAllMovement()
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];
        Dictionary<string, object> results = new();

        try
        {
            _logger.LogInformation("Testing all WASD movement keys");

            string[] directions = ["forward", "backward", "left", "right"];

            foreach (string direction in directions)
            {
                _addonData.UpdateData();
                PlayerStateSnapshot before = PlayerStateSnapshot.Capture(_playerReader, _bits);

                // Execute movement
                var response = await TestMovement(new MovementTestRequest
                {
                    Direction = direction,
                    DurationMs = 300,
                    ReturnToStart = false
                });

                // Extract result
                if (response is OkObjectResult okResult && okResult.Value is TestResult testResult)
                {
                    bool passed = testResult.Success && testResult.Checks.All(c => c.Passed);
                    checks.Add(TestHelpers.CreateBoolCheck(
                        $"{direction} movement",
                        passed,
                        passed ? $"✅ {direction}" : $"❌ {direction}"));

                    results[direction] = new
                    {
                        success = testResult.Success,
                        checksPassed = testResult.Checks.Count(c => c.Passed),
                        checksTotal = testResult.Checks.Count,
                        data = testResult.Data
                    };
                }

                // Wait between tests
                await Task.Delay(500);
            }

            sw.Stop();

            int passedCount = checks.Count(c => c.Passed);
            string message = passedCount == 4
                ? "✅ All WASD keys validated"
                : $"⚠️ {passedCount}/4 movement directions working";

            return Ok(TestResult.Pass(
                "WASD Movement Test",
                sw.Elapsed,
                checks,
                results,
                message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WASD test failed");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("WASD Movement Test", sw.Elapsed, ex));
        }
    }

    #endregion

    #region Phase 4: Combat Tests

    /// <summary>
    /// Request model for ability testing
    /// </summary>
    public class AbilityTestRequest
    {
        public string AbilityName { get; set; } = "Unknown";
        public string Key { get; set; } = "1";
        public int ExpectedEnergyCost { get; set; }
        public int ExpectedComboPoints { get; set; }
        public int WaitAfterCastMs { get; set; } = 1500; // Default GCD
    }

    /// <summary>
    /// Tests targeting mechanics by pressing Tab to acquire nearest enemy
    /// Validates target acquisition, health reading, and target properties
    /// </summary>
    [HttpPost("combat/target")]
    public async Task<IActionResult> TestTargeting()
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];

        try
        {
            _logger.LogInformation("Starting targeting test");

            // 1. Capture initial state (should have no target)
            _addonData.UpdateData();
            PlayerStateSnapshot before = PlayerStateSnapshot.Capture(_playerReader, _bits);

            checks.Add(TestHelpers.CreateCheck(
                "Initial state captured",
                "true",
                "true",
                $"HasTarget: {before.HasTarget}"));

            // 2. Press Tab to target nearest enemy
            _logger.LogInformation("Pressing Tab to target nearest enemy");
            _input.PressRandom(ConsoleKey.Tab, 50);

            // 3. Wait for target acquisition
            await Task.Delay(300);
            _addonData.UpdateData();

            // 4. Check if target acquired
            PlayerStateSnapshot after = PlayerStateSnapshot.Capture(_playerReader, _bits);

            bool targetAcquired = after.HasTarget;
            checks.Add(TestHelpers.CreateBoolCheck(
                "Target acquired",
                targetAcquired,
                targetAcquired 
                    ? $"Target ID: {after.TargetId}, Level: {after.TargetLevel}" 
                    : "No target found - ensure enemies are nearby"));

            if (targetAcquired)
            {
                // 5. Validate target properties
                bool targetAlive = after.TargetAlive;
                checks.Add(TestHelpers.CreateBoolCheck(
                    "Target is alive",
                    targetAlive,
                    targetAlive ? "Target alive" : "Target is dead"));

                bool targetHostile = after.TargetHostile;
                checks.Add(TestHelpers.CreateBoolCheck(
                    "Target is hostile",
                    targetHostile,
                    targetHostile ? "Enemy target" : "Friendly/neutral target"));

                bool hasHealth = after.TargetHealthMax > 0;
                checks.Add(TestHelpers.CreateBoolCheck(
                    "Target health readable",
                    hasHealth,
                    hasHealth 
                        ? $"HP: {after.TargetHealth}/{after.TargetHealthMax}" 
                        : "Target health unavailable"));
            }

            // 6. Build result data
            Dictionary<string, object> data = new()
            {
                ["hadTargetBefore"] = before.HasTarget,
                ["hasTargetAfter"] = after.HasTarget,
                ["targetId"] = after.TargetId,
                ["targetLevel"] = after.TargetLevel,
                ["targetHealth"] = after.TargetHealth,
                ["targetHealthMax"] = after.TargetHealthMax,
                ["targetAlive"] = after.TargetAlive,
                ["targetHostile"] = after.TargetHostile,
                ["targetClassification"] = after.TargetClassification.ToString(),
                ["beforeSnapshot"] = before,
                ["afterSnapshot"] = after
            };

            sw.Stop();

            string message = targetAcquired
                ? $"✅ Target acquired: Level {after.TargetLevel} {after.TargetClassification} ({after.TargetHealth}/{after.TargetHealthMax} HP)"
                : "⚠️ No target found - move closer to enemies";

            return Ok(TestResult.Pass(
                "Targeting Test",
                sw.Elapsed,
                checks,
                data,
                message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Targeting test failed");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("Targeting Test", sw.Elapsed, ex));
        }
    }

    /// <summary>
    /// Tests ability casting by using a specified ability key
    /// Validates energy cost, GCD, combo points, and damage dealt
    /// </summary>
    [HttpPost("combat/ability")]
    public async Task<IActionResult> TestAbility([FromBody] AbilityTestRequest request)
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];

        try
        {
            _logger.LogInformation("Starting ability test: {AbilityName} on key {Key}", 
                request.AbilityName, request.Key);

            // 1. Ensure we have a target
            _addonData.UpdateData();
            PlayerStateSnapshot before = PlayerStateSnapshot.Capture(_playerReader, _bits);

            if (!before.HasTarget || !before.TargetAlive)
            {
                sw.Stop();
                return BadRequest(TestResult.Fail(
                    "Ability Test",
                    sw.Elapsed,
                    [TestHelpers.CreateCheck("Has valid target", "true", "false", "No valid target - use /api/test/combat/target first")],
                    "❌ Test requires a valid living target",
                    new Dictionary<string, object>()));
            }

            checks.Add(TestHelpers.CreateCheck(
                "Has valid target",
                "true",
                "true",
                $"Target: Level {before.TargetLevel} ({before.TargetHealth}/{before.TargetHealthMax} HP)"));

            // 2. Parse ability key
            if (!ConsoleKey.TryParse(request.Key, out ConsoleKey abilityKey))
            {
                // Try numeric keys (1-9, 0)
                if (request.Key.Length == 1 && char.IsDigit(request.Key[0]))
                {
                    abilityKey = request.Key[0] switch
                    {
                        '1' => ConsoleKey.D1,
                        '2' => ConsoleKey.D2,
                        '3' => ConsoleKey.D3,
                        '4' => ConsoleKey.D4,
                        '5' => ConsoleKey.D5,
                        '6' => ConsoleKey.D6,
                        '7' => ConsoleKey.D7,
                        '8' => ConsoleKey.D8,
                        '9' => ConsoleKey.D9,
                        '0' => ConsoleKey.D0,
                        _ => ConsoleKey.None
                    };
                }
                else
                {
                    sw.Stop();
                    return BadRequest(TestResult.Fail(
                        "Ability Test",
                        sw.Elapsed,
                        [TestHelpers.CreateCheck("Valid key", request.Key, "Invalid", "Key must be 1-9, 0, or ConsoleKey name")],
                        $"❌ Invalid key: {request.Key}",
                        new Dictionary<string, object>()));
                }
            }

            // 3. Press ability key
            _logger.LogInformation("Casting {Ability} using key {Key}", request.AbilityName, abilityKey);
            _input.PressRandom(abilityKey, 50);

            // 4. Wait for GCD (1.5s) + animation
            await Task.Delay(request.WaitAfterCastMs);
            _addonData.UpdateData();

            // 5. Capture post-cast state
            PlayerStateSnapshot after = PlayerStateSnapshot.Capture(_playerReader, _bits);

            // 6. Analyze changes
            int energySpent = before.PowerCurrent - after.PowerCurrent;
            int comboPointsGained = after.ComboPoints - before.ComboPoints;
            int damageDealt = before.TargetHealth - after.TargetHealth;
            bool targetStillAlive = after.TargetAlive;

            // Energy check (for Rogues)
            if (request.ExpectedEnergyCost > 0)
            {
                bool energySpentCorrect = Math.Abs(energySpent - request.ExpectedEnergyCost) <= 5; // 5 energy tolerance
                checks.Add(TestHelpers.CreateCheck(
                    "Energy cost",
                    request.ExpectedEnergyCost.ToString(),
                    energySpent.ToString(),
                    energySpentCorrect 
                        ? $"Spent {energySpent} energy" 
                        : $"Expected {request.ExpectedEnergyCost}, actual {energySpent}"));
            }

            // Combo point check (for builders like Sinister Strike)
            if (request.ExpectedComboPoints > 0)
            {
                bool cpGained = comboPointsGained == request.ExpectedComboPoints;
                checks.Add(TestHelpers.CreateCheck(
                    "Combo points",
                    request.ExpectedComboPoints.ToString(),
                    comboPointsGained.ToString(),
                    cpGained 
                        ? $"Gained {comboPointsGained} CP (now {after.ComboPoints})" 
                        : $"Expected +{request.ExpectedComboPoints}, got +{comboPointsGained}"));
            }

            // Damage check
            bool dealtDamage = damageDealt > 0;
            checks.Add(TestHelpers.CreateBoolCheck(
                "Damage dealt",
                dealtDamage,
                dealtDamage 
                    ? $"Dealt {damageDealt} damage ({after.TargetHealth}/{after.TargetHealthMax} HP remaining)" 
                    : "No damage detected - ability may have missed or not cast"));

            // Combat state check
            bool inCombat = after.InCombat;
            checks.Add(TestHelpers.CreateBoolCheck(
                "Entered combat",
                inCombat,
                inCombat ? "In combat" : "Not in combat"));

            // 7. Build result data
            Dictionary<string, object> data = new()
            {
                ["abilityName"] = request.AbilityName,
                ["key"] = abilityKey.ToString(),
                ["energyBefore"] = before.PowerCurrent,
                ["energyAfter"] = after.PowerCurrent,
                ["energySpent"] = energySpent,
                ["comboPointsBefore"] = before.ComboPoints,
                ["comboPointsAfter"] = after.ComboPoints,
                ["comboPointsGained"] = comboPointsGained,
                ["targetHealthBefore"] = before.TargetHealth,
                ["targetHealthAfter"] = after.TargetHealth,
                ["damageDealt"] = damageDealt,
                ["inCombat"] = after.InCombat,
                ["targetAlive"] = targetStillAlive,
                ["beforeSnapshot"] = before,
                ["afterSnapshot"] = after
            };

            sw.Stop();

            string message = dealtDamage
                ? $"✅ {request.AbilityName} cast successfully: {damageDealt} damage, {energySpent} energy, +{comboPointsGained} CP"
                : $"⚠️ {request.AbilityName} used but no damage detected";

            return Ok(TestResult.Pass(
                $"Ability Test ({request.AbilityName})",
                sw.Elapsed,
                checks,
                data,
                message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ability test failed");
            sw.Stop();
            return StatusCode(500, TestResult.FromException("Ability Test", sw.Elapsed, ex));
        }
    }

    /// <summary>
    /// Request model for combat cycle testing
    /// </summary>
    public class CombatCycleRequest
    {
        public bool UseProfileAbilities { get; set; }
        public string[] FallbackAbilities { get; set; } = ["2"]; // Default: Sinister Strike on key 2
        public int MaxCombatDurationMs { get; set; } = 60000; // 60 seconds max
        public int TargetTimeoutMs { get; set; } = 10000; // 10 seconds to find target
        public int ApproachTimeoutMs { get; set; } = 15000; // 15 seconds to reach target
        public bool EnableClickToMove { get; set; } = true; // Redundancy option
    }

    /// <summary>
    /// POST /api/test/combat/cycle
    /// Full combat cycle test: Target → Approach → Combat → Kill (→ Loot optional)
    /// Uses actual bot systems for realistic validation
    /// </summary>
    [HttpPost("combat/cycle")]
    public async Task<IActionResult> TestCombatCycle([FromBody] CombatCycleRequest? request = null)
    {
        request ??= new CombatCycleRequest();

        Stopwatch sw = Stopwatch.StartNew();
        List<TestCheck> checks = [];
        List<string> events = []; // Timeline of events

        try
        {
            _logger.LogInformation("Starting full combat cycle test");

            // ========================
            // Phase 1: PRE-COMBAT STATE
            // ========================
            _addonData.UpdateData();
            PlayerStateSnapshot preSnapshot = PlayerStateSnapshot.Capture(_playerReader, _bits);

            checks.Add(TestHelpers.CreateCheck(
                "Pre-combat state",
                "Ready",
                "Ready",
                $"HP: {preSnapshot.HealthPercent}%, Combat: {preSnapshot.InCombat}"));

            events.Add($"[{sw.ElapsedMilliseconds}ms] Pre-combat: Lv{preSnapshot.Level} at ({preSnapshot.MapX:F1}, {preSnapshot.MapY:F1})");

            // ========================
            // Phase 2: TARGET ACQUISITION
            // ========================
            bool targetAcquired = await AcquireTarget(request.TargetTimeoutMs, checks, events, sw);

            if (!targetAcquired)
            {
                sw.Stop();
                return Ok(TestResult.Fail(
                    "Combat Cycle Test",
                    sw.Elapsed,
                    checks,
                    "⚠️ Failed to acquire target - ensure enemies are nearby",
                    new Dictionary<string, object>
                    {
                        ["events"] = events,
                        ["phase"] = "targeting",
                        ["preSnapshot"] = preSnapshot
                    }));
            }

            // ========================
            // Phase 3: APPROACH TARGET
            // ========================
            bool inRange = await ApproachTarget(request.ApproachTimeoutMs, request.EnableClickToMove, checks, events, sw);

            if (!inRange)
            {
                sw.Stop();
                return Ok(TestResult.Fail(
                    "Combat Cycle Test",
                    sw.Elapsed,
                    checks,
                    "⚠️ Failed to reach target - may be stuck or target unreachable",
                    new Dictionary<string, object>
                    {
                        ["events"] = events,
                        ["phase"] = "approach",
                        ["preSnapshot"] = preSnapshot
                    }));
            }

            // ========================
            // Phase 4: COMBAT EXECUTION
            // ========================
            CombatResult combatResult = await ExecuteCombat(
                request.MaxCombatDurationMs,
                request.UseProfileAbilities,
                request.FallbackAbilities,
                checks,
                events,
                sw);

            // ========================
            // Phase 5: POST-COMBAT STATE
            // ========================
            _addonData.UpdateData();
            PlayerStateSnapshot postSnapshot = PlayerStateSnapshot.Capture(_playerReader, _bits);

            events.Add($"[{sw.ElapsedMilliseconds}ms] Post-combat: HP {postSnapshot.HealthPercent}%, Combat: {postSnapshot.InCombat}");

            // Build comprehensive result data
            Dictionary<string, object> data = new()
            {
                ["preSnapshot"] = preSnapshot,
                ["postSnapshot"] = postSnapshot,
                ["events"] = events,
                ["targeting"] = new
                {
                    success = targetAcquired,
                    durationMs = combatResult.TargetingDurationMs
                },
                ["approach"] = new
                {
                    success = inRange,
                    durationMs = combatResult.ApproachDurationMs
                },
                ["combat"] = new
                {
                    success = combatResult.Success,
                    durationMs = combatResult.CombatDurationMs,
                    abilitiesCast = combatResult.AbilitiesCast,
                    damageDealt = combatResult.DamageDealt,
                    killedTarget = combatResult.KilledTarget
                },
                ["healthLost"] = preSnapshot.Health - postSnapshot.Health,
                ["energyUsed"] = combatResult.EnergyUsed,
                ["totalDurationMs"] = sw.ElapsedMilliseconds
            };

            sw.Stop();

            bool success = targetAcquired && inRange && combatResult.Success;
            string message = success
                ? $"✅ Combat cycle complete: {combatResult.DamageDealt} damage, {combatResult.AbilitiesCast.Count} abilities, {sw.ElapsedMilliseconds}ms total"
                : $"⚠️ Combat cycle incomplete - check events for details";

            return Ok(TestResult.Pass(
                "Combat Cycle Test",
                sw.Elapsed,
                checks,
                data,
                message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Combat cycle test failed");
            events.Add($"[{sw.ElapsedMilliseconds}ms] ERROR: {ex.Message}");
            sw.Stop();

            return StatusCode(500, TestResult.FromException("Combat Cycle Test", sw.Elapsed, ex));
        }
    }

    // ========================
    // HELPER METHODS FOR COMBAT CYCLE
    // ========================

    private async Task<bool> AcquireTarget(int timeoutMs, List<TestCheck> checks, List<string> events, Stopwatch sw)
    {
        events.Add($"[{sw.ElapsedMilliseconds}ms] → Targeting phase started");

        Stopwatch phaseTimer = Stopwatch.StartNew();
        int attempts = 0;

        while (phaseTimer.ElapsedMilliseconds < timeoutMs)
        {
            _addonData.UpdateData();

            // Check if we already have a valid target
            if (_bits.Target() && _bits.Target_Alive() && _bits.Target_Hostile())
            {
                events.Add($"[{sw.ElapsedMilliseconds}ms] ✓ Target acquired (attempt {attempts + 1}): Level {_playerReader.TargetLevel}, HP {_playerReader.TargetHealth()}/{_playerReader.TargetMaxHealth()}");

                checks.Add(TestHelpers.CreateBoolCheck(
                    "Target acquired",
                    true,
                    $"Level {_playerReader.TargetLevel} enemy, {_playerReader.TargetHealth()}/{_playerReader.TargetMaxHealth()} HP"));

                return true;
            }

            // Attempt to target nearest enemy (Tab key)
            attempts++;
            events.Add($"[{sw.ElapsedMilliseconds}ms]   Pressing Tab (attempt {attempts})");

            _input.PressRandom(ConsoleKey.Tab, 50);
            await Task.Delay(300); // Wait for target acquisition
            _addonData.UpdateData();

            // If still no target after multiple attempts, try turning
            if (attempts >= 3 && attempts % 3 == 0)
            {
                events.Add($"[{sw.ElapsedMilliseconds}ms]   Turning to find targets");
                ConsoleKey turnKey = attempts % 2 == 0 ? _input.TurnLeftKey : _input.TurnRightKey;
                _input.SetKeyState(turnKey, true, false);
                await Task.Delay(500);
                _input.SetKeyState(turnKey, false, false);
            }

            await Task.Delay(200);
        }

        events.Add($"[{sw.ElapsedMilliseconds}ms] ✗ Target acquisition timeout ({phaseTimer.ElapsedMilliseconds}ms)");
        checks.Add(TestHelpers.CreateBoolCheck(
            "Target acquired",
            false,
            $"Timeout after {attempts} attempts"));

        return false;
    }

    private async Task<bool> ApproachTarget(int timeoutMs, bool enableClickToMove, List<TestCheck> checks, List<string> events, Stopwatch sw)
    {
        events.Add($"[{sw.ElapsedMilliseconds}ms] → Approach phase started");

        Stopwatch phaseTimer = Stopwatch.StartNew();
        int interactAttempts = 0;
        bool movingForward = false;

        while (phaseTimer.ElapsedMilliseconds < timeoutMs)
        {
            _addonData.UpdateData();

            // Check if in melee range
            if (_playerReader.IsInMeleeRange())
            {
                // Stop forward movement if active
                if (movingForward)
                {
                    _input.SetKeyState(ConsoleKey.W, false, false);
                    movingForward = false;
                }

                events.Add($"[{sw.ElapsedMilliseconds}ms] ✓ In melee range ({_playerReader.MinRange():F1} yards)");

                checks.Add(TestHelpers.CreateBoolCheck(
                    "Reached target",
                    true,
                    $"In melee range ({_playerReader.MinRange():F1} yards)"));

                return true;
            }

            // Check for lost target
            if (!_bits.Target() || !_bits.Target_Alive())
            {
                if (movingForward)
                {
                    _input.SetKeyState(ConsoleKey.W, false, false);
                    movingForward = false;
                }

                events.Add($"[{sw.ElapsedMilliseconds}ms] ✗ Target lost during approach");
                checks.Add(TestHelpers.CreateBoolCheck(
                    "Reached target",
                    false,
                    "Target lost"));

                return false;
            }

            // Strategy: Aggressive forward movement with periodic interact
            if (!movingForward)
            {
                events.Add($"[{sw.ElapsedMilliseconds}ms]   Starting forward movement (W key)");
                _input.SetKeyState(ConsoleKey.W, true, false);
                movingForward = true;
            }

            // Periodically press interact to face target and auto-attack
            if (interactAttempts == 0 || phaseTimer.ElapsedMilliseconds > interactAttempts * 1000)
            {
                interactAttempts++;
                events.Add($"[{sw.ElapsedMilliseconds}ms]   Interact (face target) - attempt {interactAttempts}");
                _input.PressRandom(ConsoleKey.T, 10); // Face target and interact
                await Task.Delay(100);
            }

            await Task.Delay(100);
        }

        // Stop forward movement on timeout
        if (movingForward)
        {
            _input.SetKeyState(ConsoleKey.W, false, false);
        }

        events.Add($"[{sw.ElapsedMilliseconds}ms] ✗ Approach timeout ({phaseTimer.ElapsedMilliseconds}ms, {_playerReader.MinRange():F1} yards away)");
        checks.Add(TestHelpers.CreateBoolCheck(
            "Reached target",
            false,
            $"Timeout at {_playerReader.MinRange():F1} yards"));

        return false;
    }

    private async Task<CombatResult> ExecuteCombat(
        int maxDurationMs,
        bool useProfile,
        string[] fallbackAbilities,
        List<TestCheck> checks,
        List<string> events,
        Stopwatch sw)
    {
        events.Add($"[{sw.ElapsedMilliseconds}ms] → Combat phase started");

        Stopwatch combatTimer = Stopwatch.StartNew();
        CombatResult result = new();

        int initialTargetHealth = _playerReader.TargetHealth();
        int initialEnergy = _playerReader.PTCurrent();

        while (combatTimer.ElapsedMilliseconds < maxDurationMs)
        {
            _addonData.UpdateData();

            // Check if target is dead
            if (!_bits.Target() || _bits.Target_Dead())
            {
                events.Add($"[{sw.ElapsedMilliseconds}ms] ✓ Target killed");
                result.KilledTarget = true;
                result.Success = true;
                break;
            }

            // Check if we're still alive
            if (_bits.Dead())
            {
                events.Add($"[{sw.ElapsedMilliseconds}ms] ✗ Player died");
                result.Success = false;
                break;
            }

            // Combat rotation - use fallback abilities (hardcoded approach)
            foreach (string abilityKey in fallbackAbilities)
            {
                ConsoleKey key = ParseAbilityKey(abilityKey);

                if (key != ConsoleKey.None)
                {
                    // Basic resource check (for Rogues - energy 45+)
                    if (_playerReader.PTCurrent() >= 45 || abilityKey == "Auto")
                    {
                        events.Add($"[{sw.ElapsedMilliseconds}ms]   Cast ability: {abilityKey}");
                        result.AbilitiesCast.Add(abilityKey);

                        _input.PressRandom(key, 50);

                        // Wait for GCD (1.5s)
                        await Task.Delay(1500);
                        _addonData.UpdateData();

                        break; // Only cast one ability per iteration
                    }
                }
            }

            // Track damage dealt
            int currentTargetHealth = _playerReader.TargetHealth();
            if (currentTargetHealth < initialTargetHealth)
            {
                result.DamageDealt = initialTargetHealth - currentTargetHealth;
            }

            await Task.Delay(100);
        }

        result.CombatDurationMs = (int)combatTimer.ElapsedMilliseconds;
        result.EnergyUsed = initialEnergy - _playerReader.PTCurrent();

        if (result.Success)
        {
            events.Add($"[{sw.ElapsedMilliseconds}ms] ✓ Combat successful: {result.DamageDealt} damage, {result.AbilitiesCast.Count} abilities");
            checks.Add(TestHelpers.CreateBoolCheck(
                "Combat successful",
                true,
                $"{result.DamageDealt} damage dealt"));
        }
        else if (combatTimer.ElapsedMilliseconds >= maxDurationMs)
        {
            events.Add($"[{sw.ElapsedMilliseconds}ms] ✗ Combat timeout");
            checks.Add(TestHelpers.CreateBoolCheck(
                "Combat successful",
                false,
                "Timeout"));
        }

        return result;
    }

    private static ConsoleKey ParseAbilityKey(string key)
    {
        return key switch
        {
            "1" => ConsoleKey.D1,
            "2" => ConsoleKey.D2,
            "3" => ConsoleKey.D3,
            "4" => ConsoleKey.D4,
            "5" => ConsoleKey.D5,
            "6" => ConsoleKey.D6,
            "7" => ConsoleKey.D7,
            "8" => ConsoleKey.D8,
            "9" => ConsoleKey.D9,
            "0" => ConsoleKey.D0,
            "Auto" => ConsoleKey.T, // INTERACTTARGET for auto-attack
            _ => ConsoleKey.None
        };
    }

    private class CombatResult
    {
        public bool Success { get; set; }
        public int TargetingDurationMs { get; set; }
        public int ApproachDurationMs { get; set; }
        public int CombatDurationMs { get; set; }
        public List<string> AbilitiesCast { get; set; } = [];
        public int DamageDealt { get; set; }
        public int EnergyUsed { get; set; }
        public bool KilledTarget { get; set; }
    }

    #endregion
}
