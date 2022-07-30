namespace Core
{
    public readonly struct AuraCount
    {
        public int Hash { get; }
        public int PlayerDebuff { get; }
        public int PlayerBuff { get; }
        public int TargetDebuff { get; }
        public int TargetBuff { get; }

        public AuraCount(IAddonDataProvider reader, int cell)
        {
            Hash = TargetBuff = reader.GetInt(cell);

            // formula
            // playerDebuffCount * 1000000 + playerBuffCount * 10000 + targetDebuffCount * 100 + targetBuffCount

            PlayerDebuff = (int)(TargetBuff / 1000000f);
            TargetBuff -= 1000000 * PlayerDebuff;

            PlayerBuff = (int)(TargetBuff / 10000f);
            TargetBuff -= 10000 * PlayerBuff;

            TargetDebuff = (int)(TargetBuff / 100f);
            TargetBuff -= 100 * TargetDebuff;
        }

        public override string ToString()
        {
            return $"pb: {PlayerBuff} | pd: {PlayerDebuff} | tb: {TargetBuff} | td: {TargetDebuff}";
        }
    }
}
