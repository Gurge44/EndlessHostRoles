using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Impostor;
using HarmonyLib;
using UnityEngine;

namespace EHR;

class RandomSpawn
{
    public static void TP(CustomNetworkTransform nt, Vector2 location)
    {
        //if (AmongUsClient.Instance.AmHost) nt.SnapTo(location);
        //MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
        //NetHelpers.WriteVector2(location, writer);
        //writer.Write(nt.lastSequenceId);
        //AmongUsClient.Instance.FinishRpcImmediately(writer);
        Utils.TP(nt, location);
    }

    [HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.SnapTo), typeof(Vector2), typeof(ushort))]
    public class CustomNetworkTransformPatch
    {
        public static Dictionary<byte, int> NumOfTP = [];

        public static void Postfix(CustomNetworkTransform __instance, [HarmonyArgument(0)] Vector2 position)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (position == new Vector2(-25f, 40f)) return;
            if (GameStates.IsInTask)
            {
                var player = Main.AllPlayerControls.FirstOrDefault(p => p.NetTransform == __instance);
                if (player == null) return;

                if (player.Is(CustomRoles.GM)) return;

                NumOfTP[player.PlayerId]++;

                if (NumOfTP[player.PlayerId] == 2)
                {
                    if (Main.NormalOptions.MapId != 4) return;
                    player.RpcResetAbilityCooldown();
                    if (Options.FixFirstKillCooldown.GetBool() && !MeetingStates.MeetingCalled) player.SetKillCooldown(Main.AllPlayerKillCooldown[player.PlayerId]);
                    else if (Options.StartingKillCooldown.GetInt() != 10) player.SetKillCooldown(Options.StartingKillCooldown.GetInt());
                    if (!Options.RandomSpawn.GetBool() && Options.CurrentGameMode == CustomGameMode.Standard) return;
                    new AirshipSpawnMap().RandomTeleport(player);
                    Penguin.OnSpawnAirship();
                }
            }
        }
    }

    public abstract class SpawnMap
    {
        public virtual void RandomTeleport(PlayerControl player)
        {
            var spawn = GetLocation();
            Logger.Info($"{player.Data.PlayerName} => {Translator.GetString(spawn.Key.ToString())} {spawn.Value}", "RandomSpawn");
            player.TP(spawn.Value, log: false);
        }

        protected abstract KeyValuePair<SystemTypes, Vector2> GetLocation();
    }

    public class SkeldSpawnMap : SpawnMap
    {
        public readonly Dictionary<SystemTypes, Vector2> positions = new()
        {
            [SystemTypes.Cafeteria] = new(-1.0f, 3.0f),
            [SystemTypes.Weapons] = new(9.3f, 1.0f),
            [SystemTypes.LifeSupp] = new(6.5f, -3.8f),
            [SystemTypes.Nav] = new(16.5f, -4.8f),
            [SystemTypes.Shields] = new(9.3f, -12.3f),
            [SystemTypes.Comms] = new(4.0f, -15.5f),
            [SystemTypes.Storage] = new(-1.5f, -15.5f),
            [SystemTypes.Admin] = new(4.5f, -7.9f),
            [SystemTypes.Electrical] = new(-7.5f, -8.8f),
            [SystemTypes.LowerEngine] = new(-17.0f, -13.5f),
            [SystemTypes.UpperEngine] = new(-17.0f, -1.3f),
            [SystemTypes.Security] = new(-13.5f, -5.5f),
            [SystemTypes.Reactor] = new(-20.5f, -5.5f),
            [SystemTypes.MedBay] = new(-9.0f, -4.0f)
        };

        protected override KeyValuePair<SystemTypes, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class MiraHQSpawnMap : SpawnMap
    {
        public readonly Dictionary<SystemTypes, Vector2> positions = new()
        {
            [SystemTypes.Cafeteria] = new(25.5f, 2.0f),
            [SystemTypes.Balcony] = new(24.0f, -2.0f),
            [SystemTypes.Storage] = new(19.5f, 4.0f),
            [SystemTypes.Hallway] = new(17.8f, 11.5f),
            [SystemTypes.Comms] = new(15.3f, 3.8f),
            [SystemTypes.MedBay] = new(15.5f, -0.5f),
            [SystemTypes.LockerRoom] = new(9.0f, 1.0f),
            [SystemTypes.Decontamination] = new(6.1f, 6.0f),
            [SystemTypes.Laboratory] = new(9.5f, 12.0f),
            [SystemTypes.Reactor] = new(2.5f, 10.5f),
            [SystemTypes.Launchpad] = new(-4.5f, 2.0f),
            [SystemTypes.Admin] = new(21.0f, 17.5f),
            [SystemTypes.Office] = new(15.0f, 19.0f),
            [SystemTypes.Greenhouse] = new(17.8f, 23.0f)
        };

        protected override KeyValuePair<SystemTypes, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class PolusSpawnMap : SpawnMap
    {
        public readonly Dictionary<SystemTypes, Vector2> positions = new()
        {
            [SystemTypes.MeetingRoom] = new(19.5f, -18.0f), // Office
            [SystemTypes.Office] = new(26.0f, -17.0f),
            [SystemTypes.Admin] = new(24.0f, -22.5f),
            [SystemTypes.Comms] = new(12.5f, -16.0f),
            [SystemTypes.Weapons] = new(12.0f, -23.5f),
            [SystemTypes.BoilerRoom] = new(2.3f, -24.0f),
            [SystemTypes.LifeSupp] = new(2.0f, -17.5f),
            [SystemTypes.Electrical] = new(9.5f, -12.5f),
            [SystemTypes.Security] = new(3.0f, -12.0f),
            [SystemTypes.Dropship] = new(16.7f, -3.0f),
            [SystemTypes.Storage] = new(20.5f, -12.0f),
            [SystemTypes.MedBay] = new(26.7f, -8.5f), // Drill
            [SystemTypes.Laboratory] = new(36.5f, -7.5f),
            [SystemTypes.Decontamination2] = new(34.0f, -10.0f), // Toilet (Laboratory)
            [SystemTypes.Specimens] = new(36.5f, -22.0f)
        };

        protected override KeyValuePair<SystemTypes, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class DleksSpawnMap : SpawnMap
    {
        public readonly Dictionary<SystemTypes, Vector2> positions = new SkeldSpawnMap().positions.ToDictionary(e => e.Key, e => new Vector2(-e.Value.x, e.Value.y));

        protected override KeyValuePair<SystemTypes, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class AirshipSpawnMap : SpawnMap
    {
        public readonly Dictionary<SystemTypes, Vector2> positions = new()
        {
            [SystemTypes.Brig] = new(-0.7f, 8.5f),
            [SystemTypes.Engine] = new(-0.7f, -1.0f),
            [SystemTypes.Kitchen] = new(-7.0f, -11.5f),
            [SystemTypes.CargoBay] = new(33.5f, -1.5f),
            [SystemTypes.Records] = new(20.0f, 10.5f),
            [SystemTypes.MainHall] = new(15.5f, 0.0f),
            [SystemTypes.SleepingQuarters] = new(6.3f, 2.5f),
            [SystemTypes.MeetingRoom] = new(17.1f, 14.9f),
            [SystemTypes.GapRoom] = new(12.0f, 8.5f),
            [SystemTypes.VaultRoom] = new(-8.9f, 12.2f),
            [SystemTypes.Comms] = new(-13.3f, 1.3f),
            [SystemTypes.Cockpit] = new(-23.5f, -1.6f),
            [SystemTypes.Armory] = new(-10.3f, -5.9f),
            [SystemTypes.ViewingDeck] = new(-13.7f, -12.6f),
            [SystemTypes.Security] = new(5.8f, -10.8f),
            [SystemTypes.Electrical] = new(16.3f, -8.8f),
            [SystemTypes.Medical] = new(29.0f, -6.2f),
            [SystemTypes.Lounge] = new(30.9f, 6.8f),
            [SystemTypes.Showers] = new(21.2f, -0.8f)
        };

        protected override KeyValuePair<SystemTypes, Vector2> GetLocation()
        {
            return Options.AirshipAdditionalSpawn.GetBool()
                ? positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault()
                : positions.ToArray()[..6].OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class FungleSpawnMap : SpawnMap
    {
        public readonly Dictionary<SystemTypes, Vector2> positions = new()
        {
            [SystemTypes.Outside] = new(-9.8f, 3.4f), // First Spawn
            [SystemTypes.Dropship] = new(-7.8f, 10.6f),
            [SystemTypes.Cafeteria] = new(-16.4f, 7.3f),
            [SystemTypes.Balcony] = new(-15.6f, -1.8f), // Splash Zone
            [SystemTypes.Beach] = new(-22.8f, -0.6f),
            [SystemTypes.Kitchen] = new(-15.5f, -7.5f),
            [SystemTypes.FishingDock] = new(-23.1f, -7.0f),
            [SystemTypes.Storage] = new(1.7f, 4.4f),
            [SystemTypes.MeetingRoom] = new(-3.0f, -2.6f),
            [SystemTypes.SleepingQuarters] = new(2.6f, -1.3f), // The Dorm
            [SystemTypes.Laboratory] = new(-4.3f, -8.6f),
            [SystemTypes.Jungle] = new(0.8f, -11.7f),
            [SystemTypes.Greenhouse] = new(9.3f, -9.8f),
            [SystemTypes.Reactor] = new(22.3f, -7.0f),
            [SystemTypes.Lookout] = new(9.5f, 1.2f),
            [SystemTypes.MiningPit] = new(12.6f, 9.8f),
            [SystemTypes.UpperEngine] = new(22.4f, 3.4f),
            [SystemTypes.Comms] = new(22.2f, 13.7f)
        };

        protected override KeyValuePair<SystemTypes, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }
}