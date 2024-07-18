using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.Neutral;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor
{
    public class Sapper : RoleBase
    {
        private const int Id = 643000;
        public static List<byte> playerIdList = [];

        public static OptionItem ShapeshiftCooldown;
        private static OptionItem Delay;
        private static OptionItem Radius;

        public static Dictionary<Vector2, long> Bombs = [];

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Sapper);
            ShapeshiftCooldown = new FloatOptionItem(Id + 11, "SapperCD", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
                .SetValueFormat(OptionFormat.Seconds);
            Delay = new IntegerOptionItem(Id + 12, "SapperDelay", new(1, 15, 1), 5, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
                .SetValueFormat(OptionFormat.Times);
            Radius = new FloatOptionItem(Id + 13, "SapperRadius", new(0f, 10f, 0.25f), 3f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Init()
        {
            playerIdList = [];
            Bombs = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            if (UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = ShapeshiftCooldown.GetFloat();
            else
            {
                AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
                AURoleOptions.ShapeshifterDuration = 1f;
            }
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = 300f;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;
            return PlaceBomb(shapeshifter);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            return false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            PlaceBomb(pc);
        }

        public override bool OnVanish(PlayerControl pc)
        {
            return PlaceBomb(pc);
        }

        static bool PlaceBomb(PlayerControl pc)
        {
            if (pc == null) return false;
            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return false;

            Bombs.TryAdd(pc.Pos(), TimeStamp);

            return false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null || Bombs.Count == 0 || !GameStates.IsInTask || !pc.IsAlive()) return;

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
                if (b)
                    LateTask.New(() =>
                    {
                        if (!GameStates.IsEnded)
                        {
                            pc.Suicide(PlayerState.DeathReason.Bombed);
                        }
                    }, 0.5f, "Sapper Bomb Suicide");
            }

            var sb = new StringBuilder();
            foreach (long x in Bombs.Values)
            {
                sb.Append(string.Format(GetString("MagicianBombExlodesIn"), Delay.GetInt() - (TimeStamp - x) + 1));
            }

            pc.Notify(sb.ToString());
        }

        public override void OnReportDeadBody()
        {
            Bombs.Clear();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool()) hud.PetButton?.OverrideText(GetString("BomberShapeshiftText"));
            else hud.AbilityButton?.OverrideText(GetString("BomberShapeshiftText"));
        }
    }
}