using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using static EHR.Options;

namespace EHR.Impostor
{
    public class Hangman : RoleBase
    {
        private const int Id = 1400;
        private static List<byte> PlayerIdList = [];

        private static OptionItem ShapeshiftCooldown;
        public static OptionItem ShapeshiftDuration;
        private static OptionItem KCD;
        private static OptionItem HangmanLimitOpt;
        public static OptionItem HangmanAbilityUseGainWithEachKill;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Hangman);

            ShapeshiftCooldown = new FloatOptionItem(Id + 2, "ShapeshiftCooldown", new(1f, 60f, 1f), 30f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
                .SetValueFormat(OptionFormat.Seconds);

            ShapeshiftDuration = new FloatOptionItem(Id + 3, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
                .SetValueFormat(OptionFormat.Seconds);

            KCD = new FloatOptionItem(Id + 4, "KillCooldownOnStrangle", new(1f, 90f, 1f), 40f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
                .SetValueFormat(OptionFormat.Seconds);

            HangmanLimitOpt = new IntegerOptionItem(Id + 5, "AbilityUseLimit", new(0, 5, 1), 0, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
                .SetValueFormat(OptionFormat.Times);

            HangmanAbilityUseGainWithEachKill = new FloatOptionItem(Id + 6, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hangman])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(HangmanLimitOpt.GetInt());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = ShapeshiftDuration.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (Medic.ProtectList.Contains(target.PlayerId)) return false;

            if (target.Is(CustomRoles.Madmate) && !ImpCanKillMadmate.GetBool()) return false;

            if (killer.GetAbilityUseLimit() < 1 && killer.IsShifted()) return false;

            if (killer.IsShifted())
            {
                if (target.Is(CustomRoles.Pestilence)) return false;

                if (target.Is(CustomRoles.Veteran) && Veteran.VeteranInProtect.ContainsKey(target.PlayerId)) return false;

                killer.RpcRemoveAbilityUse();
                target.Data.IsDead = true;
                target.SetRealKiller(killer);
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.LossOfHead;
                target.RpcExileV2();
                Main.PlayerStates[target.PlayerId].SetDead();
                target.SetRealKiller(killer);
                killer.SetKillCooldown(KCD.GetFloat());
                return false;
            }

            return true;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (id.IsPlayerShifted()) hud.KillButton?.OverrideText(Translator.GetString("HangmanKillButtonTextDuringSS"));

            hud.AbilityButton?.SetUsesRemaining((int)id.GetAbilityUseLimit());
        }
    }
}