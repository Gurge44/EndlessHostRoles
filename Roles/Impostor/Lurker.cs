using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Impostor
{
    public class Lurker : RoleBase
    {
        private const int Id = 2100;
        public static List<byte> playerIdList = [];

        private static OptionItem DefaultKillCooldown;
        private static OptionItem ReduceKillCooldown;

        public override bool IsEnable => playerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Lurker);
            DefaultKillCooldown = new FloatOptionItem(Id + 10, "SansDefaultKillCooldown", new(20f, 180f, 1f), 30f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lurker])
                .SetValueFormat(OptionFormat.Seconds);
            ReduceKillCooldown = new FloatOptionItem(Id + 11, "SansReduceKillCooldown", new(0f, 10f, 1f), 1f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lurker])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            playerIdList = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = DefaultKillCooldown.GetFloat();

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (!pc.Is(CustomRoles.Lurker)) return;

            float newCd = Main.AllPlayerKillCooldown[pc.PlayerId] - ReduceKillCooldown.GetFloat();
            if (newCd <= 0)
            {
                return;
            }

            Main.AllPlayerKillCooldown[pc.PlayerId] = newCd;
            pc.SyncSettings();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            killer.ResetKillCooldown();
            killer.SyncSettings();
            return true;
        }
    }
}