using System.Linq;

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
        private byte LocatorId;

        private LocateStatus Status;

        public override bool IsEnable => On;
        public Team Team => Team.Impostor;
        public int Chance => CustomRoles.Locator.GetMode();
        public int Count => CustomRoles.Locator.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_901, TabGroup.ImpostorRoles, CustomRoles.Locator, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_903, "LocatorVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
            Speed = new FloatOptionItem(69_213_904, "LocatorSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
            ArrowFrequency = new IntegerOptionItem(69_213_905, "LocatorFrequency", new(0, 60, 1), 20, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
            ArrowDuration = new FloatOptionItem(69_213_906, "LocatorDuration", new(1f, 30f, 1f), 5f, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
            HidersKnowTheyAreLocated = new BooleanOptionItem(69_213_907, "LocatorTargetKnows", true, TabGroup.ImpostorRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(new(245, 158, 66, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            Status = new();
            LocatorId = playerId;
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive()) return;

            if (Status.TargetId == byte.MaxValue)
            {
                if (Status.LastArrowEndTime + ArrowFrequency.GetInt() < Utils.TimeStamp)
                {
                    var target = HnSManager.PlayerRoles.Where(x => x.Value.Interface.Team != Team.Impostor).Select(x => Utils.GetPlayerById(x.Key)).Where(x => x != null && x.IsAlive()).Shuffle().FirstOrDefault();
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

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || isHUD || seer.PlayerId != LocatorId) return string.Empty;
            return Status.TargetId == byte.MaxValue ? string.Empty : TargetArrow.GetArrows(seer, Status.TargetId);
        }

        class LocateStatus
        {
            public byte TargetId { get; set; } = byte.MaxValue;
            public long LastArrowEndTime { get; set; } = Utils.TimeStamp;
        }
    }
}