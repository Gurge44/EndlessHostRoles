using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Crewmate;

public class Whisperer : RoleBase
{
    public static bool On;
    private static List<Whisperer> Instances = [];

    public static OptionItem Cooldown;
    public static OptionItem Duration;
    public static OptionItem AbilityUseLimit;
    public static OptionItem WhispererAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static DateTime LastMeetingStart;
    private static Dictionary<byte, (byte[] SameRoomPlayers, SystemTypes? ActiveSabotage)> DeathInfo = [];

    private int Count;
    private (string Name, int Percent) CurrentlyQuestioning;
    private List<string> Info;
    private List<Soul> Souls;
    private byte WhispererId;
    
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 649550;
        Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Whisperer);

        Cooldown = new IntegerOptionItem(++id, "WhispererCooldown", new(0, 60, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Whisperer])
            .SetValueFormat(OptionFormat.Seconds);

        Duration = new IntegerOptionItem(++id, "WhispererDuration", new(0, 60, 1), 4, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Whisperer])
            .SetValueFormat(OptionFormat.Seconds);

        AbilityUseLimit = new FloatOptionItem(++id, "AbilityUseLimit", new(0, 20, 0.05f), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Whisperer])
            .SetValueFormat(OptionFormat.Times);

        WhispererAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 3.5f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Whisperer])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Whisperer])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
        DeathInfo = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Count = 0;
        Souls = [];
        Info = [];
        Instances.Add(this);
        WhispererId = playerId;
        CurrentlyQuestioning = (string.Empty, 0);
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1 || Souls.Exists(x => x.IsQuestioning)) return;

        Vector2 pos = pc.Pos();
        List<Soul> souls = Souls.FindAll(x => x.IsQuestionAble && Vector2.Distance(pos, x.Position) <= 1f);
        if (souls.Count == 0) return;

        Soul soul = souls.MinBy(x => Vector2.Distance(pos, x.Position));
        soul.QuestioningTime = Duration.GetInt();
        CurrentlyQuestioning.Name = soul.Player.PlayerId.ColoredPlayerName();
        Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 1, CurrentlyQuestioning.Name);
        Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 3, soul.Player.PlayerId, soul.QuestioningTime);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

        Soul soul = Souls.Find(x => x.IsQuestioning);
        if (soul == null) return;

        byte soulPlayerId = soul.Player.PlayerId;

        if (Vector2.Distance(pc.Pos(), soul.Position) > 1.5f)
        {
            soul.QuestioningTime = 0f;
            Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 3, soulPlayerId, soul.QuestioningTime);
            return;
        }

        soul.QuestioningTime -= Time.fixedDeltaTime;

        if (soul.QuestioningTime <= 0f)
        {
            soul.QuestioningTime = 0f;
            CurrentlyQuestioning = (string.Empty, 0);

            Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 1, CurrentlyQuestioning.Name);
            Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 2, CurrentlyQuestioning.Percent);
            Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 3, soulPlayerId, soul.QuestioningTime);

            string info;

            try
            {
                PlayerState state = Main.PlayerStates[soulPlayerId];
                (DateTime TimeStamp, byte ID) killer = state.RealKiller;
                int next = IRandom.Instance.Next(7);
                if (state.deathReason == PlayerState.DeathReason.Disconnected) next = 2;
                (byte[] SameRoomPlayers, SystemTypes? ActiveSabotage) deathInfo = ([], null);
                if (next > 3 && !DeathInfo.TryGetValue(soulPlayerId, out deathInfo)) next = IRandom.Instance.Next(4);
                if (pc.Is(CustomRoles.Autopsy) || pc.Is(CustomRoles.Doctor) || Options.EveryoneSeesDeathReasons.GetBool()) next = IRandom.Instance.Next(6);
                PlayerState killerState = Main.PlayerStates[killer.ID];

                info = next switch
                {
                    0 => string.Format(Translator.GetString("WhispererInfo.Color"), GetColorInfo(Utils.GetPlayerInfoById(killer.ID).DefaultOutfit.ColorId, out string colors), colors),
                    1 => string.Format(Translator.GetString("WhispererInfo.Time"), (int)Math.Round((LastMeetingStart - killer.TimeStamp).TotalSeconds)),
                    2 => string.Format(Translator.GetString("WhispererInfo.Role"), state.MainRole.ToColoredString() + (state.SubRoles.Count == 0 ? string.Empty : string.Join(' ', state.SubRoles.ConvertAll(x => x.ToColoredString())))),
                    3 => string.Format(Translator.GetString("WhispererInfo.KillerRole"), killerState.MainRole.ToColoredString() + (killerState.SubRoles.Count == 0 ? string.Empty : string.Join(' ', killerState.SubRoles.ConvertAll(x => x.ToColoredString())))),
                    4 => string.Format(Translator.GetString(deathInfo.SameRoomPlayers.Length == 0 ? "WhispererInfo.AloneInRoomAtDeath" : "WhispererInfo.PlayersInSameRoomAtDeath"), string.Join(", ", deathInfo.SameRoomPlayers.Select(x => x.ColoredPlayerName()))),
                    5 => string.Format(Translator.GetString(deathInfo.ActiveSabotage.HasValue ? "WhispererInfo.NoSabotageAtDeath" : "WhispererInfo.SabotageAtDeath"), Translator.GetString(deathInfo.ActiveSabotage.HasValue ? deathInfo.ActiveSabotage.ToString() : "None")),
                    6 => string.Format(Translator.GetString("WhispererInfo.DeathReason"), Translator.GetString($"DeathReason.{state.deathReason}")),
                    _ => string.Empty
                };

                info = $"{soulPlayerId.ColoredPlayerName()}: {info}";
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
                info = string.Empty;
            }

            if (!string.IsNullOrEmpty(info))
            {
                Info.Add(info);
                if (Info.Count > 5) Info.RemoveAt(0);

                pc.RpcRemoveAbilityUse();
                Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 4, info);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }
        }
        else
        {
            if (Count++ < 10) return;

            Count = 0;
            CurrentlyQuestioning.Percent = 100 - (int)(soul.QuestioningTime * 100f / Duration.GetFloat());
            Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 2, CurrentlyQuestioning.Percent);
            Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 3, soulPlayerId, soul.QuestioningTime);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    // Color IDs: Red-0, Blue-1, Green-2, Pink-3, Orange-4, Yellow-5, Black-6, White-7, Purple-8, Brown-9, Cyan-10, Lime-11, Maroon-12, Rose-13, Banana-14, Gray-15, Tan-16, Coral-17
    // Lighter color IDs: 3, 4, 5, 7, 10, 11, 13, 14, 16, 17 == Pink, Orange, Yellow, White, Cyan, Lime, Rose, Banana, Tan, Coral
    // Darker color IDs: 0, 1, 2, 6, 8, 9, 12, 15 == Red, Blue, Green, Black, Purple, Brown, Maroon, Gray

    private static string GetColorInfo(int colorId, out string colors)
    {
        var darker = new List<int> { 0, 1, 2, 6, 8, 9, 12, 15 };
        bool isDarker = darker.Contains(colorId);
        Func<int, string> selector = x => Utils.ColorString(Palette.PlayerColors[x], Palette.GetColorName(x));
        colors = isDarker ? string.Join('/', darker.Select(selector)) : string.Join('/', Enumerable.Range(0, 18).Except(darker).Select(selector));
        return isDarker ? Translator.GetString("WhispererInfo.ColorDark") : Translator.GetString("WhispererInfo.ColorLight");
    }

    public static void OnAnyoneDied(PlayerControl target)
    {
        foreach (Whisperer instance in Instances)
        {
            if (instance.Souls.Exists(x => x.Player.PlayerId == target.PlayerId)) continue;

            instance.Souls.Add(new(target));
            Utils.SendRPC(CustomRPC.SyncRoleData, instance.WhispererId, 5, target.PlayerId);
        }

        var room = target.GetPlainShipRoom();
        DeathInfo[target.PlayerId] = (room == null ? [] : Main.AllAlivePlayerControls.Where(x => x.IsInRoom(room)).Select(x => x.PlayerId).ToArray(), new[] { SystemTypes.Electrical, SystemTypes.Reactor, SystemTypes.Laboratory, SystemTypes.LifeSupp, SystemTypes.Comms, SystemTypes.HeliSabotage, SystemTypes.MushroomMixupSabotage, (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast }.FindFirst(Utils.IsActive, out var sabotage) ? sabotage : null);
    }

    public override void OnReportDeadBody()
    {
        LastMeetingStart = DateTime.Now;
    }

    public override void AfterMeetingTasks()
    {
        foreach (Soul soul in Souls)
        {
            soul.QuestioningTime = 0f;
            soul.NetObject ??= new(soul.Position, WhispererId.GetPlayer());
            soul.IsQuestionAble = true;
            Utils.SendRPC(CustomRPC.SyncRoleData, WhispererId, 3, soul.Player.PlayerId, soul.QuestioningTime);
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                CurrentlyQuestioning.Name = reader.ReadString();
                break;
            case 2:
                CurrentlyQuestioning.Percent = reader.ReadPackedInt32();
                break;
            case 3:
                byte id = reader.ReadByte();
                Souls.Find(x => x.Player.PlayerId == id).QuestioningTime = reader.ReadSingle();
                break;
            case 4:
                Info.Add(reader.ReadString());
                if (Info.Count > 5) Info.RemoveAt(0);
                break;
            case 5:
                Souls.Add(new(reader.ReadByte().GetPlayer()));
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != WhispererId || meeting || (seer.IsModdedClient() && !hud)) return string.Empty;
        return "<size=70%>" + string.Join('\n', Info) + (CurrentlyQuestioning.Percent > 0 ? "\n" + string.Format(Translator.GetString("WhispererQuestioning"), CurrentlyQuestioning.Name, CurrentlyQuestioning.Percent) : string.Empty) + "</size>";
    }

    private class Soul(PlayerControl player)
    {
        public bool IsQuestionAble { get; set; }
        public Vector2 Position { get; } = player.Pos();
        public PlayerControl Player { get; } = player;
        public SoulObject NetObject { get; set; }
        public float QuestioningTime { get; set; }
        public bool IsQuestioning => QuestioningTime > 0f;
    }
}