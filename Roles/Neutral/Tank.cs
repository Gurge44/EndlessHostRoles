using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Neutral
{
    public class Tank : RoleBase
    {
        public static bool On;

        private static OptionItem Speed;
        public static OptionItem CanBeGuessed;
        private static OptionItem CanBeKilled;
        private static OptionItem VentCooldown;

        private static HashSet<int> AllVents = [];
        private HashSet<int> EnteredVents;
        private byte TankId;

        public override bool IsEnable => On;
        public bool IsWon => EnteredVents.Count >= AllVents.Count;

        public override void SetupCustomOption()
        {
            StartSetup(646950, TabGroup.NeutralRoles, CustomRoles.Tank)
                .AutoSetupOption(ref Speed, 0.7f, new FloatValueRule(0.1f, 3f, 0.1f), OptionFormat.Multiplier)
                .AutoSetupOption(ref CanBeGuessed, false)
                .AutoSetupOption(ref CanBeKilled, true)
                .AutoSetupOption(ref VentCooldown, 15f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds);
        }

        public override void Init()
        {
            On = false;
            if (ShipStatus.Instance == null) return;
            AllVents = ShipStatus.Instance.AllVents.Select(x => x.Id).ToHashSet();
        }

        public override void Add(byte playerId)
        {
            On = true;
            TankId = playerId;
            EnteredVents = [];
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
            Main.AllPlayerSpeed[playerId] = Speed.GetFloat();
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target) => CanBeKilled.GetBool();

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            EnteredVents.Add(ventId);
            Utils.NotifyRoles(SpecifySeer: physics.myPlayer, SpecifyTarget: physics.myPlayer);
            LateTask.New(() => physics.RpcBootFromVent(ventId), 0.5f, log: false);
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var progress = $"{Utils.ColorString(Utils.GetRoleColor(CustomRoles.Tank), $"{EnteredVents.Count}")}/{AllVents.Count}";
            if (IsWon) progress = $"<#00ff00>{progress}</color>";
            return base.GetProgressText(playerId, comms) + progress;
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != TankId || isMeeting || (seer.IsModClient() && !isHUD) || IsWon) return string.Empty;
            var randomVentName = ShipStatus.Instance?.AllVents?.FirstOrDefault(x => x.Id == AllVents.Except(EnteredVents).FirstOrDefault())?.name ?? string.Empty;
            return randomVentName == string.Empty ? string.Empty : string.Format(Translator.GetString("Tank.Suffix"), randomVentName);
        }
    }
}