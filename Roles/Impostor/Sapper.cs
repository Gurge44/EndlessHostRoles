using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Sapper
    {
        private static readonly int Id = 643000;
        public static List<byte> playerIdList = [];

        public static OptionItem ShapeshiftCooldown;
        private static OptionItem Delay;
        private static OptionItem Radius;

        public static Dictionary<Vector2, long> Bombs;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Sapper);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 11, "SapperCD", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
                .SetValueFormat(OptionFormat.Seconds);
            Delay = IntegerOptionItem.Create(Id + 12, "SapperDelay", new(1, 15, 1), 5, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
                .SetValueFormat(OptionFormat.Times);
            Radius = FloatOptionItem.Create(Id + 13, "SapperRadius", new(0f, 10f, 0.25f), 3f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public static void Init()
        {
            playerIdList = [];
            Bombs = [];
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static void ApplyGameOptions()
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public static bool IsEnable => playerIdList.Count > 0;

        public static void OnShapeshift(PlayerControl pc, bool isPet = false)
        {
            if (pc == null) return;
            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return;

            Bombs.TryAdd(pc.Pos(), TimeStamp);

            //if (!isPet) _ = new LateTask(() => { pc.CmdCheckRevertShapeshift(false); }, 1.5f, "Sapper RpcRevertShapeshift");
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null || Bombs.Count == 0 || !GameStates.IsInTask || !pc.IsAlive() || !pc.Is(CustomRoles.Sapper)) return;

            foreach (var bomb in Bombs.Where(bomb => bomb.Value + Delay.GetInt() < TimeStamp))
            {
                bool b = false;
                var players = GetPlayersInRadius(Radius.GetFloat(), bomb.Key);
                foreach (PlayerControl tg in players.ToArray())
                {
                    if (tg.PlayerId == pc.PlayerId)
                    {
                        b = true;
                        continue;
                    }
                    tg.Suicide(PlayerState.DeathReason.Bombed, pc);
                }
                Bombs.Remove(bomb.Key);
                pc.Notify(GetString("MagicianBombExploded"));
                if (b) _ = new LateTask(() =>
                {
                    if (!GameStates.IsEnded)
                    {
                        pc.Suicide(PlayerState.DeathReason.Bombed);
                    }
                }, 0.5f, "Sapper Bomb Suicide");
            }

            var sb = new StringBuilder();
            long[] list = [.. Bombs.Values];
            foreach (long x in list)
            {
                sb.Append(string.Format(GetString("MagicianBombExlodesIn"), Delay.GetInt() - (TimeStamp - x) + 1));
            }
            pc.Notify(sb.ToString());
        }

        public static void OnReportDeadBody()
        {
            Bombs.Clear();
        }
    }
}
