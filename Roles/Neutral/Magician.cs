using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Neutral;

public class Magician : RoleBase
{
    private const int Id = 641300;
    public static List<byte> playerIdList = [];

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

    public static Dictionary<byte, long> SlowPPL = [];
    public static Dictionary<byte, float> TempSpeeds = [];
    public static Dictionary<byte, long> BlindPPL = [];
    public static Dictionary<Vector2, long> Bombs = [];
    private static List<Vector2> PortalMarks = [];
    private static bool isSniping;
    private static Vector3 snipeBasePosition;
    private static bool isSpeedup;
    private static float originalSpeed;
    private static long lastTP = TimeStamp;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Magician);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);

        SlownessValue = new FloatOptionItem(Id + 11, "MagicianSlownessValue", new(0f, 1f, 0.05f), 1f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        SlownessRadius = new FloatOptionItem(Id + 12, "MagicianSlownessRadius", new(0f, 10f, 0.25f), 3f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        SlownessDur = new IntegerOptionItem(Id + 13, "MagicianSlownessDur", new(1, 30, 1), 10, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);
        Speed = new FloatOptionItem(Id + 14, "MagicianSpeedup", new(0.1f, 3f, 0.05f), 1.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        SpeedDur = new IntegerOptionItem(Id + 15, "MagicianSpeedupDur", new(1, 20, 1), 10, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);
        LowKCD = new FloatOptionItem(Id + 16, "MagicianLowKCD", new(1f, 20f, 1f), 5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);
        BlindDur = new IntegerOptionItem(Id + 17, "MagicianBlindDur", new(1, 20, 1), 5, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);
        BlindRadius = new FloatOptionItem(Id + 18, "MagicianBlindRadius", new(0f, 10f, 0.25f), 3f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        ClearPortalAfterMeeting = new BooleanOptionItem(Id + 19, "MagicianClearPortalAfterMeeting", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician]);
        BombRadius = new FloatOptionItem(Id + 20, "MagicianBombRadius", new(0f, 10f, 0.25f), 3f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Multiplier);
        BombDelay = new IntegerOptionItem(Id + 21, "MagicianBombDelay", new(0, 10, 1), 3, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 22, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician]);
        HasImpostorVision = new BooleanOptionItem(Id + 23, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Magician]);
    }

    public override void Init()
    {
        playerIdList = [];
        CardId = byte.MaxValue;
        SlowPPL = [];
        BlindPPL = [];
        Bombs = [];
        PortalMarks = [];
        isSniping = false;
        isSpeedup = false;
        lastTP = TimeStamp;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        originalSpeed = Main.AllPlayerSpeed[playerId];

        isSniping = false;
        isSpeedup = false;
        lastTP = TimeStamp;
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override bool CanUseSabotage(PlayerControl pc) => pc.IsAlive() && !(UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool());

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
            AURoleOptions.PhantomCooldown = 1f;
        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
            AURoleOptions.ShapeshifterCooldown = 1f;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return;

        CardId = (byte)IRandom.Instance.Next(1, 11);

        var sb = new StringBuilder();

        sb.Append("\n\n");
        sb.AppendLine(ColorString(GetRoleColor(CustomRoles.Magician), $"Card name: <color=#ffffff>{GetString($"Magician-GetIdToName-{CardId}")}</color>"));
        sb.AppendLine(ColorString(GetRoleColor(CustomRoles.Magician), $"Description: <color=#ffffff>{GetString($"Magician-GetIdToDesc-{CardId}")}</color>"));
        sb.AppendLine(ColorString(GetRoleColor(CustomRoles.Magician), $"Trigger by: <color=#ffffff>{(UsePets.GetBool() ? "Pet button" : UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool() ? "Vanish button" : UseUnshiftTrigger.GetBool() && UseUnshiftTriggerForNKs.GetBool() ? "Shapeshift button" : "Sabotage")}</color>"));

        killer.Notify(sb.ToString(), 15f);
    }

    public override void OnPet(PlayerControl pc)
    {
        UseCard(pc);
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        UseCard(pc);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        UseCard(pc);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifter == null) return false;
        if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return false;
        UseCard(shapeshifter);
        return false;
    }

    public static void UseCard(PlayerControl pc)
    {
        if (pc == null) return;
        if (CardId == byte.MaxValue) return;

        bool sync = false;

        switch (CardId)
        {
            case 1: // Slowness for everyone nearby
                if (TempSpeeds.Count > 0) RevertSpeedChanges(true);
                var list = GetPlayersInRadius(SlownessRadius.GetFloat(), pc.Pos());
                foreach (var x in list)
                {
                    if (x.PlayerId == pc.PlayerId) continue;

                    TempSpeeds.TryAdd(x.PlayerId, Main.AllPlayerSpeed[x.PlayerId]);
                    SlowPPL.TryAdd(x.PlayerId, TimeStamp);
                    Main.AllPlayerSpeed[x.PlayerId] = SlownessValue.GetFloat();
                    x.MarkDirtySettings();
                }

                CardId = byte.MaxValue;
                pc.Notify(GetString("MagicianCardUsed"));
                break;
            case 2: // Set KCD to x seconds
                pc.SetKillCooldown(time: LowKCD.GetFloat());
                CardId = byte.MaxValue;
                break;
            case 3: // TP to random vent
                pc.TPtoRndVent();
                CardId = byte.MaxValue;
                break;
            case 4: // Create Rift Maker portal
                if (PortalMarks.Count == 2) PortalMarks.Clear();
                PortalMarks.Add(pc.Pos());
                if (PortalMarks.Count == 2) CardId = byte.MaxValue;
                break;
            case 5: // Snipe
                if (!isSniping)
                {
                    snipeBasePosition = pc.transform.position;
                    isSniping = true;
                    pc.Notify(GetString("MarkDone"));
                    return;
                }

                isSniping = false;
                CardId = byte.MaxValue;
                pc.Notify(GetString("MagicianCardUsed"));

                if (!AmongUsClient.Instance.AmHost || Pelican.IsEaten(pc.PlayerId) || Medic.ProtectList.Contains(pc.PlayerId)) return;
                pc.RPCPlayCustomSound("AWP");

                var targets = GetSnipeTargets(pc);

                if (targets.Count > 0)
                {
                    var snipedTarget = targets.MinBy(c => c.Value).Key;
                    snipedTarget.CheckMurder(snipedTarget);
                    var temp = Main.KillTimers[pc.PlayerId];
                    pc.SetKillCooldown(time: Main.AllPlayerKillCooldown[pc.PlayerId] + temp);

                    targets.Remove(snipedTarget);
                    PlayerControl[] list1 = [.. targets.Keys];
                    foreach (PlayerControl x in list1)
                    {
                        NotifyRoles(SpecifySeer: x);
                    }

                    LateTask.New(() =>
                    {
                        foreach (PlayerControl x in list1)
                        {
                            NotifyRoles(SpecifySeer: x);
                        }
                    }, 0.5f, "Sniper shot Notify");
                }

                break;
            case 6: // Blind everyone nearby
                var players = GetPlayersInRadius(BlindRadius.GetFloat(), pc.Pos());
                foreach (PlayerControl x in players)
                {
                    if (x.PlayerId == pc.PlayerId) continue;

                    BlindPPL.TryAdd(x.PlayerId, TimeStamp);
                    x.MarkDirtySettings();
                }

                CardId = byte.MaxValue;
                pc.Notify(GetString("MagicianCardUsed"));
                break;
            case 7: // Time bomb: Place, explodes after x seconds, kills everyone nearby
                Bombs.TryAdd(pc.Pos(), TimeStamp);
                CardId = byte.MaxValue;
                pc.Notify(GetString("MagicianCardUsed"));
                break;
            case 8: // Speed up
                Main.AllPlayerSpeed[pc.PlayerId] = Speed.GetFloat();
                isSpeedup = true;
                sync = true;
                LateTask.New(() =>
                {
                    Main.AllPlayerSpeed[pc.PlayerId] = originalSpeed;
                    pc.MarkDirtySettings();
                    isSpeedup = false;
                }, SpeedDur.GetInt(), "Revert Magician Speed");
                CardId = byte.MaxValue;
                break;
            case 9: // Call meeting
                pc.ReportDeadBody(null);
                CardId = byte.MaxValue;
                break;
            case 10: // Admin map
                NameNotifyManager.Notice.Remove(pc.PlayerId);
                var rooms = GetAllPlayerLocationsCount();
                var sb = new StringBuilder();
                foreach (var location in rooms)
                {
                    sb.Append($"\n<color=#00ffa5>{location.Key}:</color> {location.Value}");
                }

                pc.Notify(sb.ToString(), 10f);
                break;
            default:
                Logger.Error("Invalid Card ID", "Magician");
                break;
        }

        if (sync) pc.MarkDirtySettings();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc == null) return;
        if (!GameStates.IsInTask) return;
        if (Pelican.IsEaten(pc.PlayerId) || pc.Data.IsDead) return;

        if (TempSpeeds.Count > 0) RevertSpeedChanges(false);

        if (PortalMarks.Count == 2 && lastTP + 5 < TimeStamp)
        {
            if (Vector2.Distance(PortalMarks[0], PortalMarks[1]) <= 4f)
            {
                pc.Notify(GetString("IncorrectMarks"));
                PortalMarks.Clear();
            }
            else
            {
                Vector2 position = pc.Pos();

                bool isTP = false;
                Vector2 from = PortalMarks[0];

                foreach (Vector2 mark in PortalMarks.ToArray())
                {
                    var dis = Vector2.Distance(mark, position);
                    if (dis > 2f) continue;

                    isTP = true;
                    from = mark;
                }

                if (isTP)
                {
                    lastTP = TimeStamp;
                    if (from == PortalMarks[0])
                    {
                        pc.TP(PortalMarks[1]);
                    }
                    else if (from == PortalMarks[1])
                    {
                        pc.TP(PortalMarks[0]);
                    }
                    else
                    {
                        Logger.Error($"Teleport failed - from: {from}", "MagicianTP");
                    }
                }
            }
        }

        if (BlindPPL.Count > 0)
        {
            foreach (var x in BlindPPL.Where(x => x.Value + BlindDur.GetInt() < TimeStamp))
            {
                BlindPPL.Remove(x.Key);
                GetPlayerById(x.Key).MarkDirtySettings();
            }
        }

        if (Bombs.Count > 0)
        {
            foreach (var bomb in Bombs.Where(bomb => bomb.Value + BombDelay.GetInt() < TimeStamp))
            {
                bool b = false;
                var players = GetPlayersInRadius(BombRadius.GetFloat(), bomb.Key);
                foreach (PlayerControl tg in players)
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
                    }, 0.5f, "Magician Bomb Suicide");
            }

            var sb = new StringBuilder();
            long[] list = [.. Bombs.Values];
            foreach (long x in list)
            {
                sb.Append(string.Format(GetString("MagicianBombExlodesIn"), BombDelay.GetInt() - (TimeStamp - x) + 1));
            }

            pc.Notify(sb.ToString());
        }
    }

    public override void OnReportDeadBody()
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
        foreach (var x in TempSpeeds.Where(x => SlowPPL[x.Key] + SlownessDur.GetInt() < TimeStamp || force))
        {
            Main.AllPlayerSpeed[x.Key] = x.Value;
            SlowPPL.Remove(x.Key);
            TempSpeeds.Remove(x.Key);
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

        foreach (PlayerControl target in Main.AllAlivePlayerControls)
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