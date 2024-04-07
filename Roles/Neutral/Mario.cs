using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class Mario : RoleBase
    {
        public static Dictionary<byte, int> MarioVentCount = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(18300, TabGroup.NeutralRoles, CustomRoles.Mario);
            MarioVentNumWin = IntegerOptionItem.Create(18310, "MarioVentNumWin", new(0, 900, 5), 40, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mario])
                .SetValueFormat(OptionFormat.Times);
            MarioVentCD = FloatOptionItem.Create(18311, "VentCooldown", new(0f, 180f, 1f), 15f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mario])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
            MarioVentCount[playerId] = 0;
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
            return Utils.ColorString(Color.white, $"<color=#777777>-</color> {MarioVentCount.GetValueOrDefault(playerId, 0)}/{MarioVentNumWin.GetInt()}");
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("MarioVentButtonText");
            hud.AbilityButton?.SetUsesRemaining(MarioVentNumWin.GetInt() - MarioVentCount.GetValueOrDefault(id, 0));
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            var playerId = pc.PlayerId;
            if (MarioVentCount[playerId] > MarioVentNumWin.GetInt() && GameStates.IsInTask)
            {
                MarioVentCount[playerId] = MarioVentNumWin.GetInt();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario);
                CustomWinnerHolder.WinnerIds.Add(playerId);
            }
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            MarioVentCount.TryAdd(pc.PlayerId, 0);
            MarioVentCount[pc.PlayerId]++;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

            if (AmongUsClient.Instance.AmHost && MarioVentCount[pc.PlayerId] >= MarioVentNumWin.GetInt())
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Mario); //马里奥这个多动症赢了
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            }
        }
    }
}