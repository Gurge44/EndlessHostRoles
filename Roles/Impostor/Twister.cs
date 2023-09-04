using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Twister
    {
        private static readonly int Id = 4400;

        private static OptionItem ShapeshiftCooldown;
        private static OptionItem TwisterLimitOpt;
        public static OptionItem TwisterAbilityUseGainWithEachKill;
        public static Dictionary<byte, float> TwistLimit = new();
        //    private static OptionItem ShapeshiftDuration;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Twister);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 10, "TwisterCooldown", new(1f, 60f, 1f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Twister])
                .SetValueFormat(OptionFormat.Seconds);
            TwisterLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 5, 1), 0, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Times);
            TwisterAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.4f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Camouflager])
                .SetValueFormat(OptionFormat.Times);
            //    ShapeshiftDuration = FloatOptionItem.Create(Id + 13, "ShapeshiftDuration", new(1f, 999f, 1f), 15f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Twister])
            //      .SetValueFormat(OptionFormat.Seconds);
        }
        public static void Init()
        {
            TwistLimit = new();
        }
        public static void Add(byte playerId)
        {
            TwistLimit.Add(playerId, TwisterLimitOpt.GetInt());
        }
        public static void ApplyGameOptions()
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
        public static void TwistPlayers(PlayerControl shapeshifter, bool shapeshifting)
        {
            if (shapeshifter == null) return;
            if (TwistLimit[shapeshifter.PlayerId] < 1) return;

            List<byte> changePositionPlayers = new() { shapeshifter.PlayerId };
            TwistLimit[shapeshifter.PlayerId] -= 1;

            var rd = IRandom.Instance;
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (changePositionPlayers.Contains(pc.PlayerId) || Pelican.IsEaten(pc.PlayerId) || !pc.IsAlive() || pc.onLadder || pc.inVent || GameStates.IsMeeting)
                {
                    continue;
                }

                var filtered = Main.AllAlivePlayerControls.Where(a =>
                    pc.IsAlive() && !Pelican.IsEaten(pc.PlayerId) && !pc.inVent && a.PlayerId != pc.PlayerId && !changePositionPlayers.Contains(a.PlayerId)).ToList();
                if (!filtered.Any())
                {
                    break;
                }

                PlayerControl target = filtered[rd.Next(0, filtered.Count)];

                if (pc.inVent || target.inVent) continue;

                changePositionPlayers.Add(target.PlayerId);
                changePositionPlayers.Add(pc.PlayerId);

                pc.RPCPlayCustomSound("Teleport");

                var originPs = target.GetTruePosition();
                TP(target.NetTransform, pc.GetTruePosition());
                TP(pc.NetTransform, originPs);

                target.Notify(ColorString(GetRoleColor(CustomRoles.Twister), string.Format(GetString("TeleportedByTwister"), pc.GetRealName())));
                pc.Notify(ColorString(GetRoleColor(CustomRoles.Twister), string.Format(GetString("TeleportedByTwister"), target.GetRealName())));
            }
        }
    }
}