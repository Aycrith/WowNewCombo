using System.Numerics;

namespace Core
{
    public static class PointEstimator
    {
        public const float YARD_TO_COORD = 0.035921f;

        public static Vector3 GetPoint(Vector3 map, float wowRad, float rangeYard)
        {
            //player direction
            //0.00061

            //player location
            //37.4017,44.4587

            //NPC
            //37.4016,44.2791

            //~5yard Distance
            //44.4587 - 44.2791 = 0.1796

            //~1yard Distance
            //0.1796 / 5 = 0.03592

            float range = rangeYard * YARD_TO_COORD;
            Vector2 dir = DirectionCalculator.ToNormalRadian(wowRad);

            return new Vector3(map.X + (range * dir.X), map.Y + (range * dir.Y), map.Z);
        }
    }
}
