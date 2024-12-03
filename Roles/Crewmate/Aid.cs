using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Aid : RoleBase
{
    private const int Id = 640200;
    public static Dictionary<byte, long> ShieldedPlayers = [];

    public static OptionItem AidDur;
    public static OptionItem AidCD;
    public static OptionItem TargetKnowsShield;
    public static OptionItem UseLimitOpt;
    public static OptionItem UsePet;
    private static bool On;
    private byte AidId;

    public byte TargetId;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Aid);

        AidCD = new FloatOptionItem(Id + 10, "AidCD", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
            .SetValueFormat(OptionFormat.Seconds);

        AidDur = new FloatOptionItem(Id + 11, "AidDur", new(0f, 60f, 1f), 10f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
            .SetValueFormat(OptionFormat.Seconds);

        TargetKnowsShield = new BooleanOptionItem(Id + 14, "AidTargetKnowsAboutShield", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid]);

        UseLimitOpt = new IntegerOptionItem(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
            .SetValueFormat(OptionFormat.Times);

        UsePet = Options.CreatePetUseSetting(Id + 13, CustomRoles.Aid);
    }

    public override void Init()
    {
        On = false;
        ShieldedPlayers = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
        TargetId = byte.MaxValue;
        AidId = playerId;
    }

    public override void SetKillCooldown(byte playerId)
    {
        Main.AllPlayerKillCooldown[playerId] = AidCD.GetInt();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() >= 1;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() >= 1 && TargetId != byte.MaxValue;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;

        if (target == null) return false;

        TargetId = target.PlayerId;
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc == null || !pc.Is(CustomRoles.Aid) || ShieldedPlayers.Count == 0) return;

        var change = false;

        foreach (KeyValuePair<byte, long> x in ShieldedPlayers.ToArray())
        {
            if (x.Value + AidDur.GetInt() <= Utils.TimeStamp || !GameStates.IsInTask)
            {
                ShieldedPlayers.Remove(x.Key);
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, x.Key);
                change = true;
            }
        }

        if (change && GameStates.IsInTask) Utils.NotifyRoles(SpecifySeer: pc);
    }

    public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
    {
        PlayerControl pc = physics.myPlayer;

        if (pc.GetAbilityUseLimit() >= 1 && TargetId != byte.MaxValue)
        {
            pc.RpcRemoveAbilityUse();
            ShieldedPlayers[TargetId] = Utils.TimeStamp;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 0, TargetId);
            PlayerControl target = Utils.GetPlayerById(TargetId);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
            TargetId = byte.MaxValue;
        }

        LateTask.New(() => physics.RpcBootFromVent(ventId), 0.5f, log: false);
    }

    public override void OnReportDeadBody()
    {
        ShieldedPlayers.Clear();
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 0:
                ShieldedPlayers[reader.ReadByte()] = Utils.TimeStamp;
                break;
            case 1:
                ShieldedPlayers.Remove(reader.ReadByte());
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || (seer.IsModClient() && !hud)) return string.Empty;

        if (TargetKnowsShield.GetBool() && ShieldedPlayers.TryGetValue(seer.PlayerId, out long ts))
        {
            long timeLeft = AidDur.GetInt() - (Utils.TimeStamp - ts);
            return string.Format(Translator.GetString("AidCounterSelf"), timeLeft);
        }

        if (seer.PlayerId == AidId)
        {
            int duration = AidDur.GetInt();
            long now = Utils.TimeStamp;
            IEnumerable<string> formatted = ShieldedPlayers.Select(x => string.Format(Translator.GetString("AidCounterTarget"), x.Key.ColoredPlayerName(), duration - (now - x.Value)));
            return string.Join("\n", formatted);
        }

        return string.Empty;
    }
}