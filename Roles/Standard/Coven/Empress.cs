using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Roles;

public class Empress : CovenBase
{
    public static bool On;
    private static List<Empress> Instances = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    private static OptionItem KillCooldown;
    private static OptionItem AbilityCooldown;
    private static OptionItem CoverageRange;
    private static OptionItem CoverageDelay;
    private static OptionItem CoverageDuration;
    private static OptionItem FrostGazeDelay;
    private static OptionItem FrostGazeDuration;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private byte EmpressId;
    private bool Empowered;
    private bool Weakened;
    private Spells SelectedSpell;
    private HashSet<byte> FrostGazed;
    private HashSet<byte> Killed;

    public static HashSet<byte> Encouraged = [];

    public override void SetupCustomOption()
    {
        StartSetup(657600)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityCooldown, 15f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CoverageRange, 8f, new FloatValueRule(0.25f, 30f, 0.25f), OptionFormat.Multiplier)
            .AutoSetupOption(ref CoverageDelay, 3f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CoverageDuration, 5f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref FrostGazeDelay, 5f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref FrostGazeDuration, 5f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
        Encouraged = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        EmpressId = playerId;
        Empowered = false;
        Weakened = false;
        SelectedSpell = default;
        FrostGazed = [];
        Killed = [];
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
        Encouraged = [];
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = 0.1f;
        AURoleOptions.PhantomDuration = 0.1f;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = HasNecronomicon ? KillCooldown.GetFloat() : AbilityCooldown.GetFloat();
    }

    public override void OnReceiveNecronomicon()
    {
        SetKillCooldown(EmpressId);
    }

    public override bool OnVote(PlayerControl voter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak || voter == null || target == null || Main.DontCancelVoteList.Contains(voter.PlayerId) || voter.PlayerId != target.PlayerId || Empowered) return false;

        Empower(voter);

        Main.DontCancelVoteList.Add(voter.PlayerId);
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        Empower(shapeshifter);
    }

    private void Empower(PlayerControl voter)
    {
        if (Weakened)
        {
            Utils.SendMessage("\n", voter.PlayerId, Translator.GetString("Empress.EmpowerFailWeakenedMessage"));
            return;
        }

        Empowered = true;
        Utils.SendMessage("\n", voter.PlayerId, Translator.GetString("Empress.EmpowerSuccessMessage"));
    }

    public override void OnReportDeadBody()
    {
        Killed = [];
        
        Encouraged.Do(x => Main.AllPlayerSpeed[x] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
        Encouraged = [];
        
        if (Weakened)
        {
            Weakened = false;
            return;
        }
        
        if (Empowered)
        {
            Empowered = false;
            Weakened = true;
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (HasNecronomicon) return killer.CheckDoubleTrigger(target, CastSpell);
        
        CastSpell();
        return false;

        void CastSpell()
        {
            float cd = AbilityCooldown.GetFloat();

            switch (SelectedSpell)
            {
                case Spells.FrostGaze:
                {
                    float duration = FrostGazeDuration.GetFloat();
                    float delay = FrostGazeDelay.GetFloat();
                    LateTask.New(() =>
                    {
                        if (GameStates.IsEnded || ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting || !FrostGazed.Add(target.PlayerId)) return;
                        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                        target.MarkDirtySettings();

                        LateTask.New(() =>
                        {
                            if (!FrostGazed.Remove(target.PlayerId)) return;
                            Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                            if (GameStates.IsEnded || ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting) return;
                            target.MarkDirtySettings();
                            target.Suicide(realKiller: killer);
                        }, duration, "EmpressFrostGazeEnd");
                    }, delay, "EmpressFrostGazeStart");
                    cd += delay + duration;
                    killer.SetKillCooldown(cd);
                    killer.AddAbilityCD((int)Math.Round(cd));
                    break;
                }
                case Spells.Encourage when target.Is(Team.Coven) && Encouraged.Add(target.PlayerId):
                {
                    Main.AllPlayerSpeed[target.PlayerId] *= 1.25f;
                    target.MarkDirtySettings();
                    killer.SetKillCooldown(cd);
                    killer.AddAbilityCD((int)Math.Round(cd));
                    break;
                }
            }
        }
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (SelectedSpell == Spells.Encourage) SelectedSpell = Spells.FrostGaze;
        else SelectedSpell++;

        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        PlayerControl[] playersInRadius = FastVector2.GetPlayersInRange(pc.Pos(), CoverageRange.GetFloat()).Where(x => x.Is(Team.Coven)).ToArray();
        float duration = CoverageDuration.GetFloat();
        float delay = CoverageDelay.GetFloat();
        LateTask.New(() =>
        {
            if (GameStates.IsEnded || ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting) return;
            playersInRadius.DoIf(x => x.Is(Team.Coven), x => x.RpcMakeInvisible());
            LateTask.New(() =>
            {
                if (GameStates.IsEnded || ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting) return;
                playersInRadius.DoIf(x => x.Is(Team.Coven), x => x.RpcMakeVisible());
            }, duration, "EmpressCoverageEnd");
        }, delay, "EmpressCoverageStart");
        float cd = AbilityCooldown.GetFloat() + delay + duration;
        pc.SetKillCooldown(cd);
        pc.AddAbilityCD((int)Math.Round(cd));
    }

    public static void OnInteraction(PlayerControl target)
    {
        foreach (Empress instance in Instances)
            instance.FrostGazed.Remove(target.PlayerId);
    }

    public static void OnAnyoneMurder(PlayerControl killer)
    {
        if (!killer.Is(Team.Coven)) return;
        
        foreach (Empress instance in Instances)
        {
            if (!instance.Empowered) continue;

            if (instance.Killed.Add(killer.PlayerId))
            {
                LateTask.New(() => killer.SetKillCooldown(0.01f), 0.2f, log: false);
                return;
            }

            LateTask.New(() =>
            {
                killer.ResetKillCooldown(sync: false);
                killer.SetKillCooldown(Main.AllPlayerKillCooldown[killer.PlayerId] * 2f);
            }, 0.2f, log: false);
            return;
        }
    }

    public enum Spells
    {
        FrostGaze,
        Encourage
    }
}