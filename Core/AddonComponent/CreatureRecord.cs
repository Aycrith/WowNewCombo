using System;

namespace Core
{
    public struct CreatureRecord
    {
        public int Guid { get; init; }
        public float HealthPercent { get; set; }
        public DateTime LastEvent { get; set; }

        public override string ToString()
        {
            return $"guid: {Guid} | hp: {HealthPercent}";
        }

        public override bool Equals(object? obj)
        {
            return obj is CreatureRecord other && other.Guid == Guid;
        }

        public override int GetHashCode()
        {
            return Guid;
        }

        public static bool operator ==(CreatureRecord left, CreatureRecord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CreatureRecord left, CreatureRecord right)
        {
            return !(left == right);
        }
    }
}
