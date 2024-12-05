using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.AddOns.Common;

public class Dynamo : IAddon
{
    private static OptionItem MinSpeed;
    private static OptionItem Modulator;
    private static OptionItem MaxSpeed;
    private static OptionItem DisplaysCharge;

    private static readonly Dictionary<byte, Vector2> LastPos = [];
    private static readonly Dictionary<byte, int> LastNum = [];
    private static readonly Dictionary<byte, long> LastUpdate = [];
    
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        const int id = 19390;
        Options.SetupAdtRoleOptions(id, CustomRoles.Dynamo, canSetNum: true, teamSpawnOptions: true);

        MinSpeed = new FloatOptionItem(id + 10, "SpurtMinSpeed", new(0f, 3f, 0.25f), 1.25f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dynamo])
            .SetValueFormat(OptionFormat.Multiplier);

        MaxSpeed = new FloatOptionItem(id + 7, "SpurtMaxSpeed", new(1.5f, 3f, 0.25f), 3f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dynamo])
            .SetValueFormat(OptionFormat.Multiplier);

        Modulator = new FloatOptionItem(id + 8, "SpurtModule", new(0.25f, 3f, 0.25f), 1.25f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dynamo])
            .SetValueFormat(OptionFormat.Multiplier);

        DisplaysCharge = new BooleanOptionItem(id + 9, "EnableSpurtCharge", true, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dynamo]);
    }

    public static void Add()
    {
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc.Is(CustomRoles.Dynamo))
            {
                LastPos[pc.PlayerId] = pc.Pos();
                LastNum[pc.PlayerId] = 0;
                LastUpdate[pc.PlayerId] = Utils.TimeStamp;
            }
        }
    }

    public static string GetSuffix(PlayerControl player, bool hud = false)
    {
        if (!player.Is(CustomRoles.Dynamo) || !DisplaysCharge.GetBool() || GameStates.IsMeeting) return string.Empty;
        return $"<size={(hud ? 90 : 65)}%>{string.Format(Translator.GetString("DynamoSuffix"), DetermineCharge(player, out _, out _))}</size>";
    }

    public static void OnFixedUpdate(PlayerControl player)
    {
        Vector2 pos = player.Pos();
        bool moving = Vector2.Distance(pos, LastPos[player.PlayerId]) > 0.1f || player.MyPhysics.Animations.IsPlayingRunAnimation();
        LastPos[player.PlayerId] = pos;

        float modulator = Modulator.GetFloat();
        float increaseBy = Mathf.Clamp(modulator / 20 * 1.5f, 0.05f, 0.6f);
        float decreaseby = Mathf.Clamp(modulator / 20 * 0.5f, 0.01f, 0.3f);

        int charge = DetermineCharge(player, out float minSpeed, out float maxSpeed);

        if (DisplaysCharge.GetBool() && !player.IsModClient() && LastNum[player.PlayerId] != charge)
        {
            LastNum[player.PlayerId] = charge;
            long now = Utils.TimeStamp;

            if (now != LastUpdate[player.PlayerId])
            {
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                LastUpdate[player.PlayerId] = now;
            }
        }

        if (!moving) Main.AllPlayerSpeed[player.PlayerId] -= Mathf.Clamp(decreaseby, 0f, Main.AllPlayerSpeed[player.PlayerId] - minSpeed);
        else Main.AllPlayerSpeed[player.PlayerId] += Mathf.Clamp(increaseBy, 0f, maxSpeed - Main.AllPlayerSpeed[player.PlayerId]);
        
        player.MarkDirtySettings();
    }

    private static int DetermineCharge(PlayerControl player, out float minSpeed, out float maxSpeed)
    {
        minSpeed = MinSpeed.GetFloat();
        maxSpeed = MaxSpeed.GetFloat();
        return Mathf.Approximately(minSpeed, maxSpeed) ? 100 : (int)((Main.AllPlayerSpeed[player.PlayerId] - minSpeed) / (maxSpeed - minSpeed) * 100);
    }
}