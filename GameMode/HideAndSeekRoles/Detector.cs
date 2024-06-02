using System.Linq;

namespace EHR.GameMode.HideAndSeekRoles
{
    public class Detector : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem InfoFrequency;
        public static OptionItem Vision;
        public static OptionItem Speed;

        private long LastInfoTime;

        public override bool IsEnable => On;
        public Team Team => Team.Crewmate;
        public int Chance => CustomRoles.Detector.GetMode();
        public int Count => CustomRoles.Detector.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_601, TabGroup.CrewmateRoles, CustomRoles.Detector, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_603, "DetectorVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(66, 221, 245, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detector]);
            Speed = new FloatOptionItem(69_213_604, "DetectorSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(66, 221, 245, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detector]);
            InfoFrequency = new IntegerOptionItem(69_213_605, "DetectorFrequency", new(0, 60, 1), 20, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(66, 221, 245, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detector]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            LastInfoTime = Utils.TimeStamp + 8 + InfoFrequency.GetInt();
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive()) return;

            long now = Utils.TimeStamp;
            if (LastInfoTime + InfoFrequency.GetInt() <= now)
            {
                var imps = HnSManager.PlayerRoles.Where(x => x.Value.Interface.Team == Team.Impostor).Select(x => Utils.GetPlayerById(x.Key)).Where(x => x != null && x.GetPlainShipRoom() != null).ToArray();
                if (imps.Length > 0)
                {
                    var imp = imps.RandomElement();
                    var room = Translator.GetString($"{imp.GetPlainShipRoom().RoomId}");
                    pc.Notify(string.Format(Translator.GetString("DetectorNotify"), room));
                    LastInfoTime = now;
                }
            }
        }
    }
}