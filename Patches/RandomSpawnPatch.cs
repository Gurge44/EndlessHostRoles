using EHR.Roles.Impostor;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR;

class RandomSpawn
{
    [HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.SnapTo), typeof(Vector2), typeof(ushort))]
    public class CustomNetworkTransformPatch
    {
        public static Dictionary<byte, int> NumOfTP = [];

        public static void Postfix(CustomNetworkTransform __instance, [HarmonyArgument(0)] Vector2 position)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (position == new Vector2(-25f, 40f)) return; //最初の湧き地点ならreturn
            if (GameStates.IsInTask)
            {
                var player = Main.AllPlayerControls.FirstOrDefault(p => p.NetTransform == __instance);
                if (player == null)
                {
                    Logger.Warn("Player is null", "RandomSpawn");
                    return;
                }

                if (player.Is(CustomRoles.GM)) return;

                NumOfTP[player.PlayerId]++;

                if (NumOfTP[player.PlayerId] == 2)
                {
                    if (Main.NormalOptions.MapId != 4) return;
                    player.RpcResetAbilityCooldown();
                    if (Options.FixFirstKillCooldown.GetBool() && !MeetingStates.MeetingCalled) player.SetKillCooldown(Main.AllPlayerKillCooldown[player.PlayerId]);
                    else if (Options.StartingKillCooldown.GetInt() != 10) player.SetKillCooldown(Options.StartingKillCooldown.GetInt());
                    if (!Options.RandomSpawn.GetBool() && Options.CurrentGameMode != CustomGameMode.SoloKombat) return;
                    new AirshipSpawnMap().RandomTeleport(player);
                    Penguin.OnSpawnAirship();
                }
            }
        }
    }

    public static void TP(CustomNetworkTransform nt, Vector2 location)
    {
        //if (AmongUsClient.Instance.AmHost) nt.SnapTo(location);
        //MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
        //NetHelpers.WriteVector2(location, writer);
        //writer.Write(nt.lastSequenceId);
        //AmongUsClient.Instance.FinishRpcImmediately(writer);
        Utils.TP(nt, location);
    }

    public abstract class SpawnMap
    {
        public virtual void RandomTeleport(PlayerControl player)
        {
            var spawn = GetLocation();
            Logger.Info($"{player.Data.PlayerName} => {spawn.Key} {spawn.Value}", "RandomSpawn");
            player.TP(spawn.Value, log: false);
        }

        public abstract KeyValuePair<string, Vector2> GetLocation();
    }

    public class SkeldSpawnMap : SpawnMap
    {
        public Dictionary<string, Vector2> positions = new()
        {
            ["Cafeteria"] = new(-1.0f, 3.0f),
            ["Weapons"] = new(9.3f, 1.0f),
            ["O2"] = new(6.5f, -3.8f),
            ["Navigation"] = new(16.5f, -4.8f),
            ["Shields"] = new(9.3f, -12.3f),
            ["Communications"] = new(4.0f, -15.5f),
            ["Storage"] = new(-1.5f, -15.5f),
            ["Admin"] = new(4.5f, -7.9f),
            ["Electrical"] = new(-7.5f, -8.8f),
            ["LowerEngine"] = new(-17.0f, -13.5f),
            ["UpperEngine"] = new(-17.0f, -1.3f),
            ["Security"] = new(-13.5f, -5.5f),
            ["Reactor"] = new(-20.5f, -5.5f),
            ["MedBay"] = new(-9.0f, -4.0f)
        };

        public override KeyValuePair<string, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class MiraHQSpawnMap : SpawnMap
    {
        public Dictionary<string, Vector2> positions = new()
        {
            ["Cafeteria"] = new(25.5f, 2.0f),
            ["Balcony"] = new(24.0f, -2.0f),
            ["Storage"] = new(19.5f, 4.0f),
            ["ThreeWay"] = new(17.8f, 11.5f),
            ["Communications"] = new(15.3f, 3.8f),
            ["MedBay"] = new(15.5f, -0.5f),
            ["LockerRoom"] = new(9.0f, 1.0f),
            ["Decontamination"] = new(6.1f, 6.0f),
            ["Laboratory"] = new(9.5f, 12.0f),
            ["Reactor"] = new(2.5f, 10.5f),
            ["Launchpad"] = new(-4.5f, 2.0f),
            ["Admin"] = new(21.0f, 17.5f),
            ["Office"] = new(15.0f, 19.0f),
            ["Greenhouse"] = new(17.8f, 23.0f)
        };

        public override KeyValuePair<string, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class PolusSpawnMap : SpawnMap
    {
        public Dictionary<string, Vector2> positions = new()
        {
            ["Office1"] = new(19.5f, -18.0f),
            ["Office2"] = new(26.0f, -17.0f),
            ["Admin"] = new(24.0f, -22.5f),
            ["Communications"] = new(12.5f, -16.0f),
            ["Weapons"] = new(12.0f, -23.5f),
            ["BoilerRoom"] = new(2.3f, -24.0f),
            ["O2"] = new(2.0f, -17.5f),
            ["Electrical"] = new(9.5f, -12.5f),
            ["Security"] = new(3.0f, -12.0f),
            ["Dropship"] = new(16.7f, -3.0f),
            ["Storage"] = new(20.5f, -12.0f),
            ["Rocket"] = new(26.7f, -8.5f),
            ["Laboratory"] = new(36.5f, -7.5f),
            ["Toilet"] = new(34.0f, -10.0f),
            ["SpecimenRoom"] = new(36.5f, -22.0f)
        };

        public override KeyValuePair<string, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class DleksSpawnMap : SpawnMap
    {
        public Dictionary<string, Vector2> positions = new SkeldSpawnMap().positions.ToDictionary(e => e.Key, e => new Vector2(-e.Value.x, e.Value.y));

        public override KeyValuePair<string, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class AirshipSpawnMap : SpawnMap
    {
        public Dictionary<string, Vector2> positions = new()
        {
            ["Brig"] = new(-0.7f, 8.5f),
            ["Engine"] = new(-0.7f, -1.0f),
            ["Kitchen"] = new(-7.0f, -11.5f),
            ["CargoBay"] = new(33.5f, -1.5f),
            ["Records"] = new(20.0f, 10.5f),
            ["MainHall"] = new(15.5f, 0.0f),
            ["NapRoom"] = new(6.3f, 2.5f),
            ["MeetingRoom"] = new(17.1f, 14.9f),
            ["GapRoom"] = new(12.0f, 8.5f),
            ["Vault"] = new(-8.9f, 12.2f),
            ["Communications"] = new(-13.3f, 1.3f),
            ["Cockpit"] = new(-23.5f, -1.6f),
            ["Armory"] = new(-10.3f, -5.9f),
            ["ViewingDeck"] = new(-13.7f, -12.6f),
            ["Security"] = new(5.8f, -10.8f),
            ["Electrical"] = new(16.3f, -8.8f),
            ["Medical"] = new(29.0f, -6.2f),
            ["Toilet"] = new(30.9f, 6.8f),
            ["Showers"] = new(21.2f, -0.8f)
        };

        public override KeyValuePair<string, Vector2> GetLocation()
        {
            return Options.AirshipAdditionalSpawn.GetBool()
                ? positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault()
                : positions.ToArray()[..6].OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }

    public class FungleSpawnMap : SpawnMap
    {
        public Dictionary<string, Vector2> positions = new()
        {
            ["FirstSpawn"] = new(-9.8f, 3.4f),
            ["Dropship"] = new(-7.8f, 10.6f),
            ["Cafeteria"] = new(-16.4f, 7.3f),
            ["SplashZone"] = new(-15.6f, -1.8f),
            ["Shore"] = new(-22.8f, -0.6f),
            ["Kitchen"] = new(-15.5f, -7.5f),
            ["Dock"] = new(-23.1f, -7.0f),
            ["Storage"] = new(1.7f, 4.4f),
            ["MeetingRoom"] = new(-3.0f, -2.6f),
            ["TheDorm"] = new(2.6f, -1.3f),
            ["Laboratory"] = new(-4.3f, -8.6f),
            ["Jungle"] = new(0.8f, -11.7f),
            ["Greenhouse"] = new(9.3f, -9.8f),
            ["Reactor"] = new(22.3f, -7.0f),
            ["Lookout"] = new(9.5f, 1.2f),
            ["MiningPit"] = new(12.6f, 9.8f),
            ["UpperEngine"] = new(22.4f, 3.4f),
            ["Communications"] = new(22.2f, 13.7f)
        };

        public override KeyValuePair<string, Vector2> GetLocation()
        {
            return positions.ToArray().OrderBy(_ => Guid.NewGuid()).Take(1).FirstOrDefault();
        }
    }
}