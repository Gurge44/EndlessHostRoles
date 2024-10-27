using System.Collections.Generic;
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

        public static OptionItem KillCooldown;
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

            var pc = playerId.GetPlayer();
            if (pc == null) return;
            pc.AddAbilityCD();
            pc.ResetKillCooldown();
            pc.SyncSettings();
            pc.SetKillCooldown();
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
                    Enigma.PlayerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Mediumshiper:
                    Mediumshiper.PlayerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Mortician:
                    Mortician.PlayerIdList.Remove(target.PlayerId);
                    break;
                case CustomRoles.Spiritualist:
                    Spiritualist.PlayerIdList.Remove(target.PlayerId);
                    break;
            }

            killer.RpcSetCustomRole(targetRole);
            killer.RpcChangeRoleBasis(targetRole);

            var targetRoleBase = Main.PlayerStates[target.PlayerId].Role;
            LateTask.New(() => Main.PlayerStates[killer.PlayerId].Role = targetRoleBase, 0.5f, "Change RoleBase");

            killer.SetAbilityUseLimit(target.GetAbilityUseLimit());

            var taskState = target.GetTaskState();
            if (taskState.HasTasks) Main.PlayerStates[killer.PlayerId].TaskState = taskState;

            killer.RemoveAbilityCD();
            killer.SyncSettings();

            // ------------------------------------------------------------------------------------------

            target.RpcSetCustomRole(CustomRoles.Shifter);
            target.RpcChangeRoleBasis(CustomRoles.Shifter);
            Main.AbilityUseLimit.Remove(target.PlayerId);
            Utils.SendRPC(CustomRPC.RemoveAbilityUseLimit, target.PlayerId);
            target.SyncSettings();
            LateTask.New(() => target.SetKillCooldown(), 0.2f, log: false);

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

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("ShifterKillButtonText"));
        }
    }
}