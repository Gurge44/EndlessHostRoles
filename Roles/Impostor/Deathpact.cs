using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Roles.Crewmate;
using EHR.Roles.Neutral;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Roles.Impostor
{
    public class Deathpact : RoleBase
    {
        private const int Id = 1100;
        public static List<byte> playerIdList = [];

        public static List<byte> ActiveDeathpacts = [];

        private static OptionItem KillCooldown;
        private static OptionItem ShapeshiftCooldown;
        private static OptionItem DeathpactDuration;
        private static OptionItem NumberOfPlayersInPact;
        private static OptionItem ShowArrowsToOtherPlayersInPact;
        private static OptionItem ReduceVisionWhileInPact;
        private static OptionItem VisionWhileInPact;
        private static OptionItem KillDeathpactPlayersOnMeeting;
        private byte DeathPactId;
        public long DeathpactTime;

        public List<PlayerControl> PlayersInDeathpact = [];

        public override bool IsEnable => playerIdList.Count > 0 || Randomizer.Exists;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Deathpact);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 11, "DeathPactCooldown", new(0f, 180f, 2.5f), 15f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact])
                .SetValueFormat(OptionFormat.Seconds);
            DeathpactDuration = FloatOptionItem.Create(Id + 13, "DeathpactDuration", new(0f, 180f, 2.5f), 17.5f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact])
                .SetValueFormat(OptionFormat.Seconds);
            NumberOfPlayersInPact = IntegerOptionItem.Create(Id + 14, "DeathpactNumberOfPlayersInPact", new(2, 5, 1), 2, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact])
                .SetValueFormat(OptionFormat.Times);
            ShowArrowsToOtherPlayersInPact = BooleanOptionItem.Create(Id + 15, "DeathpactShowArrowsToOtherPlayersInPact", true, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact]);
            ReduceVisionWhileInPact = BooleanOptionItem.Create(Id + 16, "DeathpactReduceVisionWhileInPact", false, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact]);
            VisionWhileInPact = FloatOptionItem.Create(Id + 17, "DeathpactVisionWhileInPact", new(0f, 5f, 0.05f), 0.4f, TabGroup.ImpostorRoles).SetParent(ReduceVisionWhileInPact)
                .SetValueFormat(OptionFormat.Multiplier);
            KillDeathpactPlayersOnMeeting = BooleanOptionItem.Create(Id + 18, "DeathpactKillPlayersInDeathpactOnMeeting", false, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact]);
        }

        public override void Init()
        {
            playerIdList = [];
            PlayersInDeathpact = [];
            DeathpactTime = 0;
            ActiveDeathpacts = [];
            DeathPactId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            PlayersInDeathpact = [];
            DeathpactTime = 0;
            DeathPactId = playerId;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();

        public override bool OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
        {
            if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId) || !shapeshifting) return false;

            if (!target.IsAlive() || Pelican.IsEaten(target.PlayerId))
            {
                pc.Notify(GetString("DeathpactCouldNotAddTarget"));
                return false;
            }

            if (PlayersInDeathpact.All(b => b.PlayerId != target.PlayerId))
            {
                PlayersInDeathpact.Add(target);
            }

            if (PlayersInDeathpact.Count < NumberOfPlayersInPact.GetInt())
            {
                return false;
            }

            if (ReduceVisionWhileInPact.GetBool())
            {
                foreach (PlayerControl player in PlayersInDeathpact)
                {
                    foreach (var otherPlayerInPact in PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId))
                    {
                        otherPlayerInPact.MarkDirtySettings();
                        player.MarkDirtySettings();
                    }
                }
            }

            pc.Notify(GetString("DeathpactComplete"));
            DeathpactTime = TimeStamp + DeathpactDuration.GetInt();
            ActiveDeathpacts.Add(pc.PlayerId);

            if (ShowArrowsToOtherPlayersInPact.GetBool())
            {
                foreach (PlayerControl player in PlayersInDeathpact)
                {
                    foreach (var otherPlayerInPact in PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId))
                    {
                        TargetArrow.Add(player.PlayerId, otherPlayerInPact.PlayerId);
                        otherPlayerInPact.MarkDirtySettings();
                    }
                }
            }

            return false;
        }

        public static void SetDeathpactVision(PlayerControl player, IGameOptions opt)
        {
            if (!ReduceVisionWhileInPact.GetBool())
            {
                return;
            }

            if (Main.PlayerStates[player.PlayerId].Role is not Deathpact { IsEnable: true } dp) return;

            if (dp.PlayersInDeathpact.Any(b => b.PlayerId == player.PlayerId) && dp.PlayersInDeathpact.Count == NumberOfPlayersInPact.GetInt())
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, VisionWhileInPact.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, VisionWhileInPact.GetFloat());
            }
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!IsEnable || !GameStates.IsInTask || player.GetCustomRole() is not CustomRoles.Deathpact and not CustomRoles.Randomizer) return;
            if (!ActiveDeathpacts.Contains(player.PlayerId)) return;
            if (CheckCancelDeathpact(player)) return;
            if (DeathpactTime < TimeStamp && DeathpactTime != 0)
            {
                foreach (PlayerControl playerInDeathpact in PlayersInDeathpact)
                {
                    KillPlayerInDeathpact(player, playerInDeathpact);
                }

                ClearDeathpact(player.PlayerId);
                player.Notify(GetString("DeathpactExecuted"));
            }
        }

        public static bool CheckCancelDeathpact(PlayerControl deathpact)
        {
            if (Main.PlayerStates[deathpact.PlayerId].Role is not Deathpact { IsEnable: true } dp) return true;

            if (dp.PlayersInDeathpact.Any(a => a.Data.Disconnected || a.Data.IsDead))
            {
                ClearDeathpact(deathpact.PlayerId);
                deathpact.Notify(GetString("DeathpactAverted"));
                return true;
            }

            bool cancelDeathpact = true;

            foreach (PlayerControl player in dp.PlayersInDeathpact)
            {
                float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                cancelDeathpact = dp.PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId).Select(otherPlayerInPact => Vector2.Distance(player.transform.position, otherPlayerInPact.transform.position)).Aggregate(cancelDeathpact, (current, dis) => current && (dis <= range));
            }

            if (cancelDeathpact)
            {
                ClearDeathpact(deathpact.PlayerId);
                deathpact.Notify(GetString("DeathpactAverted"));
            }

            return cancelDeathpact;
        }

        public static void KillPlayerInDeathpact(PlayerControl deathpact, PlayerControl target)
        {
            if (deathpact == null || target == null || target.Data.Disconnected) return;
            if (!target.IsAlive()) return;

            target.Suicide(realKiller: deathpact);
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
        {
            if (GameStates.IsMeeting) return string.Empty;
            if (!ShowArrowsToOtherPlayersInPact.GetBool()) return string.Empty;
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
            if (!IsInActiveDeathpact(seer)) return string.Empty;

            string arrows = string.Empty;
            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is Deathpact { IsEnable: true } dp)
                {
                    arrows = dp.PlayersInDeathpact.Where(a => a.PlayerId != seer.PlayerId).Select(otherPlayerInPact => TargetArrow.GetArrows(seer, otherPlayerInPact.PlayerId)).Aggregate(arrows, (current, arrow) => current + ColorString(GetRoleColor(CustomRoles.Crewmate), arrow));
                }
            }

            return arrows;
        }

        public static string GetDeathpactMark(PlayerControl seer, PlayerControl target)
        {
            if (!seer.Is(CustomRoles.Deathpact) || !IsInDeathpact(seer.PlayerId, target)) return string.Empty;
            return ColorString(Palette.ImpostorRed, "◀");
        }

        public static bool IsInActiveDeathpact(PlayerControl player)
        {
            if (ActiveDeathpacts.Count == 0) return false;
            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is Deathpact { IsEnable: true } dp)
                {
                    if (!ActiveDeathpacts.Contains(dp.DeathPactId) || dp.PlayersInDeathpact.All(b => b.PlayerId != player.PlayerId)) continue;
                    return true;
                }
            }

            return false;
        }

        public static bool IsInDeathpact(byte deathpact, PlayerControl target)
        {
            return Main.PlayerStates[deathpact].Role is Deathpact { IsEnable: true } dp && dp.PlayersInDeathpact.Any(a => a.PlayerId == target.PlayerId);
        }

        public static string GetDeathpactString(PlayerControl player)
        {
            string result = string.Empty;

            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is Deathpact { IsEnable: true } dp)
                {
                    if (!ActiveDeathpacts.Contains(dp.DeathPactId) || dp.PlayersInDeathpact.All(b => b.PlayerId != player.PlayerId)) continue;

                    string otherPlayerNames = dp.PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId).Aggregate(string.Empty, (current, otherPlayerInPact) => current + otherPlayerInPact.name.ToUpper() + ",");
                    otherPlayerNames = otherPlayerNames.Remove(otherPlayerNames.Length - 1);

                    int countdown = (int)(dp.DeathpactTime - TimeStamp);

                    result += $"{ColorString(GetRoleColor(CustomRoles.Impostor), string.Format(GetString("DeathpactActiveDeathpact"), otherPlayerNames, countdown))}";
                }
            }

            return result;
        }

        public static void ClearDeathpact(byte deathpact)
        {
            if (Main.PlayerStates[deathpact].Role is not Deathpact { IsEnable: true } dp) return;

            dp.DeathpactTime = 0;
            ActiveDeathpacts.Remove(deathpact);
            dp.PlayersInDeathpact.Clear();

            if (ReduceVisionWhileInPact.GetBool())
            {
                foreach (PlayerControl player in dp.PlayersInDeathpact)
                {
                    foreach (var otherPlayerInPact in dp.PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId))
                    {
                        if (ShowArrowsToOtherPlayersInPact.GetBool()) TargetArrow.Remove(player.PlayerId, otherPlayerInPact.PlayerId);
                        otherPlayerInPact.MarkDirtySettings();
                        player.MarkDirtySettings();
                    }
                }
            }
        }

        public override void OnReportDeadBody()
        {
            foreach (byte deathpact in ActiveDeathpacts)
            {
                if (KillDeathpactPlayersOnMeeting.GetBool())
                {
                    var deathpactPlayer = Main.AllPlayerControls.FirstOrDefault(a => a.PlayerId == deathpact);
                    if (deathpactPlayer == null || deathpactPlayer.Data.IsDead)
                    {
                        continue;
                    }

                    foreach (var player in PlayersInDeathpact)
                    {
                        KillPlayerInDeathpact(deathpactPlayer, player);
                    }
                }

                ClearDeathpact(deathpact);
            }
        }
    }
}