using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Crewmate;

public class Tree : RoleBase
{
    public static bool On;

    public const string Sprite = "<voffset=7em><alpha=#00>.</alpha></voffset><size=150%><font=\"VCR SDF\"><line-height=67%><alpha=#00>█<alpha=#00>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<alpha=#00>█<br><#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<br><#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<br><#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<#711e1e>█<#711e1e>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<#711e1e>█<#711e1e>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<#711e1e>█<#711e1e>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></font></size>";
    public const string FallenSprite = "<size=150%><font=\"VCR SDF\"><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#6ef514>█<#6ef514>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#6ef514>█<#6ef514>█<#6ef514>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<br><#6f1515>█<#6f1515>█<#6f1515>█<#6f1515>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<br><#6f1515>█<#6f1515>█<#6f1515>█<#6f1515>█<#6ef514>█<#6ef514>█<#6ef514>█<#6ef514>█<br></line-height></font></size>";
    
    public override bool IsEnable => On;

    private static OptionItem TreeSpriteVisible;
    public static OptionItem FallDelay;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;
    private static OptionItem FallRadius;
    private static OptionItem FallKillChance;
    public static OptionItem FallStunDuration;

    public bool TreeSpriteActive;

    public override void SetupCustomOption()
    {
        StartSetup(655100)
            .AutoSetupOption(ref TreeSpriteVisible, false)
            .AutoSetupOption(ref FallDelay, 5, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.5f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref FallRadius, 3f, new FloatValueRule(0.25f, 10f, 0.25f), OptionFormat.Multiplier)
            .AutoSetupOption(ref FallKillChance, 10, new IntegerValueRule(0, 100, 5), OptionFormat.Percent)
            .AutoSetupOption(ref FallStunDuration, 5, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
        TreeSpriteActive = false;
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;
        pc.RpcRemoveAbilityUse();

        Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
        pc.MarkDirtySettings();

        bool treeSpriteVisible = TreeSpriteVisible.GetBool();
        
        if (treeSpriteVisible)
        {
            TreeSpriteActive = true;
            Utils.NotifyRoles(SpecifyTarget: pc);
        }
        else
            pc.RpcMakeInvisible();
        
        LateTask.New(() =>
        {
            Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || pc == null || !pc.IsAlive()) return;
            pc.MarkDirtySettings();
            
            if (treeSpriteVisible)
            {
                TreeSpriteActive = false;
                Utils.NotifyRoles(SpecifyTarget: pc);
            }
            else
                pc.RpcMakeVisible();
            
            int chance = FallKillChance.GetInt();
            bool allKill = true;
            bool any = false;
            
            Utils.GetPlayersInRadius(FallRadius.GetFloat(), pc.Pos()).Without(pc).Do(x =>
            {
                any = true;
                if (IRandom.Instance.Next(100) < chance) x.Suicide(PlayerState.DeathReason.Fall, pc);
                else
                {
                    allKill = false;
                    Main.AllPlayerSpeed[x.PlayerId] = Main.MinSpeed;
                    x.MarkDirtySettings();
                    
                    LateTask.New(() =>
                    {
                        Main.AllPlayerSpeed[x.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || pc == null || !pc.IsAlive()) return;
                        x.MarkDirtySettings();
                    }, FallStunDuration.GetFloat(), log: false);
                }
            });

            var cno = new FallenTree(pc.Pos());
            LateTask.New(() => cno.Despawn(), FallStunDuration.GetFloat(), log: false);
            
            if (pc.AmOwner && any && allKill)
                Achievements.Type.MyBad.Complete();
        }, FallDelay.GetFloat(), log: false);
    }
}