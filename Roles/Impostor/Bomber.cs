using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE.Roles.Impostor
{
    internal class Bomber : RoleBase
    {
        private bool IsNuker;
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            IsNuker = Main.PlayerStates[playerId].MainRole == CustomRoles.Nuker;
        }

        public override void Init()
        {
            On = false;
            IsNuker = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return base.CanUseKillButton(pc) && !IsNuker && Options.BomberCanKill.GetBool();
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = !IsNuker && Options.BomberCanKill.GetBool() ? Options.BomberKillCD.GetFloat() : 300f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool()) hud.PetButton?.OverrideText(Translator.GetString("BomberShapeshiftText"));
            else hud.AbilityButton?.OverrideText(Translator.GetString("BomberShapeshiftText"));
        }

        public override void OnPet(PlayerControl pc)
        {
            Bomb(pc);
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting) return true;
            Bomb(shapeshifter);

            return false;
        }

        void Bomb(PlayerControl pc)
        {
            Logger.Info("Bomber explosion", "Boom");
            CustomSoundsManager.RPCPlayCustomSoundAll("Boom");

            float radius = IsNuker ? Options.NukeRadius.GetFloat() : Options.BomberRadius.GetFloat();
            foreach (PlayerControl tg in Main.AllPlayerControls)
            {
                if (!tg.IsModClient()) tg.KillFlash();
                var pos = pc.Pos();
                var dis = Vector2.Distance(pos, tg.Pos());

                if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || (tg.Is(CustomRoleTypes.Impostor) && Options.ImpostorsSurviveBombs.GetBool()) || tg.inVent || tg.Is(CustomRoles.Pestilence)) continue;
                if (dis > radius) continue;
                if (tg.PlayerId == pc.PlayerId) continue;

                tg.Suicide(PlayerState.DeathReason.Bombed, pc);
            }

            _ = new LateTask(() =>
            {
                var totalAlive = Main.AllAlivePlayerControls.Length;
                if (Options.BomberDiesInExplosion.GetBool() && totalAlive > 1 && !GameStates.IsEnded)
                {
                    pc.Suicide(PlayerState.DeathReason.Bombed);
                }

                Utils.NotifyRoles(ForceLoop: true);
            }, 1.5f, "Bomber Suiscide");
        }
    }
}
