using System.Collections.Generic;
using EHR.Crewmate;
using static EHR.Options;

namespace EHR.Impostor;

internal class Nullifier : RoleBase
{
    private const int Id = 642000;
    public static List<byte> PlayerIdList = [];

    public static OptionItem NullCD;
    private static OptionItem KCD;
    private static OptionItem Delay;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Nullifier);

        NullCD = new FloatOptionItem(Id + 10, "NullCD", new(0f, 180f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Nullifier])
            .SetValueFormat(OptionFormat.Seconds);

        KCD = new FloatOptionItem(Id + 11, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Nullifier])
            .SetValueFormat(OptionFormat.Seconds);

        Delay = new IntegerOptionItem(Id + 12, "NullifierDelay", new(0, 90, 1), 5, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Nullifier])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KCD.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable || killer == null || target == null) return false;

        return killer.CheckDoubleTrigger(target, () =>
        {
            killer.SetKillCooldown(NullCD.GetFloat());
            killer.Notify(Translator.GetString("NullifierUseRemoved"));

            LateTask.New(() =>
            {
                switch (target.GetCustomRole())
                {
                    case CustomRoles.Cleanser:
                        if (Main.PlayerStates[target.PlayerId].Role is not Cleanser { IsEnable: true } cs) return;

                        cs.CleanserUses++;
                        cs.SendRPC(target.PlayerId);
                        break;
                    case CustomRoles.Hacker:
                        if (target.IsModdedClient())
                        {
                            Hacker.UseLimitSeconds[target.PlayerId] -= Hacker.ModdedClientAbilityUseSecondsMultiplier.GetInt();
                            Hacker.SendRPC(target.PlayerId, Hacker.UseLimitSeconds[target.PlayerId]);
                        }
                        else
                            Hacker.UseLimit[target.PlayerId]--;

                        break;
                    case CustomRoles.Vigilante:
                        Vigilante.Killed.Add(target.PlayerId);
                        Vigilante.SendRPC(target.PlayerId);
                        break;
                    default:
                        target.RpcRemoveAbilityUse();
                        break;
                }

                if (GameStates.IsInTask) Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
            }, Delay.GetInt(), "Nullifier Remove Ability Use");
        });
    }
}