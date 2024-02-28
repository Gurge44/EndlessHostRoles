using AmongUs.GameOptions;
using System.Collections.Generic;

namespace TOHE.Roles.Neutral;

public class Sidekick : RoleBase
{
    public static List<byte> playerIdList = [];

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => Jackal.CanVent.GetBool();
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Jackal.KillCooldownSK.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(Jackal.HasImpostorVision.GetBool());
    public override void SetButtonTexts(HudManager __instance, byte id) => __instance.SabotageButton.ToggleVisible(Jackal.CanSabotageSK.GetBool());
    public override bool CanUseSabotage(PlayerControl pc) => Jackal.CanSabotageSK.GetBool();
}