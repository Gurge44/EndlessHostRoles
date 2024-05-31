using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Patches;

namespace EHR.Neutral
{
    public class Shifter : RoleBase
    {
        private const int Id = 644400;
        public static bool On;

        public static List<byte> WasShifter = [];
        private static Dictionary<byte, RoleTypes> AllPlayerBasis = [];

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        private static OptionItem TryChangeBasis;

        public override bool IsEnable => On;
        public static bool ForceDisableTasks(byte id) => !TryChangeBasis.GetBool() && WasShifter.Contains(id) && AllPlayerBasis.TryGetValue(id, out var basis) && basis is RoleTypes.Impostor or RoleTypes.Shapeshifter;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Shifter);
            KillCooldown = new FloatOptionItem(Id + 2, "AbilityCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
            HasImpostorVision = new BooleanOptionItem(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
            TryChangeBasis = new BooleanOptionItem(Id + 5, "TryChangeBasis", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
        }

        public override void Init()
        {
            if (GameStates.InGame && !Main.HasJustStarted) return;

            On = false;

            WasShifter = [];
            AllPlayerBasis = [];
            LateTask.New(() => AllPlayerBasis = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, x => x.GetRoleTypes()), 10f, log: false);
        }

        public override void Add(byte playerId)
        {
            On = true;

            if (!TryChangeBasis.GetBool() || playerId == 0)
                WasShifter.Add(playerId);

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

            killer.RpcSetCustomRole(target.GetCustomRole());
            if (TryChangeBasis.GetBool())
                killer.ChangeRoleBasis(target.GetRoleTypes());

            var targetRoleBase = Main.PlayerStates[target.PlayerId].Role;
            LateTask.New(() => Main.PlayerStates[killer.PlayerId].Role = targetRoleBase, 0.5f, "Change RoleBase");

            killer.SetAbilityUseLimit(target.GetAbilityUseLimit());

            var taskState = target.GetTaskState();
            if (taskState.hasTasks) Main.PlayerStates[killer.PlayerId].taskState = taskState;

            var killerSubRoles = killer.GetCustomSubRoles();
            var targetSubRoles = target.GetCustomSubRoles();
            if (targetSubRoles.Count > 0)
            {
                killer.RpcSetCustomRole(targetSubRoles[0], replaceAllAddons: true);
                targetSubRoles.Skip(1).Do(x => killer.RpcSetCustomRole(x));
            }

            if (killerSubRoles.Count > 0)
            {
                target.RpcSetCustomRole(killerSubRoles[0], replaceAllAddons: true);
                killerSubRoles.Skip(1).Do(x => target.RpcSetCustomRole(x));
            }

            Main.AbilityCD.Remove(killer.PlayerId);
            killer.SyncSettings();

            // ------------------------------------------------------------------------------------------

            target.RpcSetCustomRole(CustomRoles.Shifter);
            Main.AbilityUseLimit.Remove(target.PlayerId);
            Utils.SendRPC(CustomRPC.RemoveAbilityUseLimit, target.PlayerId);
            target.SyncSettings();
            target.SetKillCooldown();

            // ------------------------------------------------------------------------------------------

            Utils.NotifyRoles(SpecifyTarget: killer);
            Utils.NotifyRoles(SpecifyTarget: target);

            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            OnCheckMurder(pc, ExternalRpcPetPatch.SelectKillButtonTarget(pc));
        }
    }
}