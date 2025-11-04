using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Spurt : IAddon
{
    private static OptionItem MinSpeed;
    private static OptionItem Modulator;
    private static OptionItem MaxSpeed;
    private static OptionItem DisplaysCharge;

    public static bool LocalPlayerAvoidsZeroAndOneHundredPrecent;

    private static readonly Dictionary<byte, Vector2> LastPos = [];
    public static readonly Dictionary<byte, float> StartingSpeed = [];
    private static readonly Dictionary<byte, int> LastNum = [];
    private static readonly Dictionary<byte, long> LastUpdate = [];
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        const int id = 648950;
        SetupAdtRoleOptions(id, CustomRoles.Spurt, canSetNum: true, teamSpawnOptions: true);

        MinSpeed = new FloatOptionItem(id + 10, "SpurtMinSpeed", new(0f, 3f, 0.25f), 0.75f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spurt])
            .SetValueFormat(OptionFormat.Multiplier);

        MaxSpeed = new FloatOptionItem(id + 11, "SpurtMaxSpeed", new(1.5f, 3f, 0.25f), 3f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spurt])
            .SetValueFormat(OptionFormat.Multiplier);

        Modulator = new FloatOptionItem(id + 8, "SpurtModule", new(0.25f, 3f, 0.25f), 1.25f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spurt])
            .SetValueFormat(OptionFormat.Multiplier);

        DisplaysCharge = new BooleanOptionItem(id + 9, "EnableSpurtCharge", true, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Spurt]);
    }

    public static void Add()
    {
        foreach ((PlayerControl pc, float speed) in Main.AllAlivePlayerControls.Zip(Main.AllPlayerSpeed.Values))
        {
            if (pc.Is(CustomRoles.Spurt))
            {
                LastPos[pc.PlayerId] = pc.Pos();
                LastNum[pc.PlayerId] = 0;
                LastUpdate[pc.PlayerId] = Utils.TimeStamp;
                StartingSpeed[pc.PlayerId] = speed;
            }
        }

        LocalPlayerAvoidsZeroAndOneHundredPrecent = PlayerControl.LocalPlayer.Is(CustomRoles.Spurt);
    }

    public static void DeathTask(PlayerControl player)
    {
        Main.AllPlayerSpeed[player.PlayerId] = StartingSpeed[player.PlayerId];
        player.MarkDirtySettings();
    }

    private static int DetermineCharge(PlayerControl player)
    {
        float minSpeed = MinSpeed.GetFloat();
        float maxSpeed = MaxSpeed.GetFloat();

        if (Mathf.Approximately(minSpeed, maxSpeed)) return 100;

        return (int)((Main.AllPlayerSpeed[player.PlayerId] - minSpeed) / (maxSpeed - minSpeed) * 100);
    }

    public static string GetSuffix(PlayerControl player, bool isforhud = false)
    {
        if (!player.Is(CustomRoles.Spurt) || !DisplaysCharge.GetBool() || GameStates.IsMeeting) return string.Empty;

        int fontsize = isforhud ? 90 : 65;

        return $"<size={fontsize}%>{string.Format(Translator.GetString("SpurtSuffix"), DetermineCharge(player))}</size>";
    }

    public static void OnFixedUpdate(PlayerControl player)
    {
        Vector2 pos = player.Pos();
        bool moving = Vector2.Distance(pos, LastPos[player.PlayerId]) > 0.1f || player.MyPhysics.Animations.IsPlayingRunAnimation();
        LastPos[player.PlayerId] = pos;

        float modulator = Modulator.GetFloat();
        float increaseBy = Mathf.Clamp(modulator / 20 * 1.5f, 0.05f, 0.6f);
        float decreaseby = Mathf.Clamp(modulator / 20 * 0.5f, 0.01f, 0.3f);

        int charge = DetermineCharge(player);

        if (player.AmOwner && charge is <= 0 or >= 100)
            LocalPlayerAvoidsZeroAndOneHundredPrecent = false;

        if (DisplaysCharge.GetBool() && !player.IsModdedClient() && LastNum[player.PlayerId] != charge)
        {
            LastNum[player.PlayerId] = charge;
            long now = Utils.TimeStamp;

            if (now != LastUpdate[player.PlayerId])
            {
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                LastUpdate[player.PlayerId] = now;
            }
        }

        if (!moving)
        {
            Main.AllPlayerSpeed[player.PlayerId] += Mathf.Clamp(increaseBy, 0f, MaxSpeed.GetFloat() - Main.AllPlayerSpeed[player.PlayerId]);
            return;
        }

        Main.AllPlayerSpeed[player.PlayerId] -= Mathf.Clamp(decreaseby, 0f, Main.AllPlayerSpeed[player.PlayerId] - MinSpeed.GetFloat());
        if (charge >= 1) player.MarkDirtySettings();
    }
}