using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Crewmate;

internal class Tornado : RoleBase
{
    private static readonly List<byte> PlayerIdList = [];

    public static OptionItem TornadoCooldown;
    public static OptionItem TornadoDuration;
    private static OptionItem TornadoRange;

    private static readonly Dictionary<string, string> ReplacementDict = new() { { "Tornado", ColorString(GetRoleColor(CustomRoles.Tornado), "Tornado") } };

    private static RandomSpawn.SpawnMap Map;
    private static readonly Dictionary<(Vector2 Location, string RoomName), long> Tornados = [];
    private static long LastNotify = TimeStamp;
    private static bool CanUseMap;
    private PlayerControl TornadoPC;
    private static int Id => 64420;

    public override bool IsEnable => PlayerIdList.Count > 0 || Randomizer.Exists;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tornado);

        TornadoCooldown = new IntegerOptionItem(Id + 2, "TornadoCooldown", new(1, 90, 1), 15, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
            .SetValueFormat(OptionFormat.Seconds);

        TornadoDuration = new IntegerOptionItem(Id + 3, "TornadoDuration", new(1, 90, 1), 25, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
            .SetValueFormat(OptionFormat.Seconds);

        TornadoRange = new FloatOptionItem(Id + 4, "TornadoRange", new(0.5f, 25f, 0.5f), 3f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tornado])
            .SetValueFormat(OptionFormat.Multiplier);

        TornadoCooldown.ReplacementDictionary = ReplacementDict;
        TornadoDuration.ReplacementDictionary = ReplacementDict;
        TornadoRange.ReplacementDictionary = ReplacementDict;
    }

    public override void Init()
    {
        PlayerIdList.Clear();
        Tornados.Clear();
        LastNotify = TimeStamp;

        try
        {
            Map = RandomSpawn.SpawnMap.GetSpawnMap();
            CanUseMap = true;
        }
        catch (ArgumentOutOfRangeException)
        {
            Logger.CurrentMethod();
            Logger.Error("Unsupported Map", "Torando");
            CanUseMap = false;
        }
    }

    public override void Add(byte playerId)
    {
        TornadoPC = GetPlayerById(playerId);
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private static void SendRPCAddTornado(bool add, Vector2 pos, string roomname, long timestamp = 0)
    {
        if (!DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AddTornado, SendOption.Reliable);
        writer.Write(add);
        writer.Write(pos.x);
        writer.Write(pos.y);
        writer.Write(roomname);
        if (add) writer.Write(timestamp.ToString());

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCAddTornado(MessageReader reader)
    {
        bool add = reader.ReadBoolean();
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        string roomname = reader.ReadString();

        if (add)
        {
            long timestamp = long.Parse(reader.ReadString());
            Tornados.TryAdd((new(x, y), roomname), timestamp);
        }
        else
            Tornados.Remove((new(x, y), roomname));
    }

    public override void OnPet(PlayerControl pc)
    {
        SpawnTornado(pc);
    }

    public static void SpawnTornado(PlayerControl pc)
    {
        if (pc == null) return;

        (Vector2 Location, string RoomName) info = pc.GetPositionInfo();
        long now = TimeStamp;
        Tornados.TryAdd(info, now);
        SendRPCAddTornado(true, info.Location, info.RoomName, now);
        _ = new TornadoObject(info.Location, [pc.PlayerId]);
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (!IsEnable || !GameStates.IsInTask || Tornados.Count == 0 || pc == null) return;

        long now = TimeStamp;

        if (!pc.Is(CustomRoles.Tornado))
        {
            var Random = IRandom.Instance;
            string NotifyString = GetString("TeleportedByTornado");
            float tornadoRange = TornadoRange.GetFloat();
            int tornadoDuration = TornadoDuration.GetInt();

            foreach (KeyValuePair<(Vector2 Location, string RoomName), long> tornado in Tornados)
            {
                if (Vector2.Distance(tornado.Key.Location, pc.Pos()) <= tornadoRange)
                {
                    if (!CanUseMap || Random.Next(0, 100) < 50)
                        pc.TPToRandomVent();
                    else
                        Map.RandomTeleport(pc);

                    pc.Notify(NotifyString);
                }

                if (tornado.Value + tornadoDuration < now)
                {
                    Tornados.Remove(tornado.Key);
                    SendRPCAddTornado(false, tornado.Key.Location, tornado.Key.RoomName);
                    NotifyRoles(SpecifySeer: TornadoPC, SpecifyTarget: TornadoPC);
                }
            }
        }
        else
        {
            if (LastNotify >= now || pc.HasAbilityCD()) return;

            NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            LastNotify = now;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || !IsEnable || (seer.IsModdedClient() && !hud) || seer.PlayerId != TornadoPC.PlayerId) return string.Empty;

        return string.Join(hud ? "\n" : ", ", Tornados.Select(x => $"Tornado {GetFormattedRoomName(x.Key.RoomName)} {GetFormattedVectorText(x.Key.Location)} ({(int)(TornadoDuration.GetInt() - (TimeStamp - x.Value) + 1)}s)"));
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}