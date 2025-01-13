using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Impostor;

internal class Commander : RoleBase
{
    private const int Id = 643560;
    public static List<Commander> PlayerList = [];
    public static bool On;
    public static OptionItem CannotSpawnAsSoloImp;
    private static OptionItem ShapeshiftCooldown;

    private byte CommanderId;
    private Mode CurrentMode;
    public HashSet<byte> DontKillMarks = [];
    public bool IsWhistling;
    public byte MarkedPlayer;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Commander);

        CannotSpawnAsSoloImp = new BooleanOptionItem(Id + 2, "CannotSpawnAsSoloImp", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Commander]);

        ShapeshiftCooldown = new FloatOptionItem(Id + 3, "ShapeshiftCooldown", new(0f, 60f, 1f), 1f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Commander]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        IsWhistling = false;
        MarkedPlayer = byte.MaxValue;
        DontKillMarks = [];
        CurrentMode = Mode.Whistle;
        CommanderId = playerId;
        PlayerList.Add(this);
    }

    public override void Init()
    {
        On = false;
        PlayerList = [];
    }

    public override void Remove(byte playerId)
    {
        PlayerList.Remove(this);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetValue();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    private void SendRPC()
    {
        Utils.SendRPC(CustomRPC.SyncRoleData, CommanderId, 1, (int)CurrentMode, IsWhistling, MarkedPlayer);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                CurrentMode = (Mode)reader.ReadPackedInt32();
                IsWhistling = reader.ReadBoolean();
                MarkedPlayer = reader.ReadByte();
                break;
            case 2:
                DontKillMarks.Add(reader.ReadByte());
                break;
            case 3:
                DontKillMarks = [];
                break;
        }
    }

    public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
    {
        if (!Options.UsePets.GetBool()) CycleMode(physics.myPlayer);
    }

    public override void OnPet(PlayerControl pc)
    {
        CycleMode(pc);
    }

    private void CycleMode(PlayerControl pc)
    {
        CurrentMode = (Mode)(((int)CurrentMode + 1) % Enum.GetValues(typeof(Mode)).Length);
        SendRPC();
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        switch (CurrentMode)
        {
            case Mode.Whistle:
                Whistle(shapeshifter, target.Is(Team.Impostor) ? target : null);
                break;
            case Mode.Mark:
                MarkPlayer(target);
                break;
            case Mode.KillAnyone:
                if (target.Is(Team.Impostor))
                    target.Notify(Translator.GetString("CommanderKillAnyoneNotify"), 7f);
                else
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (!pc.Is(Team.Impostor) || pc.PlayerId == shapeshifter.PlayerId) continue;

                        pc.Notify(Translator.GetString("CommanderKillAnyoneNotify"), 7f);
                    }
                }

                break;
            case Mode.DontKillMark:
                if (target.Is(Team.Impostor))
                    target.Notify(Translator.GetString("CommanderDontKillAnyoneNotify"), 7f);
                else
                    MarkPlayerAsDontKill(target);

                break;
            case Mode.DontSabotage:
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (!pc.Is(Team.Impostor) || pc.PlayerId == shapeshifter.PlayerId) continue;

                    pc.Notify(Translator.GetString("CommanderDontSabotageNotify"), 7f);
                }

                break;
            case Mode.UseAbility:
                if (target.Is(Team.Impostor))
                    target.Notify(Translator.GetString("CommanderUseAbilityNotify"), 7f);
                else
                {
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (!pc.Is(Team.Impostor) || pc.PlayerId == shapeshifter.PlayerId) continue;

                        pc.Notify(Translator.GetString("CommanderUseAbilityNotify"), 7f);
                    }
                }

                break;
        }

        return false;
    }

    private void Whistle(PlayerControl commander, PlayerControl target = null)
    {
        if (IsWhistling) return;

        IsWhistling = true;

        if (target != null)
        {
            AddArrowAndNotify(target);
            return;
        }

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (!pc.Is(Team.Impostor) || pc.Is(CustomRoles.Commander)) continue;

            AddArrowAndNotify(pc);
        }

        SendRPC();
        return;

        void AddArrowAndNotify(PlayerControl pc)
        {
            TargetArrow.Add(pc.PlayerId, commander.PlayerId);
            pc.Notify(Translator.GetString("CommanderNotify"));
        }
    }

    private void MarkPlayer(PlayerControl target)
    {
        if (target == null)
        {
            MarkedPlayer = byte.MaxValue;
            return;
        }

        MarkedPlayer = target.PlayerId;
        SendRPC();
        Utils.NotifyRoles(SpecifyTarget: target);
    }

    private void MarkPlayerAsDontKill(PlayerControl target)
    {
        if (target == null) return;

        DontKillMarks.Add(target.PlayerId);
        Utils.SendRPC(CustomRPC.SyncRoleData, CommanderId, 2, target.PlayerId);
        Utils.NotifyRoles(SpecifyTarget: target);
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (lowLoad || !GameStates.IsInTask || !On || !IsWhistling) return;

        if (TargetArrow.GetArrows(pc, CommanderId) == "・")
        {
            TargetArrow.Remove(pc.PlayerId, CommanderId);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        if (Main.AllPlayerControls.Where(x => x.Is(Team.Impostor)).All(x => TargetArrow.GetArrows(x, CommanderId) == string.Empty))
        {
            IsWhistling = false;
            SendRPC();
        }
    }

    public override void OnReportDeadBody()
    {
        IsWhistling = false;
        MarkedPlayer = byte.MaxValue;
        DontKillMarks = [];
        SendRPC();
        Utils.SendRPC(CustomRPC.SyncRoleData, CommanderId, 3);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer == null || !seer.Is(Team.Impostor)) return string.Empty;

        if (seer.PlayerId == target.PlayerId && Main.PlayerStates[seer.PlayerId].Role is Commander { IsEnable: true } cm)
        {
            if (seer.IsModClient() && !hud) return string.Empty;

            string whistlingText = cm.IsWhistling ? $"\n<size=70%>{Translator.GetString("CommanderWhistling")}</size>" : string.Empty;
            return $"{string.Format(Translator.GetString("WMMode"), Translator.GetString($"Commander{cm.CurrentMode}Mode"))}{whistlingText}";
        }

        bool isTargetTarget = PlayerList.Any(x => x.MarkedPlayer == target.PlayerId);
        bool isTargetDontKill = PlayerList.Any(x => x.DontKillMarks.Contains(target.PlayerId));
        string arrowToCommander = PlayerList.Aggregate(string.Empty, (result, commander) => result + TargetArrow.GetArrows(seer, commander.CommanderId));

        if (seer.PlayerId == target.PlayerId)
        {
            if (arrowToCommander.Length > 0) return $"{Translator.GetString("Commander")} {arrowToCommander}";
        }
        else if (isTargetTarget)
            return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sprayer), Translator.GetString("CommanderTarget"));
        else if (isTargetDontKill) return Utils.ColorString(ColorUtility.TryParseHtmlString("#0daeff", out Color color) ? color : Utils.GetRoleColor(CustomRoles.TaskManager), Translator.GetString("CommanderDontKill"));

        return string.Empty;
    }

    private enum Mode
    {
        Whistle,
        Mark,
        KillAnyone,
        DontKillMark,
        DontSabotage,
        UseAbility
    }
}