using EHR.Modules;
using static EHR.Options;

namespace EHR.Roles.Crewmate
{
    internal class Demolitionist : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(5550, TabGroup.CrewmateRoles, CustomRoles.Demolitionist);
            DemolitionistVentTime = FloatOptionItem.Create(5552, "DemolitionistVentTime", new(0f, 90f, 1f), 5f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Demolitionist])
                .SetValueFormat(OptionFormat.Seconds);
            DemolitionistKillerDiesOnMeetingCall = BooleanOptionItem.Create(5553, "DemolitionistKillerDiesOnMeetingCall", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Demolitionist]);
        }

        public static void OnDeath(PlayerControl killer, PlayerControl target)
        {
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Demolitionist), Translator.GetString("OnDemolitionistDead")));
            killer.KillFlash();
            _ = new LateTask(() =>
            {
                if (!killer.inVent && (killer.PlayerId != target.PlayerId))
                {
                    if ((DemolitionistKillerDiesOnMeetingCall.GetBool() || GameStates.IsInTask) && killer.IsAlive())
                    {
                        killer.Suicide(PlayerState.DeathReason.Demolished, target);
                        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                    }
                }
                else
                {
                    if (killer.IsModClient()) RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                    else killer.RpcGuardAndKill(killer);
                    killer.SetKillCooldown(Main.AllPlayerKillCooldown[killer.PlayerId] - (DemolitionistVentTime.GetFloat() + 0.5f));
                }
            }, DemolitionistVentTime.GetFloat() + 0.5f);
        }
    }
}
