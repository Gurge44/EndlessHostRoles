using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles;

internal class Puppeteer : RoleBase
{
    public static Dictionary<byte, byte> PuppeteerList = [];
    public static Dictionary<byte, long> PuppeteerDelayList = [];
    private static Dictionary<byte, int> PuppeteerDelay = [];
    private static Dictionary<byte, int> PuppeteerMaxPuppets = [];
    private static Dictionary<byte, float> TargetDistance = [];

    public static bool On;

    private static OptionItem PuppeteerKCD;
    private static OptionItem PuppeteerCD;
    public static OptionItem PuppeteerCanKillNormally;
    private static OptionItem PuppeteerManipulationBypassesLazy;
    private static OptionItem PuppeteerManipulationBypassesLazyGuy;
    private static OptionItem PuppeteerPuppetCanKillPuppeteer;
    private static OptionItem PuppeteerPuppetCanKillImpostors;
    private static OptionItem PuppeteerMaxPuppetsOpt;
    private static OptionItem PuppeteerDiesAfterMaxPuppets;
    private static OptionItem PuppeteerMinDelay;
    private static OptionItem PuppeteerMaxDelay;
    private static OptionItem PuppeteerManipulationEndsAfterFixedTime;
    private static OptionItem PuppeteerManipulationEndsAfterTime;
    private static OptionItem PuppetDiesAlongWithVictim;
    
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(3900, TabGroup.ImpostorRoles, CustomRoles.Puppeteer);

        PuppeteerCD = new FloatOptionItem(3911, "PuppeteerCD", new(2.5f, 60f, 0.5f), 22.5f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
            .SetValueFormat(OptionFormat.Seconds);

        PuppeteerCanKillNormally = new BooleanOptionItem(3917, "PuppeteerCanKillNormally", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);

        PuppeteerKCD = new FloatOptionItem(3912, "PuppeteerKCD", new(2.5f, 60f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(PuppeteerCanKillNormally)
            .SetValueFormat(OptionFormat.Seconds);

        PuppeteerMinDelay = new IntegerOptionItem(3913, "PuppeteerMinDelay", new(0, 90, 1), 3, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
            .SetValueFormat(OptionFormat.Seconds);

        PuppeteerMaxDelay = new IntegerOptionItem(3914, "PuppeteerMaxDelay", new(0, 90, 1), 7, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
            .SetValueFormat(OptionFormat.Seconds);

        PuppeteerManipulationEndsAfterFixedTime = new BooleanOptionItem(3915, "PuppeteerManipulationEndsAfterFixedTime", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);

        PuppeteerManipulationEndsAfterTime = new IntegerOptionItem(3916, "PuppeteerManipulationEndsAfterTime", new(0, 90, 1), 30, TabGroup.ImpostorRoles)
            .SetParent(PuppeteerManipulationEndsAfterFixedTime)
            .SetValueFormat(OptionFormat.Seconds);

        PuppeteerManipulationBypassesLazy = new BooleanOptionItem(3918, "PuppeteerManipulationBypassesLazy", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);

        PuppeteerManipulationBypassesLazyGuy = new BooleanOptionItem(3922, "PuppeteerManipulationBypassesLazyGuy", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);

        PuppeteerPuppetCanKillImpostors = new BooleanOptionItem(3919, "PuppeteerPuppetCanKillImpostors", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);

        PuppeteerPuppetCanKillPuppeteer = new BooleanOptionItem(3920, "PuppeteerPuppetCanKillPuppeteer", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);

        PuppeteerMaxPuppetsOpt = new IntegerOptionItem(3921, "PuppeteerMaxPuppets", new(0, 30, 1), 5, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
            .SetValueFormat(OptionFormat.Times);

        PuppeteerDiesAfterMaxPuppets = new BooleanOptionItem(3923, "PuppeteerDiesAfterMaxPuppets", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);

        PuppetDiesAlongWithVictim = new BooleanOptionItem(3924, "PuppetDiesAlongWithVictim", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
        PuppeteerList = [];
        PuppeteerDelayList = [];
        PuppeteerDelay = [];
        PuppeteerMaxPuppets = [];
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = PuppeteerCanKillNormally.GetBool() ? PuppeteerKCD.GetFloat() : PuppeteerCD.GetFloat();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("PuppeteerOperateButtonText"));
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Thanos.IsImmune(target)) return false;
        if (target.Is(CustomRoles.LazyGuy) && !PuppeteerManipulationBypassesLazyGuy.GetBool()) return false;
        if (target.Is(CustomRoles.Lazy) && !PuppeteerManipulationBypassesLazy.GetBool()) return false;

        if (Medic.ProtectList.Contains(target.PlayerId)) return false;

        if (!PuppeteerMaxPuppets.TryGetValue(killer.PlayerId, out int usesLeft))
        {
            usesLeft = PuppeteerMaxPuppetsOpt.GetInt();
            PuppeteerMaxPuppets[killer.PlayerId] = usesLeft;
        }

        if (PuppeteerCanKillNormally.GetBool())
        {
            return killer.CheckDoubleTrigger(target, () =>
            {
                PuppeteerList[target.PlayerId] = killer.PlayerId;
                PuppeteerDelayList[target.PlayerId] = Utils.TimeStamp;
                PuppeteerDelay[target.PlayerId] = IRandom.Instance.Next(PuppeteerMinDelay.GetInt(), PuppeteerMaxDelay.GetInt());
                killer.SetKillCooldown(PuppeteerCD.GetFloat());

                if (usesLeft <= 1 && PuppeteerDiesAfterMaxPuppets.GetBool())
                    LateTask.New(() => { killer.Suicide(); }, 1.5f, "Puppeteer Max Uses Reached => Suicide");
                else
                    killer.Notify(string.Format(Translator.GetString("PuppeteerUsesRemaining"), usesLeft - 1));

                PuppeteerMaxPuppets[killer.PlayerId]--;
                killer.RPCPlayCustomSound("Line");
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            });
        }

        PuppeteerList[target.PlayerId] = killer.PlayerId;
        PuppeteerDelayList[target.PlayerId] = Utils.TimeStamp;
        PuppeteerDelay[target.PlayerId] = IRandom.Instance.Next(PuppeteerMinDelay.GetInt(), PuppeteerMaxDelay.GetInt());
        killer.SetKillCooldown();

        if (usesLeft <= 1)
            LateTask.New(() => { killer.Suicide(); }, 1.5f, "Puppeteer Max Uses Reached => Suicide");
        else
            killer.Notify(string.Format(Translator.GetString("PuppeteerUsesRemaining"), usesLeft - 1));

        PuppeteerMaxPuppets[killer.PlayerId]--;
        killer.RPCPlayCustomSound("Line");
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        return false;
    }

    public override void OnGlobalFixedUpdate(PlayerControl player, bool lowLoad)
    {
        if (player == null || lowLoad) return;

        byte playerId = player.PlayerId;
        long now = Utils.TimeStamp;

        if (!GameStates.IsInTask || !PuppeteerList.ContainsKey(playerId)) return;

        long pupetDelay = PuppeteerDelayList[playerId];

        void ClearPuppet()
        {
            PuppeteerList.Remove(playerId);
            PuppeteerDelayList.Remove(playerId);
            PuppeteerDelay.Remove(playerId);
        }

        if (!player.IsAliveWithConditions())
        {
            ClearPuppet();
            return;
        }
        if (PuppeteerManipulationEndsAfterFixedTime.GetBool() && pupetDelay + PuppeteerManipulationEndsAfterTime.GetInt() < now)
        {
            ClearPuppet();

            var alive = Main.CachedAlivePlayerControls();
            for (int i = 0; i < alive.Count; i++)
            {
                var x = alive[i];
                if (x.Is(CustomRoles.Puppeteer))
                    Utils.NotifyRoles(SpecifySeer: x, SpecifyTarget: player);
            }
            return;
        }
        if (pupetDelay + PuppeteerDelay[playerId] >= now) return;

        Vector2 puppeteerPos = player.Pos();
        float minDistance = float.MaxValue;
        PlayerControl closestTarget = null;
        var alivePlayers = Main.CachedAlivePlayerControls();

        for (int targetIndex = 0; targetIndex < alivePlayers.Count; targetIndex++)
        {
            PlayerControl target = alivePlayers[targetIndex];
            if (target.PlayerId == playerId) continue;
            if (target.Is(CustomRoles.Pestilence)) continue;
            if (target.Is(CustomRoles.Puppeteer) && !PuppeteerPuppetCanKillPuppeteer.GetBool()) continue;
            if (target.Is(CustomRoleTypes.Impostor) && !PuppeteerPuppetCanKillImpostors.GetBool()) continue;

            float dis = Vector2.Distance(puppeteerPos, target.Pos());
            if (dis < minDistance)
            {
                minDistance = dis;
                closestTarget = target;
            }
        }
        if (closestTarget == null) return;

        float killRange = GameManager.Instance.LogicOptions.GetKillDistance();
        if (minDistance > killRange || !player.CanMove || !closestTarget.CanMove) return;
        if (!player.RpcCheckAndMurder(closestTarget, true)) return;

        byte puppeteerId = PuppeteerList[playerId];
        RPC.PlaySoundRPC(puppeteerId, Sounds.KillSound);
        PlayerControl puppeteer = Utils.GetPlayerById(puppeteerId);

        closestTarget.SetRealKiller(puppeteer);
        player.Kill(closestTarget);

        if (PuppetDiesAlongWithVictim.GetBool())
            player.Suicide(realKiller: puppeteer);

        player.MarkDirtySettings();
        closestTarget.MarkDirtySettings();

        ClearPuppet();

        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
        Utils.NotifyRoles(SpecifySeer: closestTarget, SpecifyTarget: closestTarget);
    }
}
