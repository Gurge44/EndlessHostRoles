using System;
using AmongUs.GameOptions;
using EHR.Modules.Extensions;
using static EHR.Options;

namespace EHR.Roles;

internal class Zombie : RoleBase
{
    public static bool On;

    private static OptionItem ZombieKillCooldown;
    private static OptionItem ZombieSpeedReduce;
    private static OptionItem ZombieSpeedReduceInterval;
    private static OptionItem ZombieInitialSpeed;
    private static OptionItem ZombieMinimumSpeed;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(16400, TabGroup.ImpostorRoles, CustomRoles.Zombie);

        ZombieKillCooldown = new FloatOptionItem(16410, "KillCooldown", new(0f, 180f, 0.5f), 5f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
            .SetValueFormat(OptionFormat.Seconds);

        ZombieSpeedReduce = new FloatOptionItem(16411, "ZombieSpeedReduce", new(0.0f, 1.0f, 0.1f), 0.1f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
            .SetValueFormat(OptionFormat.Multiplier);

        ZombieSpeedReduceInterval = new FloatOptionItem(16412, "ZombieSpeedReduceInterval", new(0f, 180f, 1f), 10f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
            .SetValueFormat(OptionFormat.Seconds);

        ZombieInitialSpeed = new FloatOptionItem(16413, "ZombieInitialSpeed", new(0.1f, 3f, 0.1f), 1f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
            .SetValueFormat(OptionFormat.Multiplier);

        ZombieMinimumSpeed = new FloatOptionItem(16414, "ZombieMinimumSpeed", new(0.05f, 2f, 0.05f), 0.1f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Zombie])
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public override void Add(byte playerId)
    {
        On = true;
        if (!AmongUsClient.Instance.AmHost) return;
        Main.AllPlayerSpeed[playerId] = ZombieInitialSpeed.GetFloat();
        var timer = new CountdownTimer(ZombieSpeedReduceInterval.GetInt() + 8, OnElapsed, cancelOnMeeting: false);
        return;

        void OnElapsed()
        {
            timer = new CountdownTimer(ZombieSpeedReduceInterval.GetInt(), OnElapsed, cancelOnMeeting: false);
            if (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) return;

            var pc = playerId.GetPlayer();

            if (pc == null)
            {
                timer.Dispose();
                return;
            }

            if (!pc.IsAlive())
            {
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                pc.MarkDirtySettings();
                timer.Dispose();
                return;
            }

            Main.AllPlayerSpeed[pc.PlayerId] = Math.Clamp(Main.AllPlayerSpeed[pc.PlayerId] - ZombieSpeedReduce.GetFloat(), ZombieMinimumSpeed.GetFloat(), 3f);
            pc.MarkDirtySettings();
        }
    }

    public override void Init()
    {
        On = false;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = ZombieKillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0.2f);
    }
}