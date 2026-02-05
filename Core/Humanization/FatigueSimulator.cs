using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

using System;

namespace Core.Humanization;

public sealed class FatigueSimulator
{
    private readonly ILogger<FatigueSimulator> logger;
    private readonly TimeProvider timeProvider;

    private readonly object gate = new();

    private HumanizationFatigueOptions options = new();
    private bool enabled;

    private DateTimeOffset sessionStartUtc;
    private TimeSpan recoveredPlayTime;

    private DateTimeOffset lastBreakUtc;
    private DateTimeOffset nextBreakDueUtc;

    private bool isOnBreak;
    private DateTimeOffset breakStartUtc;
    private DateTimeOffset breakEndUtc;

    public FatigueSimulator(
        ILogger<FatigueSimulator> logger,
        TimeProvider timeProvider)
    {
        this.logger = logger;
        this.timeProvider = timeProvider;

        DateTimeOffset now = timeProvider.GetUtcNow();
        sessionStartUtc = now;
        lastBreakUtc = now;
        nextBreakDueUtc = now;
    }

    public void ApplyOptions(HumanizationFatigueOptions options, bool enabled)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        lock (gate)
        {
            this.options = options;
            this.enabled = enabled && options.Enabled;

            if (!this.enabled)
            {
                isOnBreak = false;
                breakEndUtc = default;
            }

            EnsureNextBreakScheduled(timeProvider.GetUtcNow());
        }
    }

    public double FatigueMultiplier
    {
        get
        {
            lock (gate)
            {
                if (!enabled)
                {
                    return 1.0;
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                TimeSpan elapsed = now - sessionStartUtc;
                TimeSpan effective = elapsed - recoveredPlayTime;
                if (effective < TimeSpan.Zero)
                {
                    effective = TimeSpan.Zero;
                }

                double hours = effective.TotalHours;
                double multiplier = 1.0 + (hours * options.FatigueRatePerHour);
                double max = options.MaxFatigueMultiplier <= 1.0 ? 1.5 : options.MaxFatigueMultiplier;
                return Math.Min(multiplier, max);
            }
        }
    }

    public TimeSpan SessionDuration => timeProvider.GetUtcNow() - sessionStartUtc;

    public TimeSpan TimeSinceLastBreak => timeProvider.GetUtcNow() - lastBreakUtc;

    public bool IsOnBreak
    {
        get
        {
            lock (gate)
            {
                if (!isOnBreak)
                {
                    return false;
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                if (now >= breakEndUtc)
                {
                    EndBreak_NoLock(now);
                    return false;
                }

                return true;
            }
        }
    }

    public TimeSpan RemainingBreakTime
    {
        get
        {
            lock (gate)
            {
                if (!isOnBreak)
                {
                    return TimeSpan.Zero;
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                if (now >= breakEndUtc)
                {
                    EndBreak_NoLock(now);
                    return TimeSpan.Zero;
                }

                return breakEndUtc - now;
            }
        }
    }

    public bool IsBreakDue()
    {
        lock (gate)
        {
            if (!enabled || isOnBreak)
            {
                return false;
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            EnsureNextBreakScheduled(now);
            return now >= nextBreakDueUtc;
        }
    }

    public TimeSpan StartBreak()
    {
        lock (gate)
        {
            if (!enabled)
            {
                return TimeSpan.Zero;
            }

            if (isOnBreak)
            {
                return RemainingBreakTime;
            }

            DateTimeOffset now = timeProvider.GetUtcNow();

            double min = Math.Max(0.1, options.BreakDurationMinMinutes);
            double max = Math.Max(min, options.BreakDurationMaxMinutes);
            double fatigueBonus = (FatigueMultiplier - 1.0) * (max - min);

            double durationMinutes = min + (Random.Shared.NextDouble() * (max - min)) + fatigueBonus;
            durationMinutes = Math.Min(durationMinutes, max * 1.5);

            TimeSpan duration = TimeSpan.FromMinutes(durationMinutes);
            isOnBreak = true;
            breakStartUtc = now;
            breakEndUtc = now + duration;

            logger.LogInformation(
                "[FatigueSimulator ] Starting break for {Minutes:F1} min (fatigue={Fatigue:P0})",
                duration.TotalMinutes,
                FatigueMultiplier - 1.0);

            return duration;
        }
    }

    public void EndBreak()
    {
        lock (gate)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (!isOnBreak)
            {
                EnsureNextBreakScheduled(now);
                return;
            }

            EndBreak_NoLock(now);
        }
    }

    public void ResetSession()
    {
        lock (gate)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            sessionStartUtc = now;
            lastBreakUtc = now;
            recoveredPlayTime = TimeSpan.Zero;
            isOnBreak = false;
            breakStartUtc = default;
            breakEndUtc = default;
            nextBreakDueUtc = default;

            EnsureNextBreakScheduled(now);
        }

        logger.LogInformation("[FatigueSimulator ] Session reset");
    }

    private void EnsureNextBreakScheduled(DateTimeOffset now)
    {
        if (!enabled)
        {
            nextBreakDueUtc = default;
            return;
        }

        if (nextBreakDueUtc != default && nextBreakDueUtc > lastBreakUtc)
        {
            return;
        }

        int intervalMin = options.BreakIntervalMinutes <= 0 ? 45 : options.BreakIntervalMinutes;
        double jitter = 0.90 + (Random.Shared.NextDouble() * 0.20);
        TimeSpan interval = TimeSpan.FromMinutes(intervalMin * jitter);
        nextBreakDueUtc = lastBreakUtc + interval;

        if (nextBreakDueUtc <= now)
        {
            nextBreakDueUtc = now + TimeSpan.FromMinutes(Math.Max(5, intervalMin / 3));
        }
    }

    private void EndBreak_NoLock(DateTimeOffset now)
    {
        TimeSpan totalBreakDuration = now - breakStartUtc;
        if (totalBreakDuration < TimeSpan.Zero)
        {
            totalBreakDuration = TimeSpan.Zero;
        }

        // Recover ~30% of the play time accumulated since the previous break ended.
        if (breakStartUtc > lastBreakUtc)
        {
            recoveredPlayTime += (breakStartUtc - lastBreakUtc) * 0.30;
        }

        isOnBreak = false;
        breakStartUtc = default;
        breakEndUtc = default;
        lastBreakUtc = now;

        nextBreakDueUtc = default;
        EnsureNextBreakScheduled(now);

        logger.LogInformation(
            "[FatigueSimulator ] Break ended (duration={Seconds:F0}s). Next due at {Next:u}",
            totalBreakDuration.TotalSeconds,
            nextBreakDueUtc);
    }
}
