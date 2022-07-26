using System.Collections.Specialized;

namespace Core
{
    public class AddonBits
    {
        private readonly int cell1;
        private readonly int cell2;

        private BitVector32 v1;
        private BitVector32 v2;

        public AddonBits(int cell1, int cell2)
        {
            this.cell1 = cell1;
            this.cell2 = cell2;
        }

        public void Update(AddonDataProvider reader)
        {
            v1 = new(reader.GetInt(cell1));
            v2 = new(reader.GetInt(cell2));
        }

        // -- value1 based flags
        public bool TargetInCombat() => v1[Mask._0];
        public bool TargetIsDead() => v1[Mask._1];
        public bool DeadStatus() => v1[Mask._2];
        public bool TalentPoints() => v1[Mask._3];
        public bool TargetInTradeRange() => v1[Mask._4];
        public bool TargetCanBeHostile() => v1[Mask._5];
        public bool HasPet() => v1[Mask._6];
        public bool MainHandEnchant_Active() => v1[Mask._7];
        public bool OffHandEnchant_Active() => v1[Mask._8];
        public bool ItemsAreBroken() => v1[Mask._9];
        public bool IsFlying() => v1[Mask._10];
        public bool IsSwimming() => v1[Mask._11];
        public bool PetHappy() => v1[Mask._12];
        public bool HasAmmo() => v1[Mask._13];
        public bool PlayerInCombat() => v1[Mask._14];
        public bool TargetOfTargetIsPlayerOrPet() => v1[Mask._15];
        public bool SpellOn_AutoShot() => v1[Mask._16];
        public bool HasTarget() => v1[Mask._17];
        public bool IsMounted() => v1[Mask._18];
        public bool SpellOn_Shoot() => v1[Mask._19];
        public bool SpellOn_AutoAttack() => v1[Mask._20];
        public bool TargetIsNormal() => v1[Mask._21];
        public bool IsTagged() => v1[Mask._22];
        public bool IsFalling() => v1[Mask._23];

        // -- value2 based flags
        public bool IsDrowning() => v2[Mask._0];

        public bool IsCorpseInRange() => v2[Mask._1];

        public bool IsIndoors() => v2[Mask._2];

        public bool HasFocus() => v2[Mask._3];

        public bool FocusInCombat() => v2[Mask._4];

        public bool FocusHasTarget() => v2[Mask._5];

        public bool FocusTargetInCombat() => v2[Mask._6];

        public bool FocusTargetCanBeHostile() => v2[Mask._7];

        public bool FocusTargetInTradeRange() => v2[Mask._8];

        public bool PetTargetIsDead() => v2[Mask._9];
    }
}