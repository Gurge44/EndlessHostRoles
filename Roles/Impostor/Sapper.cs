using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Roles.Crewmate;
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
        public static List<byte> playerIdList = new();

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
            playerIdList = new();
            Bombs = new();
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

        public static bool IsEnable => playerIdList.Any();

        public static void OnShapeshift(PlayerControl pc, bool isPet = false)
        {
            if (pc == null) return;
            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return;

            Bombs.TryAdd(pc.transform.position, GetTimeStamp());
            Main.SapperCD.TryAdd(pc.PlayerId, GetTimeStamp());

            //if (!isPet) _ = new LateTask(() => { pc.CmdCheckRevertShapeshift(false); }, 1.5f, "Sapper RpcRevertShapeshift");
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if (!Bombs.Any()) return;
            if (!GameStates.IsInTask) return;
            if (!pc.IsAlive()) return;
            if (!pc.Is(CustomRoles.Sapper)) return;

            foreach (var bomb in Bombs.Where(bomb => bomb.Value + Delay.GetInt() < GetTimeStamp()))
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
                    Main.PlayerStates[tg.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    tg.SetRealKiller(pc);
                    tg.Kill(tg);
                    Medic.IsDead(tg);
                }
                Bombs.Remove(bomb.Key);
                pc.Notify(GetString("MagicianBombExploded"));
                if (b) _ = new LateTask(() =>
                {
                    if (!GameStates.IsEnded)
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        pc.Kill(pc);
                    }
                }, 0.5f, "Sapper Bomb Suicide");
            }

            var sb = new StringBuilder();
            long[] list = Bombs.Values.ToArray();
            foreach (long x in list)
            {
                sb.Append(string.Format(GetString("MagicianBombExlodesIn"), Delay.GetInt() - (GetTimeStamp() - x) + 1));
            }
            pc.Notify(sb.ToString());
        }

        public static void OnReportDeadBody()
        {
            Bombs.Clear();
        }
    }
}
