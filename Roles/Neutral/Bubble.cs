using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class Bubble
    {
        private static int Id => 643220;

        private static PlayerControl Bubble_ => GetPlayerById(BubbleId);
        private static byte BubbleId = byte.MaxValue;

        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;
        public static OptionItem CanVent;
        public static OptionItem NotifyDelay;
        private static OptionItem ExplodeDelay;
        private static OptionItem BubbleDiesIfInRange;
        private static OptionItem ExplosionRadius;

        public static Dictionary<byte, long> EncasedPlayers = [];

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Bubble, 1, zeroOne: false);
            KillCooldown = FloatOptionItem.Create(Id + 2, "BubbleCD", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble])
                .SetValueFormat(OptionFormat.Seconds);
            HasImpostorVision = BooleanOptionItem.Create(Id + 6, "ImpostorVision", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble]);
            NotifyDelay = IntegerOptionItem.Create(Id + 3, "BubbleTargetNotifyDelay", new(0, 60, 1), 3, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble])
                .SetValueFormat(OptionFormat.Seconds);
            ExplodeDelay = IntegerOptionItem.Create(Id + 4, "BubbleExplosionDelay", new(0, 60, 1), 10, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble])
                .SetValueFormat(OptionFormat.Seconds);
            BubbleDiesIfInRange = BooleanOptionItem.Create(Id + 5, "BubbleDiesIfInRange", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble]);
            ExplosionRadius = FloatOptionItem.Create(Id + 7, "BubbleExplosionRadius", new(0.1f, 5f, 0.1f), 3f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble])
                .SetValueFormat(OptionFormat.Multiplier);
            CanVent = BooleanOptionItem.Create(Id + 8, "CanVent", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble]);
        }
        public static void Init()
        {
            BubbleId = byte.MaxValue;
            EncasedPlayers.Clear();
        }
        public static void Add(byte playerId)
        {
            BubbleId = playerId;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => BubbleId != byte.MaxValue;
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
        public static void OnCheckMurder(PlayerControl target)
        {
            if (!IsEnable || target == null) return;
            EncasedPlayers.Add(target.PlayerId, GetTimeStamp());
            Bubble_.SetKillCooldown();
        }
        public static void OnFixedUpdate()
        {
            if (!IsEnable || !GameStates.IsInTask || !EncasedPlayers.Any()) return;

            long now = GetTimeStamp();
            foreach (var id in EncasedPlayers.Where(item => item.Value + ExplodeDelay.GetInt() < now).Select(item => item.Key).ToArray())
            {
                var players = GetPlayersInRadius(ExplosionRadius.GetFloat(), GetPlayerById(id).Pos());
                foreach (var pc in players)
                {
                    if (pc == null || (pc.PlayerId == BubbleId && !BubbleDiesIfInRange.GetBool())) continue;
                    pc.Suicide(realKiller: Bubble_);
                }
            }
        }
        public static void OnReportDeadBody()
        {
            if (IsEnable) return;
            foreach (var pc in EncasedPlayers.Keys.Select(x => GetPlayerById(x)).Where(x => x != null && x.IsAlive())) pc.Suicide(realKiller: Bubble_);
        }
    }
}
