using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Crewmate;

using static Options;
using static Translator;
using static Utils;

public class Hacker : RoleBase
{
    private const int Id = 641000;
    public static Dictionary<byte, bool> PlayerIdList = [];
    public static Dictionary<byte, float> UseLimit = [];
    public static Dictionary<byte, float> UseLimitSeconds = [];

    private static Dictionary<byte, long> LastUpdate = [];

    public static OptionItem AbilityCD;
    public static OptionItem UseLimitOpt;
    public static OptionItem HackerAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    public static OptionItem ModdedClientAbilityUseSecondsMultiplier;
    public static OptionItem ModdedClientCanMoveWhileViewingMap;
    public static OptionItem VanillaClientSeesInfoFor;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Hacker);

        AbilityCD = new FloatOptionItem(Id + 10, "AbilityCD", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Seconds);

        UseLimitOpt = new IntegerOptionItem(Id + 11, "AbilityUseLimit", new(0, 20, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);

        HackerAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);

        ModdedClientAbilityUseSecondsMultiplier = new FloatOptionItem(Id + 14, "HackerModdedClientAbilityUseSecondsMultiplier", new(0f, 70f, 1f), 3f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Seconds);

        ModdedClientCanMoveWhileViewingMap = new BooleanOptionItem(Id + 15, "HackerModdedClientCanMoveWhileViewingMap", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker]);

        VanillaClientSeesInfoFor = new FloatOptionItem(Id + 16, "HackerVanillaClientSeesInfoFor", new(0f, 70f, 1f), 4f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        UseLimit = [];
        UseLimitSeconds = [];
        LastUpdate = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.TryAdd(playerId, GetPlayerById(playerId).IsModdedClient());

        if (!GetPlayerById(playerId).IsModdedClient())
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        else
            UseLimitSeconds.Add(playerId, UseLimitOpt.GetInt() * ModdedClientAbilityUseSecondsMultiplier.GetInt());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = AbilityCD.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public static void SendRPC(byte playerId, float secondsLeft)
    {
        if (!DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetHackerLimit, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(secondsLeft);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        byte playerId = reader.ReadByte();
        float secondsLeft = reader.ReadSingle();

        UseLimitSeconds[playerId] = secondsLeft;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        UseAbility(pc, false);
    }

    public override void OnPet(PlayerControl pc)
    {
        UseAbility(pc, true);
    }

    private static void UseAbility(PlayerControl pc, bool pet)
    {
        if (pc == null) return;

        if (pc.IsModdedClient() || !UseLimit.ContainsKey(pc.PlayerId)) return;

        if (UseLimit[pc.PlayerId] >= 1)
        {
            UseLimit[pc.PlayerId] -= 1;
            Dictionary<string, int> list = GetAllPlayerLocationsCount();
            var sb = new StringBuilder();

            foreach (KeyValuePair<string, int> location in list)
                sb.Append($"\n<color=#00ffa5>{location.Key}:</color> {location.Value}");

            pc.Notify(sb.ToString(), VanillaClientSeesInfoFor.GetFloat() + (pet ? 0f : 1.5f));
        }
        else
            pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc == null) return;

        if (GameStates.IsMeeting) return;

        if (Main.PlayerStates[pc.PlayerId].TaskState.IsTaskFinished)
        {
            LastUpdate.TryAdd(pc.PlayerId, TimeStamp);

            if (LastUpdate[pc.PlayerId] + 5 < TimeStamp)
            {
                if (pc.IsModdedClient())
                    UseLimitSeconds[pc.PlayerId] += AbilityChargesWhenFinishedTasks.GetFloat() * ModdedClientAbilityUseSecondsMultiplier.GetInt();
                else
                    UseLimit[pc.PlayerId] += AbilityChargesWhenFinishedTasks.GetFloat();

                LastUpdate[pc.PlayerId] = TimeStamp;
            }
        }
    }

    public static void MapHandle(PlayerControl pc, MapBehaviour map, MapOptions opts)
    {
        map.countOverlayAllowsMovement = ModdedClientCanMoveWhileViewingMap.GetBool();

        if (UseLimitSeconds[pc.PlayerId] >= 1)
        {
            UseLimitSeconds[pc.PlayerId] -= 1; // Remove 1s for opening the map, so they can't utilize spamming
            SendRPC(pc.PlayerId, UseLimitSeconds[pc.PlayerId]);
            opts.Mode = MapOptions.Modes.CountOverlay;
            LateTask.New(() => { MapCountdown(pc, map, opts, (int)UseLimitSeconds[pc.PlayerId]); }, 1f, "Hacker.StartCountdown");
        }
        else
        {
            opts.Mode = pc.IsMadmate() ? MapOptions.Modes.Sabotage : MapOptions.Modes.Normal;
            pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
        }
    }

    private static void MapCountdown(PlayerControl pc, MapBehaviour map, MapOptions opts, int seconds)
    {
        if (!map.IsOpen) return;

        if (seconds <= 0)
        {
            map.Close();
            pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
            opts.Mode = pc.IsMadmate() ? MapOptions.Modes.Sabotage : MapOptions.Modes.Normal;
            return;
        }

        UseLimitSeconds[pc.PlayerId] -= 1;
        SendRPC(pc.PlayerId, UseLimitSeconds[pc.PlayerId]);
        LateTask.New(() => { MapCountdown(pc, map, opts, seconds - 1); }, 1f, "HackerAbilityCountdown");
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || seer == null) return string.Empty;

        return !seer.Is(CustomRoles.Hacker) ? string.Empty : $"<color=#00ffa5>{GetString("HackerAbilitySecondsLeft")}:</color> <b>{(int)UseLimitSeconds[seer.PlayerId]}</b>s";
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        if (playerId.IsPlayerModdedClient() || !UseLimit.ContainsKey(playerId)) return string.Empty;

        var sb = new StringBuilder();

        sb.Append(GetTaskCount(playerId, comms));
        sb.Append(ColorString(UseLimit[playerId] < 1 ? Color.red : Color.white, $" <color=#777777>-</color> {Math.Round(UseLimit[playerId], 1)}"));

        return sb.ToString();
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}