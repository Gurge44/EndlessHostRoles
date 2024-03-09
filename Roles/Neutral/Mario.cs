using AmongUs.GameOptions;
using System.Collections.Generic;
using UnityEngine;

namespace TOHE.Roles.Neutral
{
    internal class Mario : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            Main.MarioVentCount[playerId] = 0;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = Options.MarioVentCD.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            return Utils.ColorString(Color.white, $"<color=#777777>-</color> {Main.MarioVentCount.GetValueOrDefault(playerId, 0)}/{Options.MarioVentNumWin.GetInt()}");
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("MarioVentButtonText");
            hud.AbilityButton?.SetUsesRemaining(Options.MarioVentNumWin.GetInt() - (Main.MarioVentCount.GetValueOrDefault(id, 0)));
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            var playerId = pc.PlayerId;
            if (Main.MarioVentCount[playerId] > Options.MarioVentNumWin.GetInt() && GameStates.IsInTask)
            {
                Main.MarioVentCount[playerId] = Options.MarioVentNumWin.GetInt();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario);
                CustomWinnerHolder.WinnerIds.Add(playerId);
            }
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            Main.MarioVentCount.TryAdd(pc.PlayerId, 0);
            Main.MarioVentCount[pc.PlayerId]++;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

            if (AmongUsClient.Instance.AmHost && Main.MarioVentCount[pc.PlayerId] >= Options.MarioVentNumWin.GetInt())
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario); //马里奥这个多动症赢了
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            }
        }
    }
}