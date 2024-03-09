using AmongUs.GameOptions;
using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class Paranoia : RoleBase
    {
        public static Dictionary<byte, int> ParaUsedButtonCount = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(7800, TabGroup.CrewmateRoles, CustomRoles.Paranoia);
            ParanoiaNumOfUseButton = IntegerOptionItem.Create(7810, "ParanoiaNumOfUseButton", new(0, 90, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Paranoia])
                .SetValueFormat(OptionFormat.Times);
            ParanoiaVentCooldown = FloatOptionItem.Create(7811, "ParanoiaVentCooldown", new(0, 180, 1), 10, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Paranoia])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
            ParaUsedButtonCount[playerId] = 0;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown =
                !ParaUsedButtonCount.TryGetValue(playerId, out var count2) || count2 < ParanoiaNumOfUseButton.GetInt()
                    ? ParanoiaVentCooldown.GetFloat()
                    : 300f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("ParanoiaVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("ParanoiaVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            Panic(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            pc.MyPhysics?.RpcBootFromVent(vent.Id);
            Panic(pc);
        }

        static void Panic(PlayerControl pc)
        {
            if (ParaUsedButtonCount.TryGetValue(pc.PlayerId, out var count2) && count2 < ParanoiaNumOfUseButton.GetInt())
            {
                ParaUsedButtonCount[pc.PlayerId] += 1;
                if (AmongUsClient.Instance.AmHost)
                {
                    _ = new LateTask(() => { Utils.SendMessage(Translator.GetString("SkillUsedLeft") + (ParanoiaNumOfUseButton.GetInt() - ParaUsedButtonCount[pc.PlayerId]), pc.PlayerId); }, 4.0f, "Paranoia Skill Remain Message");
                }

                pc.NoCheckStartMeeting(pc.Data);
            }
        }
    }
}