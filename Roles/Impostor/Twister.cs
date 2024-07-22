using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor
{
    public class Twister : RoleBase
    {
        private const int Id = 4400;

        public static OptionItem ShapeshiftCooldown;
        private static OptionItem TwisterLimitOpt;
        public static OptionItem TwisterAbilityUseGainWithEachKill;

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Twister);
            ShapeshiftCooldown = new FloatOptionItem(Id + 10, "TwisterCooldown", new(1f, 60f, 1f), 30f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Twister])
                .SetValueFormat(OptionFormat.Seconds);
            TwisterLimitOpt = new IntegerOptionItem(Id + 11, "AbilityUseLimit", new(0, 5, 1), 0, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Twister])
                .SetValueFormat(OptionFormat.Times);
            TwisterAbilityUseGainWithEachKill = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.4f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Twister])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            playerId.SetAbilityUseLimit(TwisterLimitOpt.GetInt());
            On = true;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            if (UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = ShapeshiftCooldown.GetFloat();
            else
            {
                AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
                AURoleOptions.ShapeshifterDuration = 1f;
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            TwistPlayers(pc, true);
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            TwistPlayers(shapeshifter, shapeshifting);
            return false;
        }

        public override bool OnVanish(PlayerControl pc)
        {
            TwistPlayers(pc, true);
            return false;
        }

        static void TwistPlayers(PlayerControl shapeshifter, bool shapeshifting)
        {
            if (shapeshifter == null) return;
            if (shapeshifter.GetAbilityUseLimit() < 1) return;
            if (!shapeshifting) return;

            List<byte> changePositionPlayers = [shapeshifter.PlayerId];
            shapeshifter.RpcRemoveAbilityUse();

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (changePositionPlayers.Contains(pc.PlayerId) || Pelican.IsEaten(pc.PlayerId) || pc.onLadder || pc.inVent || GameStates.IsMeeting) continue;

                var filtered = Main.AllAlivePlayerControls.Where(a => !a.inVent && !Pelican.IsEaten(a.PlayerId) && !a.onLadder && a.PlayerId != pc.PlayerId && !changePositionPlayers.Contains(a.PlayerId)).ToArray();
                if (filtered.Length == 0) break;

                PlayerControl target = filtered.RandomElement();

                changePositionPlayers.Add(target.PlayerId);
                changePositionPlayers.Add(pc.PlayerId);

                pc.RPCPlayCustomSound("Teleport");

                var originPs = target.Pos();
                target.TP(pc.Pos());
                pc.TP(originPs);

                target.Notify(ColorString(GetRoleColor(CustomRoles.Twister), string.Format(GetString("TeleportedByTwister"), pc.GetRealName())));
                pc.Notify(ColorString(GetRoleColor(CustomRoles.Twister), string.Format(GetString("TeleportedByTwister"), target.GetRealName())));
            }
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool())
            {
                hud.PetButton?.OverrideText(GetString("TwisterButtonText"));
            }
            else
            {
                hud.AbilityButton?.OverrideText(GetString("TwisterButtonText"));
                hud.AbilityButton?.SetUsesRemaining((int)id.GetAbilityUseLimit());
            }
        }
    }
}