using Il2CppSystem.Net.Mail;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Options;

namespace EHR.AddOns.Common
{
    internal class Spurt : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;
        private static OptionItem MinSpeed;
        private static OptionItem Modulator;
        private static OptionItem MaxSpeed;

        private static Dictionary<byte, Vector2> LastPos = [];
        private static Dictionary<byte, float> StartingSpeed = [];

        public void SetupCustomOption()
        {
            const int id = 19391;
            SetupAdtRoleOptions(id, CustomRoles.Spurt, canSetNum: true, teamSpawnOptions: true);
            MinSpeed = new FloatOptionItem(id + 6, "SpurtMinSpeed", new(0f, 3f, 0.25f), 0.75f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Spurt])
                .SetValueFormat(OptionFormat.Multiplier);
            MaxSpeed = new FloatOptionItem(id + 7, "SpurtMaxSpeed", new(1.5f, 3f, 0.25f), 3f, TabGroup.Addons)
               .SetParent(CustomRoleSpawnChances[CustomRoles.Spurt])
               .SetValueFormat(OptionFormat.Multiplier);
            Modulator = new FloatOptionItem(id + 8, "SpurtModule", new(0.25f, 3f, 0.25f), 1.25f, TabGroup.Addons)
              .SetParent(CustomRoleSpawnChances[CustomRoles.Spurt])
              .SetValueFormat(OptionFormat.Multiplier);
        }
        public static void Add()
        {

            foreach ((PlayerControl pc, float speed) in Main.AllAlivePlayerControls.Zip(Main.AllPlayerSpeed.Values))
            {
                if (pc.Is(CustomRoles.Spurt))
                {
                    LastPos[pc.PlayerId] = pc.Pos();
                    StartingSpeed[pc.PlayerId] = speed;
                }
            }

        }
        public static void DeathTask(PlayerControl player)
        {
            if (!player.Is(CustomRoles.Spurt)) return;

            Main.AllPlayerSpeed[player.PlayerId] = StartingSpeed[player.PlayerId];
            player.MarkDirtySettings();
        }
        public static void OnFixedUpdate(PlayerControl player)
        {
            var pos = player.Pos();
            bool moving = Vector2.Distance(pos, LastPos[player.PlayerId]) > 0f;
            LastPos[player.PlayerId] = pos;

            float ChargeBy = Mathf.Clamp(Modulator.GetFloat() / 20 * 1.5f, 0.1f, 0.6f);
            float Decreaseby = Mathf.Clamp(Modulator.GetFloat() / 20 * 0.5f, 0.01f, 0.3f);

            if (!moving)
            {
                Main.AllPlayerSpeed[player.PlayerId] += Mathf.Clamp(ChargeBy, 0f, MaxSpeed.GetFloat() - Main.AllPlayerSpeed[player.PlayerId]);

                Logger.Info($": {Main.AllPlayerSpeed[player.PlayerId]}", "CURRENTSPEEDT_IMOVING");
                return;
            }

            Main.AllPlayerSpeed[player.PlayerId] -= Mathf.Clamp(Decreaseby, 0f, Main.AllPlayerSpeed[player.PlayerId] - MinSpeed.GetFloat());
            player.MarkDirtySettings();
            Logger.Info($": {Main.AllPlayerSpeed[player.PlayerId]}", "CURRENTSPEEDT_MOVING");
        }
    }
}