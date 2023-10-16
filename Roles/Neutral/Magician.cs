using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral;

public static class Magician
{
    private static readonly int Id = 641300;
    public static List<byte> playerIdList = new();

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem Speed;
    private static OptionItem SpeedDur;
    private static OptionItem LowKCD;
    private static OptionItem BlindDur;
    private static OptionItem BlindRadius;
    private static OptionItem SlownessRadius;
    private static OptionItem SlownessDur;
    private static OptionItem SlownessValue;
    private static OptionItem BombRadius;
    private static OptionItem BombDelay;
    private static OptionItem ClearPortalAfterMeeting;

    private static byte CardId = byte.MaxValue;

    public static Dictionary<byte, long> SlowPPL = new();
    public static Dictionary<byte, float> TempSpeeds = new();
    public static Dictionary<byte, long> BlindPPL = new();
    public static Dictionary<Vector2, long> Bombs = new();
    private static List<Vector2> PortalMarks = new();
    private static bool isSniping;
    private static byte snipeTarget = byte.MaxValue;
    private static Vector3 snipeBasePosition;
    private static bool isSpeedup;
    private static float originalSpeed;
    private static long lastTP = GetTimeStamp();

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Magician, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);

        SlownessValue = FloatOptionItem.Create(Id + 11, "MagicianSlownessValue", new(0f, 1f, 0.05f), 1f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        SlownessRadius = FloatOptionItem.Create(Id + 12, "MagicianSlownessRadius", new(0f, 10f, 0.25f), 3f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        SlownessDur = IntegerOptionItem.Create(Id + 13, "MagicianSlownessDur", new(1, 30, 1), 10, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);
        Speed = FloatOptionItem.Create(Id + 14, "MagicianSpeedup", new(0.1f, 3f, 0.05f), 1.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        SpeedDur = IntegerOptionItem.Create(Id + 15, "MagicianSpeedupDur", new(1, 20, 1), 10, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);
        LowKCD = FloatOptionItem.Create(Id + 16, "MagicianLowKCD", new(1f, 20f, 1f), 5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);
        BlindDur = IntegerOptionItem.Create(Id + 17, "MagicianBlindDur", new(1, 20, 1), 5, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);
        BlindRadius = FloatOptionItem.Create(Id + 18, "MagicianBlindRadius", new(0f, 10f, 0.25f), 3f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        ClearPortalAfterMeeting = BooleanOptionItem.Create(Id + 19, "MagicianClearPortalAfterMeeting", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician]);
        BombRadius = FloatOptionItem.Create(Id + 20, "MagicianBombRadius", new(0f, 10f, 0.25f), 3f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        BombDelay = IntegerOptionItem.Create(Id + 21, "MagicianBombDelay", new(0, 10, 1), 3, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = BooleanOptionItem.Create(Id + 22, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 23, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Magician]);
    }
    public static void Init()
    {
        playerIdList = new();
        CardId = byte.MaxValue;
        SlowPPL = new();
        BlindPPL = new();
        Bombs = new();
        PortalMarks = new();
        isSniping = false;
        isSpeedup = false;
        snipeTarget = byte.MaxValue;
        lastTP = GetTimeStamp();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        originalSpeed = Main.AllPlayerSpeed[playerId];

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Any();
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void OnCheckMurder(PlayerControl killer)
    {
        if (killer == null) return;

        CardId = (byte)IRandom.Instance.Next(1, 11);

        var sb = new StringBuilder();

        _ = sb.Append("\n\n");
        _ = sb.AppendLine(ColorString(GetRoleColor(CustomRoles.Magician), $"Card name: <color=#ffffff>{GetString($"Magician-GetIdToName-{CardId}")}</color>"));
        _ = sb.AppendLine(ColorString(GetRoleColor(CustomRoles.Magician), $"Description: <color=#ffffff>{GetString($"Magician-GetIdToDesc-{CardId}")}</color>"));
        _ = sb.AppendLine(ColorString(GetRoleColor(CustomRoles.Magician), $"Trigger by: <color=#ffffff>{(UsePets.GetBool() ? "Pet button" : "Sabotage")}</color>"));

        killer.Notify(sb.ToString(), 15f);
    }
    public static void UseCard(PlayerControl pc)
    {
        if (pc == null) return;
        if (CardId == byte.MaxValue) return;

        bool sync = false;
        pc.Notify(GetString("MagicianCardUsed"));

        switch (CardId)
        {
            case 1: // Slowness for everyone nearby
                if (TempSpeeds.Any()) RevertSpeedChanges(true);
                var list = GetPlayersInRadius(SlownessRadius.GetFloat(), pc.transform.position);
                for (int i = 0; i < list.Count; i++)
                {
                    var x = list[i];
                    if (x.PlayerId == pc.PlayerId || x == null) continue;

                    TempSpeeds.TryAdd(x.PlayerId, Main.AllPlayerSpeed[x.PlayerId]);
                    SlowPPL.TryAdd(x.PlayerId, GetTimeStamp());
                    Main.AllPlayerSpeed[x.PlayerId] = SlownessValue.GetFloat();
                    x.MarkDirtySettings();
                }
                CardId = byte.MaxValue;
                break;
            case 2: // Set KCD to x seconds
                pc.SetKillCooldown(time: LowKCD.GetFloat());
                CardId = byte.MaxValue;
                break;
            case 3: // TP to nearest vent
                var vents = Object.FindObjectsOfType<Vent>();
                var vent = vents[IRandom.Instance.Next(0, vents.Count)];
                TP(pc.NetTransform, new Vector2(vent.transform.position.x, vent.transform.position.y));
                CardId = byte.MaxValue;
                break;
            case 4: // Create Rift Maker portal
                if (PortalMarks.Count == 2) PortalMarks.Clear();
                PortalMarks.Add(pc.transform.position);
                if (PortalMarks.Count == 2) CardId = byte.MaxValue;
                break;
            case 5: // Snipe
                var sniper = pc;
                if (!isSniping)
                {
                    snipeBasePosition = sniper.transform.position;
                    isSniping = true;
                    return;
                }

                isSniping = false;
                CardId = byte.MaxValue;

                if (!AmongUsClient.Instance.AmHost || Pelican.IsEaten(pc.PlayerId) || Medic.ProtectList.Contains(pc.PlayerId)) return;
                sniper.RPCPlayCustomSound("AWP");

                var targets = GetSnipeTargets(sniper);

                if (targets.Any())
                {
                    var snipedTarget = targets.OrderBy(c => c.Value).First().Key;
                    snipeTarget = snipedTarget.PlayerId;
                    snipedTarget.CheckMurder(snipedTarget);
                    var temp = sniper.killTimer;
                    sniper.SetKillCooldown(time: Main.AllPlayerKillCooldown[sniper.PlayerId] + temp);
                    snipeTarget = 0x7F;

                    _ = targets.Remove(snipedTarget);
                    var snList = new List<byte>();
                    foreach (var otherPc in targets.Keys)
                    {
                        snList.Add(otherPc.PlayerId);
                        NotifyRoles(SpecifySeer: otherPc);
                    }
                    _ = new LateTask(
                        () =>
                        {
                            snList.Clear();
                            foreach (var otherPc in targets.Keys)
                            {
                                NotifyRoles(SpecifySeer: otherPc);
                            }
                        },
                        0.5f, "Sniper shot Notify"
                        );
                }
                break;
            case 6: // Blind everyone nearby
                var players = GetPlayersInRadius(BlindRadius.GetFloat(), pc.transform.position);
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerControl x = players[i];
                    if (x.PlayerId == pc.PlayerId || x == null) continue;

                    _ = BlindPPL.TryAdd(x.PlayerId, GetTimeStamp());
                    x.MarkDirtySettings();
                }
                CardId = byte.MaxValue;
                break;
            case 7: // Time bomb: Place, explodes after x seconds, kills everyone nearby
                _ = Bombs.TryAdd(pc.transform.position, GetTimeStamp());
                CardId = byte.MaxValue;
                break;
            case 8: // Speed up
                Main.AllPlayerSpeed[pc.PlayerId] = Speed.GetFloat();
                isSpeedup = true;
                pc.MarkDirtySettings();
                _ = new LateTask(() => { Main.AllPlayerSpeed[pc.PlayerId] = originalSpeed; pc.MarkDirtySettings(); isSpeedup = false; }, SpeedDur.GetInt(), "Revert Magician Speed");
                CardId = byte.MaxValue;
                break;
            case 9: // Call meeting
                pc.ReportDeadBody(null);
                CardId = byte.MaxValue;
                break;
            case 10: // Admin map
                _ = NameNotifyManager.Notice.Remove(pc.PlayerId);
                var rooms = ExtendedPlayerControl.GetAllPlayerLocationsCount();
                var sb = new StringBuilder();
                foreach (var location in rooms)
                {
                    _ = sb.Append($"\n<color=#00ffa5>{location.Key}:</color> {location.Value}");
                }
                pc.Notify(sb.ToString(), 10f);
                break;
            default:
                break;
        }

        if (sync) pc.SyncSettings();
    }
    public static void OnFixedUpdate(PlayerControl pc)
    {
        if (pc == null) return;
        if (pc.GetCustomRole() != CustomRoles.Magician) return;
        if (!GameStates.IsInTask) return;
        if (Pelican.IsEaten(pc.PlayerId) || pc.Data.IsDead) return;

        if (TempSpeeds.Any()) RevertSpeedChanges(false);

        if (PortalMarks.Count == 2 && lastTP + 5 < GetTimeStamp())
        {
            if (Vector2.Distance(PortalMarks[0], PortalMarks[1]) <= 4f)
            {
                pc.Notify(GetString("IncorrectMarks"));
                PortalMarks.Clear();
            }
            else
            {
                Vector2 position = pc.transform.position;

                bool isTP = false;
                Vector2 from = PortalMarks[0];

                for (int i = 0; i < PortalMarks.Count; i++)
                {
                    Vector2 mark = PortalMarks[i];
                    var dis = Vector2.Distance(mark, position);
                    if (dis > 2f) continue;

                    isTP = true;
                    from = mark;
                }

                if (isTP)
                {
                    lastTP = GetTimeStamp();
                    if (from == PortalMarks[0])
                    {
                        TP(pc.NetTransform, PortalMarks[1]);
                    }
                    else if (from == PortalMarks[1])
                    {
                        TP(pc.NetTransform, PortalMarks[0]);
                    }
                    else
                    {
                        Logger.Error($"Teleport failed - from: {from}", "MagicianTP");
                    }
                }
            }
        }

        if (BlindPPL.Any())
        {
            foreach (var x in BlindPPL.Where(x => x.Value + BlindDur.GetInt() < GetTimeStamp()))
            {
                _ = BlindPPL.Remove(x.Key);
                GetPlayerById(x.Key).MarkDirtySettings();
            }
        }

        if (Bombs.Any())
        {
            foreach (var bomb in Bombs.Where(bomb => bomb.Value + BombDelay.GetInt() < GetTimeStamp()))
            {
                bool b = false;
                var players = GetPlayersInRadius(BombRadius.GetFloat(), bomb.Key);
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerControl tg = players[i];
                    if (tg.PlayerId == pc.PlayerId)
                    {
                        b = true;
                        continue;
                    }
                    Main.PlayerStates[tg.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    tg.SetRealKiller(pc);
                    tg.RpcMurderPlayerV3(tg);
                    Medic.IsDead(tg);
                }
                _ = Bombs.Remove(bomb.Key);
                pc.Notify(GetString("MagicianBombExploded"));
                if (b) _ = new LateTask(() =>
                {
                    if (!GameStates.IsEnded)
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        pc.RpcMurderPlayerV3(pc);
                    }
                }, 0.5f, "Magician Bomb Suicide");
            }

            var sb = new StringBuilder();
            List<long> list = Bombs.Values.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                _ = sb.Append(string.Format(GetString("MagicianBombExlodesIn"), BombDelay.GetInt() - (GetTimeStamp() - list[i]) + 1));
            }
            pc.Notify(sb.ToString());
        }
    }
    public static void OnReportDeadBody()
    {
        SlowPPL.Clear();
        BlindPPL.Clear();
        Bombs.Clear();
        isSniping = false;
        if (ClearPortalAfterMeeting.GetBool()) PortalMarks.Clear();
        if (isSpeedup)
        {
            isSpeedup = false;
            Main.AllPlayerSpeed[playerIdList[0]] = originalSpeed;
        }
    }
    private static void RevertSpeedChanges(bool force)
    {
        foreach (var x in TempSpeeds.Where(x => SlowPPL[x.Key] + SlownessDur.GetInt() < GetTimeStamp() || force))
        {
            Main.AllPlayerSpeed[x.Key] = x.Value;
            _ = SlowPPL.Remove(x.Key);
            _ = TempSpeeds.Remove(x.Key);
            GetPlayerById(x.Key).MarkDirtySettings();
        }
    }
    private static Dictionary<PlayerControl, float> GetSnipeTargets(PlayerControl sniper)
    {
        var targets = new Dictionary<PlayerControl, float>();
        var snipeBasePos = snipeBasePosition;
        var snipePos = sniper.transform.position;
        var dir = (snipePos - snipeBasePos).normalized;

        snipePos -= dir;

        foreach (var target in Main.AllAlivePlayerControls)
        {
            if (target.PlayerId == sniper.PlayerId) continue;
            var target_pos = target.transform.position - snipePos;
            if (target_pos.magnitude < 1) continue;
            var target_dir = target_pos.normalized;
            var target_dot = Vector3.Dot(dir, target_dir);

            if (target_dot < 0.995) continue;

            var err = target_pos.magnitude;
            targets.Add(target, err);
        }
        return targets;
    }
}
