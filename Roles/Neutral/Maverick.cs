using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral
{
    public class Maverick : RoleBase
    {
        private const int Id = 10000;

        private static OptionItem KillCooldown;
        public static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        public static OptionItem MinKillsToWin;

        public byte MaverickId = byte.MaxValue;
        public int NumOfKills;

        public override bool IsEnable => MaverickId != byte.MaxValue;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Maverick);
            KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 35f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Maverick])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Maverick]);
            HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Maverick]);
            MinKillsToWin = new IntegerOptionItem(Id + 12, "DQNumOfKillsNeeded", new(0, 14, 1), 2, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Maverick]);
        }

        public override void Init()
        {
            MaverickId = byte.MaxValue;
            NumOfKills = 0;
        }

        public override void Add(byte playerId)
        {
            MaverickId = playerId;
            NumOfKills = 0;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(HasImpostorVision.GetBool());
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive();
        }

        public override bool CanUseSabotage(PlayerControl pc)
        {
            return pc.Is(CustomRoles.Mischievous);
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            if (Main.PlayerStates[playerId].Role is not Maverick mr)
            {
                return string.Empty;
            }

            int kills = mr.NumOfKills;
            int min = MinKillsToWin.GetInt();
            Color color = kills >= min ? Color.green : Color.red;
            return Utils.ColorString(color, $"{kills}/{min}");
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            NumOfKills++;
        }
    }
}