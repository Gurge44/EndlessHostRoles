using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using UnityEngine;
using static EHR.Options;

namespace EHR.Impostor
{
    internal class Bomber : RoleBase
    {
        public static bool On;
        private bool IsNuker;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(2400, TabGroup.ImpostorRoles, CustomRoles.Bomber);
            BomberRadius = new FloatOptionItem(2018, "BomberRadius", new(0.5f, 5f, 0.5f), 2f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber])
                .SetValueFormat(OptionFormat.Multiplier);
            BomberCanKill = new BooleanOptionItem(2015, "CanKill", false, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber]);
            BomberKillCD = new FloatOptionItem(2020, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(BomberCanKill)
                .SetValueFormat(OptionFormat.Seconds);
            BombCooldown = new FloatOptionItem(2030, "BombCooldown", new(5f, 180f, 2.5f), 60f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber])
                .SetValueFormat(OptionFormat.Seconds);
            ImpostorsSurviveBombs = new BooleanOptionItem(2031, "ImpostorsSurviveBombs", true, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber]);
            BomberDiesInExplosion = new BooleanOptionItem(2032, "BomberDiesInExplosion", true, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber]);
            NukerChance = new IntegerOptionItem(2033, "NukerChance", new(0, 100, 5), 0, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bomber])
                .SetValueFormat(OptionFormat.Percent);
            NukeCooldown = new FloatOptionItem(2035, "NukeCooldown", new(5f, 180f, 2.5f), 60f, TabGroup.ImpostorRoles)
                .SetParent(NukerChance)
                .SetValueFormat(OptionFormat.Seconds);
            NukeRadius = new FloatOptionItem(2034, "NukeRadius", new(5f, 100f, 1f), 25f, TabGroup.ImpostorRoles)
                .SetParent(NukerChance)
                .SetValueFormat(OptionFormat.Multiplier);
        }

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
            return base.CanUseKillButton(pc) && !IsNuker && BomberCanKill.GetBool();
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = !IsNuker && BomberCanKill.GetBool() ? BomberKillCD.GetFloat() : 300f;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            try
            {
                if (UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = IsNuker ? NukeCooldown.GetFloat() : BombCooldown.GetFloat();
                else
                {
                    if (UsePets.GetBool()) return;
                    AURoleOptions.ShapeshifterCooldown = IsNuker ? NukeCooldown.GetFloat() : BombCooldown.GetFloat();
                    AURoleOptions.ShapeshifterDuration = 2f;
                }
            }
            catch
            {
            }
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool()) hud.PetButton?.OverrideText(Translator.GetString("BomberShapeshiftText"));
            else hud.AbilityButton?.OverrideText(Translator.GetString("BomberShapeshiftText"));
        }

        public override void OnPet(PlayerControl pc)
        {
            Bomb(pc);
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;
            Bomb(shapeshifter);

            return false;
        }

        public override bool OnVanish(PlayerControl pc)
        {
            Bomb(pc);
            return false;
        }

        void Bomb(PlayerControl pc)
        {
            Logger.Info("Bomber explosion", "Boom");
            CustomSoundsManager.RPCPlayCustomSoundAll("Boom");

            float radius = IsNuker ? NukeRadius.GetFloat() : BomberRadius.GetFloat();
            foreach (PlayerControl tg in Main.AllPlayerControls)
            {
                if (!tg.IsModClient()) tg.KillFlash();
                var pos = pc.Pos();
                var dis = Vector2.Distance(pos, tg.Pos());

                if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || (tg.Is(CustomRoleTypes.Impostor) && ImpostorsSurviveBombs.GetBool()) || tg.inVent || tg.Is(CustomRoles.Pestilence)) continue;
                if (dis > radius) continue;
                if (tg.PlayerId == pc.PlayerId) continue;

                tg.Suicide(PlayerState.DeathReason.Bombed, pc);
            }

            LateTask.New(() =>
            {
                var totalAlive = Main.AllAlivePlayerControls.Length;
                if (BomberDiesInExplosion.GetBool() && totalAlive > 1 && !GameStates.IsEnded)
                {
                    pc.Suicide(PlayerState.DeathReason.Bombed);
                }

                Utils.NotifyRoles(ForceLoop: true);
            }, 1.5f, "Bomber Suiscide");
        }
    }
}