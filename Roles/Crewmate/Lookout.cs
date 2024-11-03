﻿using System.Text;
using UnityEngine;

namespace EHR.Crewmate
{
    internal class Lookout : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(9150, TabGroup.CrewmateRoles, CustomRoles.Lookout);
        }

        public override void OnPet(PlayerControl pc)
        {
            PlayerControl[] aapc = Main.AllAlivePlayerControls;
            var sb = new StringBuilder();

            for (var i = 0; i < aapc.Length; i++)
                if (i % 3 == 0)
                    sb.AppendLine();

            for (var i = 0; i < aapc.Length; i++)
            {
                PlayerControl player = aapc[i];
                if (player == null) continue;

                if (i != 0) sb.Append("; ");

                string name = player.GetRealName();
                byte id = player.PlayerId;
                if (Main.PlayerColors.TryGetValue(id, out Color32 color)) name = Utils.ColorString(color, name);

                sb.Append($"{name} {id}");
                if (i % 3 == 0 && i != aapc.Length - 1) sb.AppendLine();
            }

            pc.Notify(sb.ToString());
        }
    }
}