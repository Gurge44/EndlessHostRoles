namespace EHR.Impostor
{
    internal class SchrodingersCat : RoleBase
    {
        public static bool On;

        public static OptionItem WinsWithCrewIfNotAttacked;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            const int id = 13840;
            Options.SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.SchrodingersCat);
            WinsWithCrewIfNotAttacked = new BooleanOptionItem(id + 2, "SchrodingersCat.WinsWithCrewIfNotAttacked", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SchrodingersCat]);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            CustomRoles killerRole = killer.GetCustomRole();
            if (killerRole.IsImpostor() || killerRole.IsMadmate())
            {
                killerRole = CustomRoles.Refugee;
            }

            if (killerRole == CustomRoles.Jackal)
            {
                killerRole = CustomRoles.Sidekick;
            }

            if (Options.SingleRoles.Contains(killerRole))
            {
                killerRole = CustomRoles.Amnesiac;
            }

            target.RpcSetCustomRole(killerRole);
            target.RpcChangeRoleBasis(killerRole);

            killer.SetKillCooldown(5f);

            killer.Notify(string.Format(Translator.GetString("SchrodingersCat.Notify.KillerRecruited"), target.GetRealName(), CustomRoles.SchrodingersCat.ToColoredString()), 10f);
            target.Notify(string.Format(Translator.GetString("SchrodingersCat.Notify.RecruitedByKiller"), killer.GetRealName(), killerRole.ToColoredString()));

            Utils.NotifyRoles(SpecifySeer: killer, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, ForceLoop: true);

            return false;
        }
    }
}