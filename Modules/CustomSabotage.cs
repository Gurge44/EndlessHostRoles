using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Modules;

public abstract class CustomSabotage
{
    public static readonly List<CustomSabotage> Instances = [];

    public virtual void Deteriorate()
    {
        Instances.Add(this);
    }

    protected virtual void Update() { }

    protected virtual void Fix()
    {
        Instances.Remove(this);
        SabotageSystemTypeRepairDamagePatch.Instance.IsDirty = true;
    }

    protected virtual string GetSuffix(PlayerControl seer, PlayerControl target, bool hud, bool meeting)
    {
        return string.Empty;
    }


    protected static int GetDefaultSabotageTimeLimit(MapNames map)
    {
        return map switch
        {
            MapNames.Skeld or MapNames.Dleks => 30,
            MapNames.MiraHQ => 45,
            MapNames.Polus => 60,
            MapNames.Airship => 90,
            MapNames.Fungle => 60,
            _ => 75
        };
    }

    protected static void AdjustTimeLimitBasedOnPlayerSpeed(ref int timeLimit)
    {
        float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        timeLimit = (int)Math.Ceiling(timeLimit / speed);
    }


    public static string GetAllSuffix(PlayerControl seer, PlayerControl target, bool hud, bool meeting)
    {
        StringBuilder suffix = new();

        foreach (CustomSabotage sabotage in Instances)
        {
            string tempSuffix = sabotage.GetSuffix(seer, target, hud, meeting).Trim();

            if (!string.IsNullOrEmpty(tempSuffix))
                suffix.Append($"{tempSuffix}\n");
        }

        return suffix.ToString().Trim();
    }

    public static void UpdateAll()
    {
        foreach (CustomSabotage sabotage in Instances.ToArray())
        {
            try { sabotage.Update(); }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        if (Instances.Count > 0)
        {
            SabotageSystemTypeRepairDamagePatch.Instance.Timer = SabotageSystemTypeRepairDamagePatch.IsCooldownModificationEnabled
                ? SabotageSystemTypeRepairDamagePatch.ModifiedCooldownSec
                : 30f;
        }
    }

    public static void Reset()
    {
        Instances.Clear();
    }
}

public class GrabOxygenMaskSabotage : CustomSabotage
{
    private long LastNotify;
    private long LastReactorFlash;
    private long DeteriorateTS;
    private HashSet<byte> HasMask;
    private SystemTypes TargetRoom;
    private int TimeLimit;
    private Vector2 RoomPosition;

    public override void Deteriorate()
    {
        base.Deteriorate();

        DeteriorateTS = Utils.TimeStamp;
        HasMask = [];

        MapNames map = Main.CurrentMap;

        TargetRoom = map switch
        {
            MapNames.Skeld or MapNames.Dleks => SystemTypes.MedBay,
            MapNames.MiraHQ => SystemTypes.Balcony,
            MapNames.Polus => SystemTypes.Specimens,
            MapNames.Airship => SystemTypes.Medical,
            MapNames.Fungle => SystemTypes.Kitchen,
            _ => SystemTypes.Outside
        };

        TimeLimit = GetDefaultSabotageTimeLimit(map);
        AdjustTimeLimitBasedOnPlayerSpeed(ref TimeLimit);

        RoomPosition = RandomSpawn.SpawnMap.GetSpawnMap().Positions.GetValueOrDefault(TargetRoom, TargetRoom.GetRoomClass().transform.position);
        Main.AllAlivePlayerControls.Do(x => LocateArrow.Add(x.PlayerId, RoomPosition));
    }

    protected override void Update()
    {
        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        byte[] playersInRoom = aapc.Select(x => (id: x.PlayerId, room: x.GetPlainShipRoom())).Where(x => x.room != null && x.room.RoomId == TargetRoom).Select(x => x.id).ToArray();
        playersInRoom.Except(HasMask).ToValidPlayers().NotifyPlayers(Translator.GetString("CustomSabotage.GrabOxygenMask.Done"));
        playersInRoom.Except(HasMask).Do(x => LocateArrow.Remove(x, RoomPosition));
        HasMask.UnionWith(playersInRoom);

        if (HasMask.IsSupersetOf(aapc.Select(x => x.PlayerId)))
        {
            Main.AllPlayerControls.Do(x => LocateArrow.Remove(x.PlayerId, RoomPosition));
            Fix();
            return;
        }

        long now = Utils.TimeStamp;

        if (DeteriorateTS + TimeLimit <= now)
        {
            Main.AllPlayerControls.Do(x => LocateArrow.Remove(x.PlayerId, RoomPosition));
            aapc.ExceptBy(HasMask, x => x.PlayerId).Do(x => x.Suicide(PlayerState.DeathReason.OutOfOxygen));
            Fix();
            return;
        }

        if (LastReactorFlash + 2 <= now)
        {
            LastReactorFlash = now;
            aapc.ExceptBy(HasMask, x => x.PlayerId).Do(x => x.ReactorFlash(flashDuration: 0.1f));
        }

        if (LastNotify != now)
        {
            LastNotify = now;
            Utils.NotifyRoles();
        }
    }

    protected override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud, bool meeting)
    {
        if (seer.PlayerId != target.PlayerId || hud || meeting || HasMask.Contains(seer.PlayerId)) return string.Empty;
        long now = Utils.TimeStamp;
        return Utils.ColorString(now % 2 == 0 ? Color.yellow : Color.red, $"[{Translator.GetString(TargetRoom.ToString())}] {Translator.GetString("CustomSabotage.GrabOxygenMask")} {TimeLimit - (now - DeteriorateTS)} {LocateArrow.GetArrow(seer, RoomPosition)}");
    }
}