using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using UnityEngine;
using static EHR.Crewmate.Randomizer;

namespace EHR.Crewmate;

internal static class EffectExtenstions
{
    public static bool IsSpeedChangingEffect(this Effect effect)
    {
        return effect is
            Effect.SuperSpeedForRandomPlayer or
            Effect.SuperSpeedForAll or
            Effect.FreezeRandomPlayer or
            Effect.InvertControls;
    }

    public static bool IsVisionChangingEffect(this Effect effect)
    {
        return effect is
            Effect.SuperVisionForRandomPlayer or
            Effect.SuperVisionForAll or
            Effect.BlindnessForRandomPlayer or
            Effect.BlindnessForAll;
    }

    public static void Apply(this Effect effect, PlayerControl randomizer)
    {
        if (!Exists) return;

        try
        {
            switch (effect)
            {
                case Effect.ShieldRandomPlayer:
                {
                    PlayerControl pc = PickRandomPlayer();
                    AddEffectForPlayer(pc, effect);
                }

                    break;
                case Effect.ShieldAll:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        AddEffectForPlayer(pc, effect);
                        NotifyAboutRNG(pc);
                    }
                }

                    break;
                case Effect.TPEveryoneToVents:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        pc.TPToRandomVent();
                        NotifyAboutRNG(pc);
                    }
                }

                    break;
                case Effect.PullEveryone:
                    Main.AllAlivePlayerControls.MassTP(randomizer.Pos(), log: true);
                    break;
                case Effect.Twist:
                {
                    List<byte> changePositionPlayers = [];

                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (changePositionPlayers.Contains(pc.PlayerId) || Pelican.IsEaten(pc.PlayerId) || pc.onLadder || pc.inVent || pc.inMovingPlat || GameStates.IsMeeting) continue;

                        PlayerControl[] filtered = Main.AllAlivePlayerControls.Where(a => !a.inVent && !Pelican.IsEaten(a.PlayerId) && !a.onLadder && a.PlayerId != pc.PlayerId && !changePositionPlayers.Contains(a.PlayerId)).ToArray();
                        if (filtered.Length == 0) break;

                        PlayerControl target = filtered.RandomElement();

                        changePositionPlayers.Add(target.PlayerId);
                        changePositionPlayers.Add(pc.PlayerId);

                        pc.RPCPlayCustomSound("Teleport");

                        Vector2 originPs = target.Pos();
                        target.TP(pc.Pos());
                        pc.TP(originPs);

                        NotifyAboutRNG(target);
                        NotifyAboutRNG(pc);
                    }
                }

                    break;
                case Effect.SuperSpeedForRandomPlayer:
                {
                    PlayerControl pc = PickRandomPlayer();
                    RevertSpeedChangesForPlayer(pc, false);
                    AddEffectForPlayer(pc, effect);
                    Main.AllPlayerSpeed[pc.PlayerId] = 5f;
                    pc.MarkDirtySettings();
                    NotifyAboutRNG(pc);
                }

                    break;
                case Effect.SuperSpeedForAll:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        RevertSpeedChangesForPlayer(pc, false);
                        AddEffectForPlayer(pc, effect);
                        Main.AllPlayerSpeed[pc.PlayerId] = 5f;
                        NotifyAboutRNG(pc);
                    }

                    Utils.MarkEveryoneDirtySettings();
                }

                    break;
                case Effect.FreezeRandomPlayer:
                {
                    PlayerControl pc = PickRandomPlayer();
                    RevertSpeedChangesForPlayer(pc, false);
                    AddEffectForPlayer(pc, effect);
                    Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                    pc.MarkDirtySettings();
                    NotifyAboutRNG(pc);

                    if (pc.AmOwner)
                        Achievements.Type.TooCold.CompleteAfterGameEnd();
                }

                    break;
                case Effect.SuperVisionForRandomPlayer:
                {
                    PlayerControl pc = PickRandomPlayer();
                    RevertVisionChangesForPlayer(pc, false);
                    AddEffectForPlayer(pc, effect);
                    pc.MarkDirtySettings();
                    NotifyAboutRNG(pc);
                }

                    break;
                case Effect.SuperVisionForAll:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        RevertVisionChangesForPlayer(pc, false);
                        AddEffectForPlayer(pc, effect);
                        NotifyAboutRNG(pc);
                    }

                    Utils.MarkEveryoneDirtySettings();
                }

                    break;
                case Effect.BlindnessForRandomPlayer:
                {
                    PlayerControl pc = PickRandomPlayer();
                    RevertVisionChangesForPlayer(pc, false);
                    AddEffectForPlayer(pc, effect);
                    pc.MarkDirtySettings();
                    NotifyAboutRNG(pc);
                }

                    break;
                case Effect.BlindnessForAll:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        RevertVisionChangesForPlayer(pc, false);
                        AddEffectForPlayer(pc, effect);
                        NotifyAboutRNG(pc);
                    }

                    Utils.MarkEveryoneDirtySettings();
                }

                    break;
                case Effect.AllKCDsReset:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (pc.HasKillButton() && pc.CanUseKillButton())
                        {
                            pc.SetKillCooldown();
                            NotifyAboutRNG(pc);
                        }
                    }
                }

                    break;
                case Effect.AllKCDsTo0:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (pc.HasKillButton() && pc.CanUseKillButton())
                        {
                            pc.SetKillCooldown(0.1f);
                            NotifyAboutRNG(pc);
                        }
                    }
                }

                    break;
                case Effect.Meeting when TimeSinceLastMeeting > Math.Max(Main.NormalOptions.EmergencyCooldown, 30):
                {
                    PlayerControl pc = PickRandomPlayer();
                    pc.CmdReportDeadBody(null);
                }

                    break;
                case Effect.Rift:
                    Rifts.TryAdd(PickRandomPlayer().Pos(), PickRandomPlayer().Pos());

                    try
                    {
                        var riftsToRemove = new List<Vector2>();

                        foreach (KeyValuePair<Vector2, Vector2> rift1 in Rifts)
                        {
                            foreach (KeyValuePair<Vector2, Vector2> rift2 in Rifts)
                            {
                                if (rift1.Key != rift2.Key && Vector2.Distance(rift1.Key, rift2.Key) <= 4f)
                                    riftsToRemove.Add(rift2.Key);
                            }
                        }

                        foreach (Vector2 rift in riftsToRemove) Rifts.Remove(rift);
                    }
                    catch (Exception ex) { Logger.Exception(ex, "Randomizer Rift Manager"); }

                    break;
                case Effect.TimeBomb:
                    Bombs.TryAdd(PickRandomPlayer().Pos(), (Utils.TimeStamp, IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration)));
                    Utils.SendRPC(CustomRPC.SyncRoleData, randomizer.PlayerId, 1, Bombs.Last().Key, Bombs.Last().Value.PlaceTimeStamp, Bombs.Last().Value.ExplosionDelay);
                    break;
                case Effect.Tornado:
                    Tornado.SpawnTornado(PickRandomPlayer());
                    break;
                case Effect.RevertToBaseRole when TimeSinceLastMeeting > 40f: // To make this less frequent than the others
                {
                    PlayerControl pc = PickRandomPlayer();
                    if (pc.PlayerId == randomizer.PlayerId) break;

                    pc.RpcSetCustomRole(pc.GetCustomRole().GetErasedRole());
                    pc.SyncSettings();
                    NotifyAboutRNG(pc);
                }

                    break;
                case Effect.InvertControls:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        RevertSpeedChangesForPlayer(pc, false);
                        AddEffectForPlayer(pc, effect);
                        Main.AllPlayerSpeed[pc.PlayerId] = -AllPlayerDefaultSpeed[pc.PlayerId];
                        NotifyAboutRNG(pc);
                    }

                    Utils.MarkEveryoneDirtySettings();
                }

                    break;
                case Effect.AddonAssign:
                {
                    CustomRoles[] addons = Enum.GetValues<CustomRoles>().Where(x => x.IsAdditionRole() && x != CustomRoles.NotAssigned).ToArray();
                    PlayerControl pc = PickRandomPlayer();
                    CustomRoles addon = addons.RandomElement();
                    if (Main.PlayerStates[pc.PlayerId].SubRoles.Contains(addon)) break;

                    Main.PlayerStates[pc.PlayerId].SetSubRole(addon);
                    pc.MarkDirtySettings();
                    NotifyAboutRNG(pc);
                }

                    break;
                case Effect.AddonRemove:
                {
                    PlayerControl pc = PickRandomPlayer();
                    List<CustomRoles> addons = Main.PlayerStates[pc.PlayerId].SubRoles;
                    if (addons.Count == 0) break;

                    CustomRoles addon = addons.RandomElement();
                    Main.PlayerStates[pc.PlayerId].RemoveSubRole(addon);
                    pc.MarkDirtySettings();
                    NotifyAboutRNG(pc);
                }

                    break;
                case Effect.HandcuffRandomPlayer:
                {
                    PlayerControl pc = PickRandomPlayer();
                    AddEffectForPlayer(pc, effect);
                    pc.BlockRole(IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration));
                    NotifyAboutRNG(pc);
                }

                    break;
                case Effect.HandcuffAll:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        AddEffectForPlayer(pc, effect);
                        pc.BlockRole(IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration));
                        NotifyAboutRNG(pc);
                    }
                }

                    break;
                case Effect.DonutForAll:
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls) DonutDelivery.RandomNotifyTarget(pc);
                }

                    break;
                case Effect.AllDoorsOpen:
                    try { DoorsReset.OpenAllDoors(); }
                    catch { }

                    break;
                case Effect.AllDoorsClose:
                    try { DoorsReset.CloseAllDoors(); }
                    catch { }

                    break;
                case Effect.SetDoorsRandomly:
                    try { DoorsReset.OpenOrCloseAllDoorsRandomly(); }
                    catch { }

                    break;
                case Effect.Patrol:
                {
                    PlayerControl pc = PickRandomPlayer();
                    var state = new PatrollingState(pc.PlayerId, IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration), RandomFloat, pc);
                    Sentinel.PatrolStates.Add(state);
                    state.StartPatrolling();
                }

                    break;
                case Effect.GhostPlayer when TimeSinceLastMeeting > Options.AdjustedDefaultKillCooldown:
                {
                    PlayerControl killer = PickRandomPlayer();
                    PlayerControl[] allPc = Main.AllAlivePlayerControls.Where(x => x.PlayerId != killer.PlayerId).ToArray();
                    if (allPc.Length == 0) break;

                    PlayerControl target = allPc.RandomElement();
                    Impostor.Lightning.CheckLightningMurder(killer, target, true);
                    NotifyAboutRNG(target);
                }

                    break;
                case Effect.Camouflage when TimeSinceLastMeeting > Camouflager.CamouflageCooldown.GetFloat():
                    var camouflager = new Camouflager();
                    camouflager.Init();
                    camouflager.Add(randomizer.PlayerId);
                    camouflager.OnShapeshift(randomizer, randomizer, true);
                    LateTask.New(camouflager.OnReportDeadBody, IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration), "Randomizer Revert Camo");
                    break;
                case Effect.Deathpact:
                {
                    var deathpact = new Deathpact();
                    deathpact.Init();
                    deathpact.Add(randomizer.PlayerId);
                    deathpact.OnShapeshift(randomizer, PickRandomPlayer(), true);
                }

                    break;
                case Effect.DevourRandomPlayer:
                {
                    var devourer = new Devourer();
                    devourer.Init();
                    devourer.Add(randomizer.PlayerId);
                    devourer.OnShapeshift(randomizer, PickRandomPlayer(), true);
                }

                    break;
                case Effect.Duel:
                {
                    PlayerControl pc1 = PickRandomPlayer();
                    PlayerControl[] allPc = Main.AllAlivePlayerControls.Where(x => x.CanUseKillButton() && x.PlayerId != pc1.PlayerId).ToArray();
                    if (allPc.Length == 0) break;

                    PlayerControl pc2 = allPc.RandomElement();
                    var duellist = new Duellist();
                    duellist.OnShapeshift(pc1, pc2, true);
                }

                    break;
                default:
                    Logger.Info("Effect wasn't applied", "Randomizer");
                    break;
            }
        }
        catch (Exception ex) { Logger.Exception(ex, "Randomizer"); }
    }
}

internal class Randomizer : RoleBase
{
    public enum Effect
    {
        ShieldRandomPlayer,
        ShieldAll,
        TPEveryoneToVents,
        PullEveryone,
        Twist,
        SuperSpeedForRandomPlayer,
        SuperSpeedForAll,
        FreezeRandomPlayer,
        SuperVisionForRandomPlayer,
        SuperVisionForAll,
        BlindnessForRandomPlayer,
        BlindnessForAll,
        AllKCDsReset,
        AllKCDsTo0,
        Meeting,
        Rift,
        TimeBomb,
        Tornado,
        RevertToBaseRole,
        InvertControls,
        AddonAssign,
        AddonRemove,
        HandcuffRandomPlayer,
        HandcuffAll,
        DonutForAll,
        AllDoorsOpen,
        AllDoorsClose,
        SetDoorsRandomly,
        Patrol, // Sentinel ability
        GhostPlayer, // Lightning ability
        Camouflage,
        Deathpact,
        DevourRandomPlayer,
        Duel
    }

    private static List<byte> PlayerIdList = [];

    private static OptionItem EffectFrequencyOpt;
    private static OptionItem EffectDurMin;
    private static OptionItem EffectDurMax;
    private static OptionItem NotifyOpt;

    private static int EffectFrequency;
    public static int MinimumEffectDuration;
    public static int MaximumEffectDuration;
    private static bool Notify;

    private static Dictionary<byte, Dictionary<Effect, (long StartTimeStamp, int Duration)>> CurrentEffects = [];
    public static Dictionary<byte, float> AllPlayerDefaultSpeed = [];

    public static Dictionary<Vector2, Vector2> Rifts = [];
    public static Dictionary<Vector2, (long PlaceTimeStamp, int ExplosionDelay)> Bombs = [];

    public static float TimeSinceLastMeeting;
    private static Dictionary<byte, long> LastEffectPick = [];
    private static Dictionary<byte, long> LastTP = [];
    private static long LastDeathEffect;

    public static bool Exists;

    private static int Id => 643490;

    private static string RNGString => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Randomizer), Translator.GetString("RNGHasSpoken"));

    public static float RandomFloat => IRandom.Instance.Next(0, 5) + (IRandom.Instance.Next(0, 10) / 10f);

    public override bool IsEnable => Exists;

    public static void NotifyAboutRNG(PlayerControl pc)
    {
        if (!Notify) return;

        pc.Notify(RNGString, IRandom.Instance.Next(2, 7), log: false);
    }

    public static bool IsShielded(PlayerControl pc)
    {
        return CurrentEffects.TryGetValue(pc.PlayerId, out Dictionary<Effect, (long StartTimeStamp, int Duration)> effects) && (effects.ContainsKey(Effect.ShieldRandomPlayer) || effects.ContainsKey(Effect.ShieldAll));
    }

    public static bool HasSuperVision(PlayerControl pc)
    {
        return CurrentEffects.TryGetValue(pc.PlayerId, out Dictionary<Effect, (long StartTimeStamp, int Duration)> effects) && (effects.ContainsKey(Effect.SuperVisionForRandomPlayer) || effects.ContainsKey(Effect.SuperVisionForAll));
    }

    public static bool IsBlind(PlayerControl pc)
    {
        return CurrentEffects.TryGetValue(pc.PlayerId, out Dictionary<Effect, (long StartTimeStamp, int Duration)> effects) && (effects.ContainsKey(Effect.BlindnessForRandomPlayer) || effects.ContainsKey(Effect.BlindnessForAll));
    }

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Randomizer);

        EffectFrequencyOpt = new IntegerOptionItem(Id + 2, "RandomizerEffectFrequency", new(1, 90, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
            .SetValueFormat(OptionFormat.Seconds);

        EffectDurMin = new IntegerOptionItem(Id + 3, "RandomizerEffectDurMin", new(1, 90, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
            .SetValueFormat(OptionFormat.Seconds);

        EffectDurMax = new IntegerOptionItem(Id + 4, "RandomizerEffectDurMax", new(1, 90, 1), 15, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
            .SetValueFormat(OptionFormat.Seconds);

        NotifyOpt = new BooleanOptionItem(Id + 5, "RandomizerNotifyOpt", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer]);
    }

    public override void Init()
    {
        long now = Utils.TimeStamp;

        PlayerIdList = [];
        CurrentEffects = [];
        AllPlayerDefaultSpeed = [];
        Rifts = [];
        Bombs = [];

        TimeSinceLastMeeting = 0;
        LastEffectPick = [];
        LastTP = [];
        Main.AllPlayerControls.Do(x => LastTP[x.PlayerId] = now);
        LastDeathEffect = 0;

        EffectFrequency = EffectFrequencyOpt.GetInt();
        MinimumEffectDuration = EffectDurMin.GetInt();
        MaximumEffectDuration = EffectDurMax.GetInt();
        Notify = NotifyOpt.GetBool();

        Exists = false;
    }

    public override void Add(byte playerId)
    {
        Exists = true;
        PlayerIdList.Add(playerId);
        AllPlayerDefaultSpeed = Main.AllPlayerSpeed.ToDictionary(x => x.Key, x => x.Value);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public static PlayerControl PickRandomPlayer()
    {
        PlayerControl[] allPc = Main.AllAlivePlayerControls;
        if (allPc.Length == 0) return null;

        PlayerControl pc = allPc.RandomElement();
        return pc;
    }

    public static void AddEffectForPlayer(PlayerControl pc, Effect effect)
    {
        if (pc == null || !Exists) return;

        if (!CurrentEffects.ContainsKey(pc.PlayerId)) CurrentEffects[pc.PlayerId] = [];

        int duration = IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration + 1);
        CurrentEffects[pc.PlayerId].TryAdd(effect, (Utils.TimeStamp, duration));
    }

    private static Effect PickRandomEffect(byte id)
    {
        long now = Utils.TimeStamp;

        LastEffectPick[id] = now;

        Effect[] allEffects = Enum.GetValues<Effect>();
        Effect effect = allEffects.RandomElement();

        if (effect == Effect.GhostPlayer)
        {
            if (LastDeathEffect + 60 > now) return Effect.AddonRemove;

            LastDeathEffect = now;
        }

        Logger.Info($"Effect: {effect}", "Randomizer");

        return effect;
    }

    public static void RevertSpeedChangesForPlayer(PlayerControl pc, bool sync)
    {
        try
        {
            if (pc == null || !Exists) return;

            if (CurrentEffects.TryGetValue(pc.PlayerId, out Dictionary<Effect, (long StartTimeStamp, int Duration)> effects) && effects.Any(x => x.Key.IsSpeedChangingEffect()))
            {
                Main.AllPlayerSpeed[pc.PlayerId] = AllPlayerDefaultSpeed[pc.PlayerId];
                if (sync) pc.MarkDirtySettings();

                IEnumerable<Effect> keys = effects.Keys.AsEnumerable();
                keys.DoIf(x => x.IsSpeedChangingEffect(), x => effects.Remove(x), false);
            }
        }
        catch (Exception e) { Logger.Exception(e, "Randomizer"); }
    }

    public static void RevertVisionChangesForPlayer(PlayerControl pc, bool sync)
    {
        try
        {
            if (pc == null || !Exists) return;

            if (CurrentEffects.TryGetValue(pc.PlayerId, out Dictionary<Effect, (long StartTimeStamp, int Duration)> effects) && effects.Any(x => x.Key.IsVisionChangingEffect()))
            {
                IEnumerable<Effect> keys = effects.Keys.AsEnumerable();
                keys.DoIf(x => x.IsVisionChangingEffect(), x => effects.Remove(x), false);
                if (sync) pc.MarkDirtySettings();
            }
        }
        catch (Exception e) { Logger.Exception(e, "Randomizer"); }
    }

    public static void OnAnyoneDeath(PlayerControl pc)
    {
        try
        {
            if (!Exists) return;

            RevertSpeedChangesForPlayer(pc, false);
            RevertVisionChangesForPlayer(pc, false);

            Main.AllPlayerSpeed[pc.PlayerId] = AllPlayerDefaultSpeed[pc.PlayerId];
            pc.MarkDirtySettings();
        }
        catch (Exception e) { Logger.Exception(e, "Randomizer"); }
    }

    public override void OnReportDeadBody()
    {
        if (!IsEnable) return;

        TimeSinceLastMeeting = 0;
        LastDeathEffect = Utils.TimeStamp;
        Rifts.Clear();
        Bombs.Clear();
        Utils.SendRPC(CustomRPC.SyncRoleData, PlayerIdList[0], 3);

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            RevertSpeedChangesForPlayer(pc, false);
            RevertVisionChangesForPlayer(pc, false);
        }
    }

    public override void AfterMeetingTasks()
    {
        if (!IsEnable) return;

        TimeSinceLastMeeting = 0;
        LastDeathEffect = Utils.TimeStamp;
    }

    public override void OnGlobalFixedUpdate(PlayerControl player, bool lowLoad)
    {
        try
        {
            if (lowLoad || !Exists || !GameStates.IsInTask || Bombs.Count == 0 || Main.HasJustStarted) return;

            long now = Utils.TimeStamp;
            PlayerControl randomizer = Utils.GetPlayerById(PlayerIdList.FirstOrDefault());

            foreach (KeyValuePair<Vector2, (long PlaceTimeStamp, int ExplosionDelay)> bomb in Bombs)
            {
                if (bomb.Value.PlaceTimeStamp + bomb.Value.ExplosionDelay < now)
                {
                    IEnumerable<PlayerControl> players = Utils.GetPlayersInRadius(RandomFloat, bomb.Key);
                    foreach (PlayerControl pc in players) pc.Suicide(PlayerState.DeathReason.RNG, randomizer);

                    Bombs.Remove(bomb.Key);
                    Utils.SendRPC(CustomRPC.SyncRoleData, randomizer.PlayerId, 2, bomb.Key);
                }
            }
        }
        catch (Exception ex) { Logger.Exception(ex, "Randomizer"); }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Bombs.TryAdd(NetHelpers.ReadVector2(reader), (long.Parse(reader.ReadString()), reader.ReadPackedInt32()));
                break;
            case 2:
                Bombs.Remove(NetHelpers.ReadVector2(reader));
                break;
            case 3:
                Bombs.Clear();
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer == null || seer.PlayerId != target.PlayerId || Bombs.Count == 0) return string.Empty;

        KeyValuePair<Vector2, (long PlaceTimeStamp, int ExplosionDelay)> bomb = Bombs.FirstOrDefault(x => Vector2.Distance(x.Key, seer.Pos()) <= 5f);
        long time = bomb.Value.ExplosionDelay - (Utils.TimeStamp - bomb.Value.PlaceTimeStamp) + 1;
        return time < 0 ? string.Empty : $"<#ffff00>⚠ {time}</color>";
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        try
        {
            if (!IsEnable || !GameStates.IsInTask || Main.HasJustStarted) return;

            TimeSinceLastMeeting += Time.fixedDeltaTime;

            if (pc == null || !pc.IsAlive() || TimeSinceLastMeeting <= 10f) return;

            long now = Utils.TimeStamp;

            if (LastEffectPick.TryGetValue(pc.PlayerId, out long ts) && ts + EffectFrequency <= now)
            {
                Effect effect = PickRandomEffect(pc.PlayerId);
                effect.Apply(pc);
            }
            else
                LastEffectPick.TryAdd(pc.PlayerId, now);
        }
        catch (Exception ex) { Logger.Exception(ex, "Randomizer"); }
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        try
        {
            if (!IsEnable || !GameStates.IsInTask || Main.HasJustStarted) return;

            long now = Utils.TimeStamp;
            if (LastTP[pc.PlayerId] + 5 > now) return;

            Vector2 pos = pc.Pos();

            foreach (KeyValuePair<Vector2, Vector2> rift in Rifts)
            {
                if (Vector2.Distance(pos, rift.Key) < 2f)
                {
                    pc.TP(rift.Value);
                    LastTP[pc.PlayerId] = now;
                    return;
                }

                if (Vector2.Distance(pos, rift.Value) < 2f)
                {
                    pc.TP(rift.Key);
                    LastTP[pc.PlayerId] = now;
                    return;
                }
            }
        }
        catch (Exception ex) { Logger.Exception(ex, "Randomizer"); }
    }

    public static void OnFixedUpdateForPlayers(PlayerControl pc)
    {
        try
        {
            if (!Exists || pc == null || !pc.IsAlive() || !GameStates.IsInTask || Main.HasJustStarted) return;

            if (CurrentEffects.TryGetValue(pc.PlayerId, out Dictionary<Effect, (long StartTimeStamp, int Duration)> effects))
            {
                long now = Utils.TimeStamp;

                foreach (KeyValuePair<Effect, (long StartTimeStamp, int Duration)> item in effects.ToArray())
                {
                    if (item.Value.StartTimeStamp + item.Value.Duration < now)
                    {
                        if (item.Key.IsVisionChangingEffect()) RevertVisionChangesForPlayer(pc, true);

                        if (item.Key.IsSpeedChangingEffect()) RevertSpeedChangesForPlayer(pc, true);

                        effects.Remove(item.Key);
                    }
                }
            }
        }
        catch (Exception ex) { Logger.Exception(ex, "Randomizer"); }
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