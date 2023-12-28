using AmongUs.GameOptions;
using System.Collections.Generic;

namespace TOHE.Roles.Neutral;

public static class Sidekick
{
    public static List<byte> playerIdList = [];

    public static void Init()
    {
        playerIdList = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Jackal.KillCooldownSK.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(Jackal.HasImpostorVision.GetBool());
    public static void SetHudActive(HudManager __instance, bool isActive)
    {
        __instance.SabotageButton.ToggleVisible(isActive && Jackal.CanUseSabotageSK.GetBool());
    }
}
