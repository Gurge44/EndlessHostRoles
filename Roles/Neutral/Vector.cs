using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral;

internal class Vector : RoleBase
{
    public static Dictionary<byte, int> VectorVentCount = [];

    public static bool On;

    private static OptionItem VectorVentCD;

    private static Dictionary<MapNames, OptionItem> MapWinCounts = [];
    private static int VectorVentNumWin;
    
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(18300, TabGroup.NeutralRoles, CustomRoles.Vector);

        VectorVentCD = new FloatOptionItem(18311, "VentCooldown", new(0f, 180f, 1f), 0f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vector])
            .SetValueFormat(OptionFormat.Seconds);

        MapWinCounts = Enum.GetValues<MapNames>().ToDictionary(x => x, x => new IntegerOptionItem(18312 + (int)x, $"Vector.NumVentsToWinOn.{x}", new(0, 900, 5), 80, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vector])
            .SetValueFormat(OptionFormat.Times));
    }

    public override void Add(byte playerId)
    {
        On = true;
        VectorVentCount[playerId] = 0;
        VectorVentNumWin = MapWinCounts[SubmergedCompatibility.IsSubmerged() ? MapNames.Airship : Main.CurrentMap].GetInt();
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = VectorVentCD.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return Utils.ColorString(Color.white, $"<color=#777777>-</color> {VectorVentCount.GetValueOrDefault(playerId, 0)}/{VectorVentNumWin}");
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton.buttonLabelText.text = Translator.GetString("VectorVentButtonText");
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        byte playerId = pc.PlayerId;

        if (VectorVentCount[playerId] >= VectorVentNumWin)
        {
            VectorVentCount[playerId] = VectorVentNumWin;
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Vector);
            CustomWinnerHolder.WinnerIds.Add(playerId);
        }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        VectorVentCount.TryAdd(pc.PlayerId, 0);
        VectorVentCount[pc.PlayerId]++;
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, pc.PlayerId);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        pc.RPCPlayCustomSound("MarioJump");

        if (AmongUsClient.Instance.AmHost && VectorVentCount[pc.PlayerId] >= VectorVentNumWin)
        {
            pc.RPCPlayCustomSound("MarioCoin");
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Vector);
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        byte id = reader.ReadByte();
        VectorVentCount.TryAdd(id, 0);
        VectorVentCount[id]++;
    }
}
