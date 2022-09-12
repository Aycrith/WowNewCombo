using System.Collections.Specialized;

namespace Core
{
    public sealed class AddonBits
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

        public void Update(IAddonDataProvider reader)
        {
            v1 = new(reader.GetInt(cell1));
            v2 = new(reader.GetInt(cell2));
        }

        // -- value1 based flags
        public bool TargetInCombat() => v1[Mask._0];
        public bool TargetIsDead() => v1[Mask._1];
        public bool TargetIsNotDead() => !v1[Mask._1];
        public bool IsDead() => v1[Mask._2];
        public bool HasTalentPoint() => v1[Mask._3];
        public bool HasMouseOver() => v1[Mask._4];
        public bool TargetCanBeHostile() => v1[Mask._5];
        public bool HasPet() => v1[Mask._6];
        public bool HasMainHandTempEnchant() => v1[Mask._7];
        public bool HasOffHandTempEnchant() => v1[Mask._8];
        public bool ItemsAreBroken() => v1[Mask._9];
        public bool PlayerOnTaxi() => v1[Mask._10];
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
        public bool TargetIsPlayer() => v1[Mask._21];
        public bool TargetIsTagged() => v1[Mask._22];
        public bool IsFalling() => v1[Mask._23];

        // -- value2 based flags
        public bool IsDrowning() => v2[Mask._0];
        public bool CorpseInRange() => v2[Mask._1];
        public bool IsIndoors() => v2[Mask._2];
        public bool HasFocus() => v2[Mask._3];
        public bool FocusInCombat() => v2[Mask._4];
        public bool FocusHasTarget() => v2[Mask._5];
        public bool FocusTargetInCombat() => v2[Mask._6];
        public bool FocusTargetCanBeHostile() => v2[Mask._7];
        public bool MouseOverIsDead() => v2[Mask._8];
        public bool PetTargetIsDead() => v2[Mask._9];
        public bool IsStealthed() => v2[Mask._10];
        public bool TargetIsTrivial() => v2[Mask._11];
        public bool TargetIsNotTrivial() => !v2[Mask._11];
        public bool MouseOverIsTrivial() => v2[Mask._12];
        public bool MouseOverIsNotTrivial() => !v2[Mask._12];
        public bool MouseOverIsTagged() => v2[Mask._13];
        public bool MouseOverCanBeHostile() => v2[Mask._14];
        public bool MouseOverIsPlayer() => v2[Mask._15];
        public bool MouseOverTargetIsPlayerOrPet() => v2[Mask._16];
        public bool MouseOverPlayerControlled() => v2[Mask._17];
        public bool TargetIsPlayerControlled() => v2[Mask._18];
    }
}