﻿namespace EHR
{
    internal class VanillaRole : RoleBase
    {
        public static bool On;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
        }
    }
}