using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.GameMode.HideAndSeekRoles
{
    public class Locator : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem ArrowFrequency;
        public static OptionItem ArrowDuration;
        public static OptionItem HidersKnowTheyAreLocated;
        public static OptionItem Vision;
        public static OptionItem Speed;

        private LocateStatus Status;

        public override bool IsEnable => On;
        public Team Team => Team.Crewmate;
        public int Chance => CustomRoles.Locator.GetMode();
        public int Count => CustomRoles.Locator.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_301, TabGroup.CrewmateRoles, CustomRoles.Locator, CustomGameMode.HideAndSeek);
            Vision = FloatOptionItem.Create(69_211_303, "LocatorVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
            Speed = FloatOptionItem.Create(69_213_304, "LocatorSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
            ArrowFrequency = IntegerOptionItem.Create(69_213_305, "LocatorFrequency", new(0, 60, 1), 20, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
            ArrowDuration = FloatOptionItem.Create(69_213_306, "LocatorDuration", new(1f, 30f, 1f), 5f, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
            HidersKnowTheyAreLocated = BooleanOptionItem.Create(69_213_307, "LocatorTargetKnows", true, TabGroup.CrewmateRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            Status = new();
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive()) return;

            if (Status.TargetId == byte.MaxValue)
            {
                if (Status.LastArrowEndTime + ArrowFrequency.GetInt() < Utils.TimeStamp)
                {
                    var target = CustomHideAndSeekManager.PlayerRoles.Where(x => x.Value.Interface.Team == Team.Crewmate).Select(x => Utils.GetPlayerById(x.Key)).Shuffle(IRandom.Instance).FirstOrDefault();
                    if (target != null)
                    {
                        Status.TargetId = target.PlayerId;
                        Status.LastArrowEndTime = Utils.TimeStamp + ArrowDuration.GetInt();
                        TargetArrow.Add(pc.PlayerId, target.PlayerId);
                        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                        if (HidersKnowTheyAreLocated.GetBool()) target.Notify(Translator.GetString("LocatorNotify"));
                    }
                }
            }
            else if (Status.LastArrowEndTime < Utils.TimeStamp)
            {
                TargetArrow.Remove(pc.PlayerId, Status.TargetId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                Status.TargetId = byte.MaxValue;
            }
        }

        class LocateStatus
        {
            public byte TargetId { get; set; } = byte.MaxValue;
            public long LastArrowEndTime { get; set; } = Utils.TimeStamp;
        }
    }
}