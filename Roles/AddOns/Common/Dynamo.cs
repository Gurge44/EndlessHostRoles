using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR.AddOns.Common;

public class Dynamo : IAddon
{
    private static OptionItem IncreaseSpeedBy;
    private static OptionItem IncreaseSpeedFrequency;
    private static OptionItem MaxSpeed;

    private static readonly Dictionary<byte, Vector2> LastPosition = [];
    private static readonly Dictionary<byte, float> StartingSpeed = [];
    private static readonly Dictionary<byte, float> SpeedIncreaseTimer = [];
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        const int id = 19390;
        Options.SetupAdtRoleOptions(id, CustomRoles.Dynamo, canSetNum: true, teamSpawnOptions: true);

        IncreaseSpeedBy = new FloatOptionItem(id + 9, "Dynamo.IncreaseSpeedBy", new(0.1f, 1f, 0.1f), 0.1f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dynamo])
            .SetValueFormat(OptionFormat.Multiplier);

        IncreaseSpeedFrequency = new FloatOptionItem(id + 7, "Dynamo.IncreaseSpeedFrequency", new(0.5f, 30f, 0.5f), 5f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dynamo])
            .SetValueFormat(OptionFormat.Seconds);

        MaxSpeed = new FloatOptionItem(id + 8, "Dynamo.MaxSpeed", new(0.1f, 3f, 0.1f), 3f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Dynamo])
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public static void Add()
    {
        foreach ((PlayerControl pc, float speed) in Main.AllAlivePlayerControls.Zip(Main.AllPlayerSpeed.Values))
        {
            if (pc.Is(CustomRoles.Dynamo))
            {
                LastPosition[pc.PlayerId] = pc.Pos();
                SpeedIncreaseTimer[pc.PlayerId] = -8f;
                StartingSpeed[pc.PlayerId] = speed;
            }
        }
    }

    public static void OnFixedUpdate(PlayerControl pc)
    {
        Vector2 pos = pc.Pos();
        bool moving = Vector2.Distance(pos, LastPosition[pc.PlayerId]) > 0.1f;
        LastPosition[pc.PlayerId] = pos;

        if (!moving || !pc.IsAlive() || !GameStates.IsInTask)
        {
            float speed = Main.AllPlayerSpeed[pc.PlayerId];
            float defaultSpeed = StartingSpeed[pc.PlayerId];

            if (Math.Abs(speed - defaultSpeed) > 0.1f)
            {
                Main.AllPlayerSpeed[pc.PlayerId] = defaultSpeed;
                pc.MarkDirtySettings();
            }

            return;
        }

        SpeedIncreaseTimer[pc.PlayerId] += Time.fixedDeltaTime;
        if (SpeedIncreaseTimer[pc.PlayerId] < IncreaseSpeedFrequency.GetFloat()) return;

        SpeedIncreaseTimer[pc.PlayerId] = 0f;

        Main.AllPlayerSpeed[pc.PlayerId] += IncreaseSpeedBy.GetFloat();
        float maxSpeed = MaxSpeed.GetFloat();
        if (Main.AllPlayerSpeed[pc.PlayerId] > maxSpeed) Main.AllPlayerSpeed[pc.PlayerId] = maxSpeed;

        pc.MarkDirtySettings();
    }
}