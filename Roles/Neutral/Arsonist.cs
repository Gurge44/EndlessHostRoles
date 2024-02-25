using AmongUs.GameOptions;

namespace TOHE.Roles.Neutral
{
    internal class Arsonist : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            foreach (PlayerControl ar in Main.AllPlayerControls)
            {
                Main.isDoused.Add((playerId, ar.PlayerId), false);
            }
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return Options.ArsonistCanIgniteAnytime.GetBool() ? Utils.GetDousedPlayerCount(pc.PlayerId).Item1 < Options.ArsonistMaxPlayersToIgnite.GetInt() : !pc.IsDouseDone();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return pc.IsDouseDone() || (Options.ArsonistCanIgniteAnytime.GetBool() && (Utils.GetDousedPlayerCount(pc.PlayerId).Item1 >= Options.ArsonistMinPlayersToIgnite.GetInt() || pc.inVent));
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.ArsonistCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var doused = Utils.GetDousedPlayerCount(playerId);
            return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Arsonist).ShadeColor(0.25f), !Options.ArsonistCanIgniteAnytime.GetBool() ? $"<color=#777777>-</color> {doused.Item1}/{doused.Item2}" : $"<color=#777777>-</color> {doused.Item1}/{Options.ArsonistMaxPlayersToIgnite.GetInt()}");
        }
    }
}
