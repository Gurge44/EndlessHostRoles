using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Roles;

public class Tree : RoleBase
{
    public static bool On;

    public const string Sprite = "<voffset=7em><alpha=#00>.</alpha></voffset><size=150%><line-height=97%><cspace=0.16em><#0000>WW</color><mark=#6ef514>WWWW</mark><#0000>WW\nW</color><mark=#6ef514>WWWWWW</mark><#0000>W</color>\n<mark=#6ef514>WWWWWWWW</mark>\n<mark=#6ef514>WWWWWWWW</mark>\n<mark=#6ef514>WWWWWWWW</mark>\n<#0000>WWW</color><mark=#711e1e>WW</mark><#0000>WWW\nWWW</color><mark=#711e1e>WW</mark><#0000>WWW\nWWW</color><mark=#711e1e>WW</mark><#0000>WWW";
    public const string FallenSprite = "<size=150%><line-height=97%><cspace=0.16em><#0000>WWWW</color><mark=#6ef514>WW</mark><#0000>WW\nWWWW</color><mark=#6ef514>WWW</mark><#0000>W\nWWWW</color><mark=#6ef514>WWWW</mark>\n<mark=#6f1515>WWWW</mark><mark=#6ef514>WWWW</mark>\n<mark=#6f1515>WWWW</mark><mark=#6ef514>WWWW";
    
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
            
            FastVector2.GetPlayersInRange(pc.Pos(), FallRadius.GetFloat()).Without(pc).Do(x =>
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