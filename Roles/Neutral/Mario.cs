using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral;

internal class Mario : RoleBase
{
    public static Dictionary<byte, int> MarioVentCount = [];

    public static bool On;
    public override bool IsEnable => On;

    private static OptionItem MarioVentCD;

    private static Dictionary<MapNames, OptionItem> MapWinCounts = [];
    private static int MarioVentNumWin;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(18300, TabGroup.NeutralRoles, CustomRoles.Mario);

        MarioVentCD = new FloatOptionItem(18311, "VentCooldown", new(0f, 180f, 1f), 0f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mario])
            .SetValueFormat(OptionFormat.Seconds);

        MapWinCounts = Enum.GetValues<MapNames>().ToDictionary(x => x, x => new IntegerOptionItem(18312 + (int)x, $"Mario.NumVentsToWinOn.{x}", new(0, 900, 5), 80, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Mario])
            .SetValueFormat(OptionFormat.Times));
    }

    public override void Add(byte playerId)
    {
        On = true;
        MarioVentCount[playerId] = 0;
        MarioVentNumWin = MapWinCounts[Main.CurrentMap].GetInt();
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = MarioVentCD.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return Utils.ColorString(Color.white, $"<color=#777777>-</color> {MarioVentCount.GetValueOrDefault(playerId, 0)}/{MarioVentNumWin}");
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton.buttonLabelText.text = Translator.GetString("MarioVentButtonText");
        hud.AbilityButton?.SetUsesRemaining(MarioVentNumWin - MarioVentCount.GetValueOrDefault(id, 0));
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        byte playerId = pc.PlayerId;

        if (MarioVentCount[playerId] > MarioVentNumWin && GameStates.IsInTask)
        {
            MarioVentCount[playerId] = MarioVentNumWin;
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario);
            CustomWinnerHolder.WinnerIds.Add(playerId);
        }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        MarioVentCount.TryAdd(pc.PlayerId, 0);
        MarioVentCount[pc.PlayerId]++;
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        pc.RPCPlayCustomSound("MarioJump");

        if (AmongUsClient.Instance.AmHost && MarioVentCount[pc.PlayerId] >= MarioVentNumWin)
        {
            pc.RPCPlayCustomSound("MarioCoin");
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario);
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
        }
    }
}