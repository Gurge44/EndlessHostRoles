using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class Underdog : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(10025, TabGroup.ImpostorRoles, CustomRoles.Underdog);
            UnderdogMaximumPlayersNeededToKill = new IntegerOptionItem(10030, "UnderdogMaximumPlayersNeededToKill", new(1, 15, 1), 5, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Underdog])
                .SetValueFormat(OptionFormat.Players);
            UnderdogKillCooldown = new FloatOptionItem(10031, "KillCooldown", new(0f, 180f, 2.5f), 15f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Underdog])
                .SetValueFormat(OptionFormat.Seconds);
            UnderdogKillCooldownWithMorePlayersAlive = new FloatOptionItem(10032, "UnderdogKillCooldownWithMorePlayersAlive", new(0f, 180f, 2.5f), 35f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Underdog])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Main.AllAlivePlayerControls.Length < UnderdogMaximumPlayersNeededToKill.GetInt() ? UnderdogKillCooldown.GetFloat() : UnderdogKillCooldownWithMorePlayersAlive.GetFloat();
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            SetKillCooldown(killer.PlayerId);
        }
    }
}