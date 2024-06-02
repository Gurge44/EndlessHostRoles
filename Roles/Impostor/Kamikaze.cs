using System.Collections.Generic;
using System.Linq;
using static EHR.Options;
using static EHR.Utils;

namespace EHR.Roles.Impostor
{
    internal class Kamikaze : RoleBase
    {
        private static List<byte> PlayerIdList = [];
        public static bool On;

        private static OptionItem MarkCD;
        private static OptionItem KamikazeLimitOpt;
        public static OptionItem KamikazeAbilityUseGainWithEachKill;
        private byte KamikazeId;

        public List<byte> MarkedPlayers = [];
        private static int Id => 643310;

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Kamikaze);
            MarkCD = new FloatOptionItem(Id + 2, "KamikazeMarkCD", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Kamikaze])
                .SetValueFormat(OptionFormat.Seconds);
            KamikazeLimitOpt = new IntegerOptionItem(Id + 3, "AbilityUseLimit", new(0, 5, 1), 1, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Kamikaze])
                .SetValueFormat(OptionFormat.Times);
            KamikazeAbilityUseGainWithEachKill = new FloatOptionItem(Id + 4, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.5f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Kamikaze])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            PlayerIdList = [];
            MarkedPlayers.Clear();
            On = false;
            KamikazeId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            MarkedPlayers = [];
            playerId.SetAbilityUseLimit(KamikazeLimitOpt.GetInt());
            On = true;
            KamikazeId = playerId;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null) return false;
            if (killer.GetAbilityUseLimit() < 1) return true;
            return killer.CheckDoubleTrigger(target, () =>
            {
                MarkedPlayers.Add(target.PlayerId);
                killer.SetKillCooldown(MarkCD.GetFloat());
                killer.RpcRemoveAbilityUse();
            });
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || !On) return;

            foreach (var kkId in PlayerIdList)
            {
                if (Main.PlayerStates[kkId].Role is Kamikaze { IsEnable: true } kk)
                {
                    var kamikazePc = GetPlayerById(kk.KamikazeId);
                    if (kamikazePc.IsAlive()) continue;

                    foreach (var id in kk.MarkedPlayers)
                    {
                        var victim = GetPlayerById(id);
                        if (victim == null || !victim.IsAlive()) continue;
                        victim.Suicide(PlayerState.DeathReason.Kamikazed, kamikazePc);
                    }

                    kk.MarkedPlayers.Clear();
                    Logger.Info($"Murder {kamikazePc.GetRealName()}'s targets: {string.Join(", ", kk.MarkedPlayers.Select(x => GetPlayerById(x).GetNameWithRole()))}", "Kamikaze");
                }
            }
        }
    }
}