using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Roles.Neutral;

public class Sidekick : RoleBase
{
    public static List<byte> playerIdList = [];

    public override bool IsEnable => playerIdList.Count > 0;

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

    public override bool CanUseImpostorVentButton(PlayerControl pc) => Jackal.CanVent.GetBool();
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Jackal.KillCooldownSK.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(Jackal.HasImpostorVision.GetBool());
    public override bool CanUseSabotage(PlayerControl pc) => Jackal.CanSabotageSK.GetBool() && pc.IsAlive();

    public override void SetButtonTexts(HudManager __instance, byte id)
    {
        __instance.SabotageButton.ToggleVisible(Jackal.CanSabotageSK.GetBool());
        __instance.KillButton?.OverrideText(Translator.GetString("KillButtonText"));
        __instance.ImpostorVentButton?.OverrideText(Translator.GetString("ReportButtonText"));
        __instance.SabotageButton?.OverrideText(Translator.GetString("SabotageButtonText"));
    }
}