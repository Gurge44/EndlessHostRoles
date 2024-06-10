namespace EHR.GameMode.HideAndSeekRoles
{
    internal class Seeker : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem Vision;
        public static OptionItem Speed;
        public static OptionItem CanVent;
        public static OptionItem BlindTime;

        public override bool IsEnable => On;
        public Team Team => Team.Impostor;
        public int Chance => 100;
        public int Count => HnSManager.SeekerNum;
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            new TextOptionItem(69_211_299, "Seeker", TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetHeader(true)
                .SetColor(new(255, 25, 25, byte.MaxValue));

            Vision = new FloatOptionItem(69_211_201, "SeekerVision", new(0.05f, 5f, 0.05f), 0.5f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(255, 25, 25, byte.MaxValue));
            Speed = new FloatOptionItem(69_211_202, "SeekerSpeed", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(255, 25, 25, byte.MaxValue));
            CanVent = new BooleanOptionItem(69_211_204, "CanVent", false, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(new(255, 25, 25, byte.MaxValue));
            BlindTime = new FloatOptionItem(69_211_206, "BlindTime", new(0f, 60f, 1f), 10f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(255, 25, 25, byte.MaxValue));
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }
    }
}