using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using HarmonyLib;

namespace EHR.Neutral
{
    public class Shifter : RoleBase
    {
        private const int Id = 644400;
        public static bool On;

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        private static OptionItem StealAddons;
        private static OptionItem StealProgress;

        public override bool IsEnable => On;

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
            StealAddons = BooleanOptionItem.Create(Id + 5, "Shifter.StealAddons", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
            StealProgress = BooleanOptionItem.Create(Id + 6, "Shifter.StealProgress", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Shifter]);
        }

        public override void Init()
        {
            On = false;
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

            killer.RpcSetCustomRole(target.GetCustomRole());

            if (StealProgress.GetBool())
            {
                Main.PlayerStates[killer.PlayerId].Role = Main.PlayerStates[target.PlayerId].Role;
                killer.SetAbilityUseLimit(target.GetAbilityUseLimit());
            }

            if (StealAddons.GetBool())
            {
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
            }

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
    }
}