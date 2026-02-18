using Core.FeatureFlags;

using SixLabors.ImageSharp;

using System;

namespace Core.Humanization;

public static class HumanizedMousePath
{
    public static int BuildPath(
        Point start,
        Point end,
        int stepsPerMovement,
        double curveIntensity,
        int microJitterPixels,
        double overshootProbability,
        Span<Point> buffer)
    {
        return BuildPathInternal(
            start,
            end,
            stepsPerMovement,
            curveIntensity,
            microJitterPixels,
            overshootProbability,
            buffer);
    }

    public static int BuildPath(Point start, Point end, HumanizationMouseMovementOptions options, Span<Point> buffer)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        return BuildPathInternal(
            start,
            end,
            options.StepsPerMovement,
            options.CurveIntensity,
            options.MicroJitterPixels,
            options.OvershootProbability,
            buffer);
    }

    private static int BuildPathInternal(
        Point start,
        Point end,
        int stepsPerMovement,
        double curveIntensity,
        int microJitterPixels,
        double overshootProbability,
        Span<Point> buffer)
    {
        if (buffer.Length <= 0)
        {
            return 0;
        }

        if (buffer.Length == 1)
        {
            buffer[0] = end;
            return 1;
        }

        int desiredSteps = stepsPerMovement <= 0 ? 15 : stepsPerMovement;
        int steps = Math.Clamp(desiredSteps + 1, 2, buffer.Length);

        int jitter = Math.Max(0, microJitterPixels);
        double curve = curveIntensity;
        if (double.IsNaN(curve) || double.IsInfinity(curve))
        {
            curve = 0.30;
        }
        curve = Math.Clamp(curve, 0.05, 0.80);

        bool overshoot = overshootProbability > 0 &&
                         Random.Shared.NextDouble() < Math.Clamp(overshootProbability, 0, 1);

        Point bezierEnd = end;
        if (overshoot)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double dist = Math.Sqrt((dx * dx) + (dy * dy));
            if (dist > 1)
            {
                double nx = dx / dist;
                double ny = dy / dist;
                int overshootPixels = (int)Math.Clamp(dist * 0.05, 3, 20);
                bezierEnd = new Point(
                    end.X + (int)Math.Round(nx * overshootPixels),
                    end.Y + (int)Math.Round(ny * overshootPixels));
            }
            else
            {
                overshoot = false;
            }
        }

        (Point c1, Point c2) = BuildControlPoints(start, bezierEnd, curve);

        int bezierPoints = overshoot ? steps - 1 : steps;
        for (int i = 0; i < bezierPoints; i++)
        {
            double t = (double)i / (bezierPoints - 1);
            double eased = EaseInOutQuad(t);

            double u = 1 - eased;
            double tt = eased * eased;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * eased;

            double x =
                (uuu * start.X) +
                (3 * uu * eased * c1.X) +
                (3 * u * tt * c2.X) +
                (ttt * bezierEnd.X);

            double y =
                (uuu * start.Y) +
                (3 * uu * eased * c1.Y) +
                (3 * u * tt * c2.Y) +
                (ttt * bezierEnd.Y);

            if (jitter > 0 && i != 0 && i != bezierPoints - 1)
            {
                x += Random.Shared.Next(-jitter, jitter + 1);
                y += Random.Shared.Next(-jitter, jitter + 1);
            }

            buffer[i] = new Point((int)Math.Round(x), (int)Math.Round(y));
        }

        if (overshoot)
        {
            buffer[steps - 1] = end;
        }
        else
        {
            buffer[steps - 1] = end;
        }

        return steps;
    }

    private static (Point c1, Point c2) BuildControlPoints(Point start, Point end, double curveIntensity)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double dist = Math.Sqrt((dx * dx) + (dy * dy));

        if (dist < 1)
        {
            return (start, end);
        }

        double nx = dx / dist;
        double ny = dy / dist;

        // Perpendicular direction for curvature
        double px = -ny;
        double py = nx;

        double deviation = Math.Clamp(dist * curveIntensity, 10, 100);
        double dev1 = (Random.Shared.NextDouble() * 2 - 1) * deviation;
        double dev2 = (Random.Shared.NextDouble() * 2 - 1) * deviation;

        Point c1 = new(
            start.X + (int)Math.Round(dx * 0.30 + (px * dev1)),
            start.Y + (int)Math.Round(dy * 0.30 + (py * dev1)));

        Point c2 = new(
            start.X + (int)Math.Round(dx * 0.70 + (px * dev2)),
            start.Y + (int)Math.Round(dy * 0.70 + (py * dev2)));

        return (c1, c2);
    }

    private static double EaseInOutQuad(double t)
    {
        if (t < 0.5)
        {
            return 2 * t * t;
        }

        return 1 - (Math.Pow(-2 * t + 2, 2) / 2);
    }
}
