using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Patches;

namespace EHR.Neutral
{
    public class Shifter : RoleBase
    {
        private const int Id = 644400;
        public static bool On;

        public static List<byte> WasShifter = [];

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Shifter);
            KillCooldown = new FloatOptionItem(Id + 2, "AbilityCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
            HasImpostorVision = new BooleanOptionItem(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
        }

        public override void Init()
        {
            if (GameStates.InGame && !Main.HasJustStarted) return;

            On = false;

            WasShifter = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseKillButton(PlayerControl pc) => true;

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            var targetRole = target.GetCustomRole();
            switch (targetRole)
            {
                case CustomRoles.Enigma:
                    Enigma.playerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Mediumshiper:
                    Mediumshiper.playerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Mortician:
                    Mortician.playerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Spiritualist:
                    Spiritualist.playerIdList.Remove(target.PlayerId);
                    break;
            }

            killer.RpcSetCustomRole(targetRole);
            killer.RpcChangeRoleBasis(targetRole);

            var targetRoleBase = Main.PlayerStates[target.PlayerId].Role;
            LateTask.New(() => Main.PlayerStates[killer.PlayerId].Role = targetRoleBase, 0.5f, "Change RoleBase");

            killer.SetAbilityUseLimit(target.GetAbilityUseLimit());

            var taskState = target.GetTaskState();
            if (taskState.HasTasks) Main.PlayerStates[killer.PlayerId].TaskState = taskState;

            var killerSubRoles = killer.GetCustomSubRoles().ToList();
            var targetSubRoles = target.GetCustomSubRoles().ToList();
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

            killer.RemoveAbilityCD();
            killer.SyncSettings();

            // ------------------------------------------------------------------------------------------

            target.RpcSetCustomRole(CustomRoles.Shifter);
            target.RpcChangeRoleBasis(CustomRoles.Shifter);
            Main.AbilityUseLimit.Remove(target.PlayerId);
            Utils.SendRPC(CustomRPC.RemoveAbilityUseLimit, target.PlayerId);
            target.SyncSettings();
            target.SetKillCooldown();

            // ------------------------------------------------------------------------------------------

            Utils.NotifyRoles(SpecifyTarget: killer);
            Utils.NotifyRoles(SpecifyTarget: target);

            WasShifter.Add(killer.PlayerId);

            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            OnCheckMurder(pc, ExternalRpcPetPatch.SelectKillButtonTarget(pc));
        }
    }
}