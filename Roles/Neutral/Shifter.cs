using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Neutral
{
    public class Shifter : RoleBase
    {
        private const int Id = 644400;
        public static bool On;

        private static List<int> WasShifter = [];
        private static Dictionary<int, RoleTypes> AllPlayerBasis = [];

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        private static OptionItem TrySwapBasis;

        public override bool IsEnable => On;
        public static bool ForceDisableTasks(int id) => !TrySwapBasis.GetBool() && WasShifter.Contains(id) && AllPlayerBasis.TryGetValue(id, out var basis) && basis is RoleTypes.Impostor or RoleTypes.Shapeshifter;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Shifter);
            KillCooldown = FloatOptionItem.Create(Id + 2, "AbilityCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
            HasImpostorVision = BooleanOptionItem.Create(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
            TrySwapBasis = BooleanOptionItem.Create(Id + 5, "Shifter.TrySwapBasis", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
        }

        public override void Init()
        {
            On = false;

            WasShifter = [];
            AllPlayerBasis = [];
            _ = new LateTask(() => AllPlayerBasis = Main.AllPlayerControls.ToDictionary(x => x.GetClientId(), x => x.GetRoleTypes()), 10f, log: false);
        }

        public override void Add(byte playerId)
        {
            On = true;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseKillButton(PlayerControl pc) => true;

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            var clientId = killer.GetClientId();
            if (clientId != -1) WasShifter.Add(clientId);

            if (TrySwapBasis.GetBool()) killer.ChangeRoleBasis(AllPlayerBasis[target.GetClientId()]);

            (killer.PlayerId, target.PlayerId) = (target.PlayerId, killer.PlayerId);

            Utils.SyncAllSettings();
            Utils.NotifyRoles(NoCache: true);

            return false;
        }
    }
}