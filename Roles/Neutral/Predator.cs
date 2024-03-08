using AmongUs.GameOptions;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace TOHE.Roles.Neutral
{
    internal class Predator : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        private const int Id = 643540;
        private static OptionItem NumOfRolesToKill;
        private static OptionItem MaxImpRolePicks;
        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpVision;

        private List<CustomRoles> RolesToKill = [];
        public bool IsWon;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Predator);
            NumOfRolesToKill = IntegerOptionItem.Create(Id + 2, "NumOfRolesToKill", new(1, 10, 1), 3, TabGroup.NeutralRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator]);
            MaxImpRolePicks = IntegerOptionItem.Create(Id + 3, "MaxImpRolePicks", new(1, 10, 1), 1, TabGroup.NeutralRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator]);
            KillCooldown = FloatOptionItem.Create(Id + 4, "KillCooldown", new(0f, 180f, 2.5f), 15f, TabGroup.NeutralRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 5, "CanVent", true, TabGroup.NeutralRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator]);
            HasImpVision = BooleanOptionItem.Create(Id + 6, "HasImpostorVision", false, TabGroup.NeutralRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Predator]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            IsWon = false;

            _ = new LateTask(() =>
            {
                RolesToKill = [];

                var allRoles = EnumHelper.GetAllValues<CustomRoles>().ToList();
                allRoles.RemoveAll(x => x >= CustomRoles.NotAssigned || !x.RoleExist(countDead: true));

                var r = IRandom.Instance;
                var impRoles = 0;

                for (int i = 0; i < NumOfRolesToKill.GetInt(); i++)
                {
                    var index = r.Next(allRoles.Count);
                    var role = allRoles[index];

                    if (role.Is(Team.Impostor))
                    {
                        if (impRoles >= MaxImpRolePicks.GetInt())
                        {
                            i--;
                            continue;
                        }

                        impRoles++;
                    }

                    allRoles.RemoveAt(index);
                    RolesToKill.Add(role);
                }

                Logger.Info($"Predator Roles: {RolesToKill.Join()}", "Predator");
            }, 3f, "Select Predator Roles");
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(HasImpVision.GetBool());
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return !IsWon;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (IsWon) return false;

            var targetRole = target.GetCustomRole();
            if (RolesToKill.Contains(targetRole))
            {
                IsWon = true;
                if (!killer.IsModClient()) killer.Notify(string.Format(Translator.GetString("PredatorCorrectKill"), Translator.GetString($"{targetRole}")));
                return true;
            }

            killer.Suicide();
            return false;
        }

        public static string GetSuffixAndHudText(PlayerControl seer, bool hud = false)
        {
            if (Main.PlayerStates[seer.PlayerId].Role is not Predator { IsEnable: true } pt) return string.Empty;
            if (pt.IsWon) return !hud ? "<#00ff00>\u2713</color>" : Translator.GetString("PredatorDone");
            var text = pt.RolesToKill.Join(x => Utils.ColorString(Utils.GetRoleColor(x), Translator.GetString($"{x}")));
            return hud ? text : $"<size=1.7>{text}</size>";
        }
    }
}
