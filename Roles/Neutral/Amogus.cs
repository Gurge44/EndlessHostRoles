using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Amogus : RoleBase
{
    public static bool On;
    private static List<Amogus> Instances = [];

    private static OptionItem StartingLevel;
    private static OptionItem SugomaSpeed;
    private static OptionItem SuspiciousSusArrowsToBodies;
    private static OptionItem UltimateSusVotesPerKill;
    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;
    private long AmogusFormEndTS;

    private byte AmogusID;
    private Levels CurrentLevel;
    public int ExtraVotes;

    public override bool IsEnable => On;

    public override bool SeesArrowsToDeadBodies => CurrentLevel >= Levels.SuspiciousSus && SuspiciousSusArrowsToBodies.GetBool() && AmogusFormEndTS != 0;

    public override void SetupCustomOption()
    {
        StartSetup(649075)
            .AutoSetupOption(ref StartingLevel, 0, Enum.GetValues<Levels>().Select(x => $"Amogus.Levels.{x}").ToArray())
            .AutoSetupOption(ref SugomaSpeed, 2f, new FloatValueRule(0.1f, 3f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref SuspiciousSusArrowsToBodies, false)
            .AutoSetupOption(ref UltimateSusVotesPerKill, 1, new IntegerValueRule(0, 10, 1), OptionFormat.Votes)
            .AutoSetupOption(ref AbilityCooldown, 20f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 15f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        AmogusID = playerId;
        AmogusFormEndTS = 0;
        CurrentLevel = (Levels)StartingLevel.GetValue();
        ExtraVotes = 0;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        float kcd = KillCooldown.GetFloat();
        if (CurrentLevel == Levels.UltimateSus) kcd /= 2f;
        Main.AllPlayerKillCooldown[id] = kcd;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return AmogusFormEndTS == 0;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());

        if (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool())
        {
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetInt();
            AURoleOptions.PhantomDuration = AbilityDuration.GetInt();
        }

        if (CurrentLevel >= Levels.Sugoma) Main.AllPlayerSpeed[id] = SugomaSpeed.GetFloat();
        if (CurrentLevel == Levels.Sugoma) Main.AllPlayerSpeed[id] *= -1;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void OnPet(PlayerControl pc)
    {
        if (CurrentLevel >= Levels.SuspiciousSus)
            Main.AllAlivePlayerControls.Do(x => TargetArrow.Add(AmogusID, x.PlayerId));

        AmogusFormEndTS = Utils.TimeStamp + AbilityDuration.GetInt();
        Utils.SendRPC(CustomRPC.SyncRoleData, AmogusID, 1, AmogusFormEndTS);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        OnPet(pc);
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || !pc.IsAlive()) return;

        long now = Utils.TimeStamp;

        if (AmogusFormEndTS != 0 && AmogusFormEndTS <= now)
        {
            FormExpired();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    private void FormExpired()
    {
        TargetArrow.RemoveAllTarget(AmogusID);
        LocateArrow.RemoveAllTarget(AmogusID);

        AmogusFormEndTS = 0;
        Utils.SendRPC(CustomRPC.SyncRoleData, AmogusID, 1, AmogusFormEndTS);
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (CurrentLevel < Levels.UltimateSus)
        {
            CurrentLevel++;
            Utils.SendRPC(CustomRPC.SyncRoleData, AmogusID, 2, (int)CurrentLevel);
        }

        switch (CurrentLevel)
        {
            case Levels.SuspiciousSus:
                Main.AllAlivePlayerControls.Do(x => TargetArrow.Add(AmogusID, x.PlayerId));
                break;
            case Levels.UltimateSus:
                ExtraVotes += UltimateSusVotesPerKill.GetInt();
                Utils.SendRPC(CustomRPC.SyncRoleData, AmogusID, 3, ExtraVotes);
                break;
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return CurrentLevel < Levels.Sumogus;
    }

    public override void OnReportDeadBody()
    {
        FormExpired();
    }

    public static void OnAnyoneDead(PlayerControl target)
    {
        Instances.DoIf(x => x.CurrentLevel >= Levels.SuspiciousSus && x.AmogusFormEndTS != 0, x => TargetArrow.Remove(x.AmogusID, target.PlayerId));
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                AmogusFormEndTS = long.Parse(reader.ReadString());
                break;
            case 2:
                CurrentLevel = (Levels)reader.ReadPackedInt32();
                break;
            case 3:
                ExtraVotes = reader.ReadPackedInt32();
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != AmogusID || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;

        var sb = new StringBuilder();
        if (AmogusFormEndTS != 0) sb.Append($"\u25a9 ({Utils.TimeStamp - AmogusFormEndTS}s)\n");
        if (!hud) sb.Append("<size=70%>");
        sb.Append(string.Format(Translator.GetString("Amogus.Suffix"), Translator.GetString($"Amogus.Levels.{CurrentLevel}")));
        if (!hud) sb.Append("</size>");
        return sb.ToString();
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return $"{base.GetProgressText(playerId, comms)} {string.Format(Translator.GetString("ExtraVotesPT"), ExtraVotes)}";
    }

    private enum Levels
    {
        Amogus,
        Sugoma,
        Sumogus,
        SuspiciousSus,
        UltimateSus
    }
}
