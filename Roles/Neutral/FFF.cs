using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Roles.Neutral
{
    public class FFF : RoleBase
    {
        private const int Id = 11300;
        public static List<byte> PlayerIdList = [];
        public static bool On;

        public static OptionItem CanVent;
        public static OptionItem ChooseConverted;
        public static OptionItem MisFireKillTarget;

        public static OptionItem CanKillLovers;
        public static OptionItem CanKillMadmate;
        public static OptionItem CanKillCharmed;
        public static OptionItem CanKillSidekicks;
        public static OptionItem CanKillEgoists;
        public static OptionItem CanKillInfected;
        public static OptionItem CanKillContagious;
        public static OptionItem CanKillUndead;

        public bool IsWon;

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.FFF, zeroOne: false);
            MisFireKillTarget = BooleanOptionItem.Create(Id + 11, "FFFMisFireKillTarget", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.FFF]);
            ChooseConverted = BooleanOptionItem.Create(Id + 12, "FFFChooseConverted", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.FFF]);
            CanKillMadmate = BooleanOptionItem.Create(Id + 13, "FFFCanKillMadmate", true, TabGroup.NeutralRoles).SetParent(ChooseConverted);
            CanKillCharmed = BooleanOptionItem.Create(Id + 14, "FFFCanKillCharmed", true, TabGroup.NeutralRoles).SetParent(ChooseConverted);
            CanKillLovers = BooleanOptionItem.Create(Id + 15, "FFFCanKillLovers", true, TabGroup.NeutralRoles).SetParent(ChooseConverted);
            CanKillSidekicks = BooleanOptionItem.Create(Id + 16, "FFFCanKillSidekick", true, TabGroup.NeutralRoles).SetParent(ChooseConverted);
            CanKillEgoists = BooleanOptionItem.Create(Id + 17, "FFFCanKillEgoist", true, TabGroup.NeutralRoles).SetParent(ChooseConverted);
            CanKillInfected = BooleanOptionItem.Create(Id + 18, "FFFCanKillInfected", true, TabGroup.NeutralRoles).SetParent(ChooseConverted);
            CanKillContagious = BooleanOptionItem.Create(Id + 19, "FFFCanKillContagious", true, TabGroup.NeutralRoles).SetParent(ChooseConverted);
            CanKillUndead = BooleanOptionItem.Create(Id + 21, "FFFCanKillUndead", true, TabGroup.NeutralRoles).SetParent(ChooseConverted);
        }

        public override void Init()
        {
            PlayerIdList = [];
            On = false;
            IsWon = false;
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            On = true;
            IsWon = false;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive() && !IsWon;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(true);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null) return false;
            if (killer.PlayerId == target.PlayerId) return true;

            if (target.GetCustomSubRoles().Any(x => x.IsConverted() || x == CustomRoles.Madmate)
                || IsConvertedMainRole(target.GetCustomRole()))
            {
                if (!ChooseConverted.GetBool())
                {
                    if (killer.RpcCheckAndMurder(target)) IsWon = true;
                    Logger.Info($"{killer.GetRealName()} killed right target case 1", "FFF");
                    return false;
                }

                if (
                    ((target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Gangster)) && CanKillMadmate.GetBool())
                    || ((target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Succubus)) && CanKillCharmed.GetBool())
                    || ((target.Is(CustomRoles.Undead) || target.Is(CustomRoles.Necromancer) || target.Is(CustomRoles.Deathknight)) && CanKillUndead.GetBool())
                    || ((target.Is(CustomRoles.Lovers) || target.Is(CustomRoles.Ntr)) && CanKillLovers.GetBool())
                    || ((target.Is(CustomRoles.Romantic) || target.Is(CustomRoles.RuthlessRomantic) || target.Is(CustomRoles.VengefulRomantic)
                         || Romantic.PartnerId == target.PlayerId) && CanKillLovers.GetBool())
                    || ((target.Is(CustomRoles.Sidekick) || target.Is(CustomRoles.Jackal) || target.Is(CustomRoles.Recruit)) && CanKillSidekicks.GetBool())
                    || (target.Is(CustomRoles.Egoist) && CanKillEgoists.GetBool())
                    || ((target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Virus)) && CanKillContagious.GetBool())
                )
                {
                    if (killer.RpcCheckAndMurder(target)) IsWon = true;
                    Logger.Info($"{killer.GetRealName()} killed right target case 2", "FFF");
                    return false;
                }
            }

            if (MisFireKillTarget.GetBool() && killer.RpcCheckAndMurder(target, true))
            {
                target.SetRealKiller(killer);
                killer.Kill(target);
                target.Data.IsDead = true;
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Misfire;
            }

            killer.Suicide(PlayerState.DeathReason.Sacrifice);
            Logger.Info($"{killer.GetRealName()} killed incorrect target => misfire", "FFF");
            return false;
        }

        private static bool IsConvertedMainRole(CustomRoles role)
        {
            return role switch
            {
                CustomRoles.Gangster or
                    CustomRoles.Succubus or
                    CustomRoles.Deathknight or
                    CustomRoles.Necromancer or
                    CustomRoles.Romantic or
                    CustomRoles.RuthlessRomantic or
                    CustomRoles.VengefulRomantic or
                    CustomRoles.Sidekick or
                    CustomRoles.Jackal or
                    CustomRoles.Virus
                    => true,

                _ => false,
            };
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("FFFButtonText"));
        }
    }
}