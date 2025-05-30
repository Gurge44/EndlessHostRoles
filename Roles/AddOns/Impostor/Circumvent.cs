﻿using System.Collections.Generic;

namespace EHR.AddOns.Impostor;

internal class Circumvent : IAddon
{
    private static Dictionary<byte, int> Limits = [];

    private static OptionItem VentPreventionMode;
    private static OptionItem Limit;

    private static readonly string[] VentPreventionModes =
    [
        "NoVenting",
        "LimitPerGame",
        "LimitPerRounds"
    ];

    public AddonTypes Type => AddonTypes.ImpOnly;

    public void SetupCustomOption()
    {
        const int id = 14680;
        Options.SetupAdtRoleOptions(id, CustomRoles.Circumvent, canSetNum: true);

        VentPreventionMode = new StringOptionItem(id + 5, "VentPreventionMode", VentPreventionModes, 1, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Circumvent]);

        Limit = new IntegerOptionItem(id + 4, "VentLimit", new(1, 90, 1), 8, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Circumvent]);
    }

    public static void Init()
    {
        Limits = [];
    }

    public static void Add()
    {
        if (VentPreventionMode.GetValue() == 0) return;

        LateTask.New(() =>
        {
            foreach (KeyValuePair<byte, PlayerState> state in Main.PlayerStates)
            {
                if (state.Value.SubRoles.Contains(CustomRoles.Circumvent))
                    Limits[state.Key] = Limit.GetInt();
            }
        }, 3f, log: false);
    }

    public static void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (VentPreventionMode.GetValue() == 0)
        {
            pc.MyPhysics?.RpcBootFromVent(vent.Id);
            return;
        }

        if (Limits.ContainsKey(pc.PlayerId))
        {
            Limits[pc.PlayerId]--;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    public static void AfterMeetingTasks()
    {
        if (VentPreventionMode.GetValue() != 2) return;

        Limits.SetAllValues(Limit.GetInt());
    }

    public static string GetProgressText(byte playerId)
    {
        if (!Limits.TryGetValue(playerId, out int limit)) return string.Empty;

        int mode = VentPreventionMode.GetValue();

        string color = limit switch
        {
            > 11 => "#00ff00",
            > 7 when mode == 2 => "#00ff00",
            > 7 => "#ffff00",
            > 3 when mode == 2 => "#ffff00",
            > 3 => "#ffa500",
            > 0 when mode == 2 => "#ffa500",
            > 0 => "#ff0000",
            0 when mode == 2 => "#ff0000",
            0 => "#8b0000",

            _ => "#000000"
        };

        return $" <color={color}>({limit})</color>";
    }

    public static bool CanUseImpostorVentButton(PlayerControl pc)
    {
        if (!pc.Is(CustomRoles.Circumvent)) return true;

        return pc.inVent || (!Limits.TryGetValue(pc.PlayerId, out int limit) && VentPreventionMode.GetValue() != 0) || limit > 0;
    }
}