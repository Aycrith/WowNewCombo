namespace Core
{
    public class AddonBits
    {
        private readonly AddonDataProvider reader;
        private readonly int cell1;
        private readonly int cell2;

        private readonly BitStatus v1;
        private readonly BitStatus v2;

        public AddonBits(AddonDataProvider reader, int cell1, int cell2)
        {
            this.reader = reader;
            this.cell1 = cell1;
            this.cell2 = cell2;

            v1 = new(reader.GetInt(cell1));
            v2 = new(reader.GetInt(cell2));
        }

        public void SetDirty()
        {
            v1.Update(reader.GetInt(cell1));
            v2.Update(reader.GetInt(cell2));
        }

        // -- value1 based flags
        public bool TargetInCombat() => v1.IsBitSet(0);
        public bool TargetIsDead() => v1.IsBitSet(1);
        public bool DeadStatus() => v1.IsBitSet(2);
        public bool TalentPoints() => v1.IsBitSet(3);
        public bool IsInDeadZoneRange() => v1.IsBitSet(4);
        public bool TargetCanBeHostile() => v1.IsBitSet(5);
        public bool HasPet() => v1.IsBitSet(6);
        public bool MainHandEnchant_Active() => v1.IsBitSet(7);
        public bool OffHandEnchant_Active() => v1.IsBitSet(8);
        public bool ItemsAreBroken() => v1.IsBitSet(9);
        public bool IsFlying() => v1.IsBitSet(10);
        public bool IsSwimming() => v1.IsBitSet(11);
        public bool PetHappy() => v1.IsBitSet(12);
        public bool HasAmmo() => v1.IsBitSet(13);
        public bool PlayerInCombat() => v1.IsBitSet(14);
        public bool TargetOfTargetIsPlayerOrPet() => v1.IsBitSet(15);
        public bool SpellOn_AutoShot() => v1.IsBitSet(16);
        public bool HasTarget() => v1.IsBitSet(17);
        public bool IsMounted() => v1.IsBitSet(18);
        public bool SpellOn_Shoot() => v1.IsBitSet(19);
        public bool SpellOn_AutoAttack() => v1.IsBitSet(20);
        public bool TargetIsNormal() => v1.IsBitSet(21);
        public bool IsTagged() => v1.IsBitSet(22);
        public bool IsFalling() => v1.IsBitSet(23);

        // -- value2 based flags
        public bool IsDrowning() => v2.IsBitSet(0);

        public bool IsCorpseInRange() => v2.IsBitSet(1);

        public bool IsIndoors() => v2.IsBitSet(2);

        public bool HasFocus() => v2.IsBitSet(3);

        public bool FocusInCombat() => v2.IsBitSet(4);

        public bool FocusHasTarget() => v2.IsBitSet(5);

        public bool FocusTargetInCombat() => v2.IsBitSet(6);

        public bool FocusTargetCanBeHostile() => v2.IsBitSet(7);

        public bool FocusTargetInTradeRange() => v2.IsBitSet(8);
    }
}