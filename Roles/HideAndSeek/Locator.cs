using System.Linq;
using EHR.Gamemodes;
using EHR.Modules.Extensions;

namespace EHR.Roles;

public class Locator : RoleBase, IHideAndSeekRole
{
    public static bool On;

    private static OptionItem ArrowFrequency;
    private static OptionItem ArrowDuration;
    private static OptionItem HidersKnowTheyAreLocated;
    private static OptionItem Vision;
    private static OptionItem Speed;

    private byte LocatorId;
    private byte TargetId;

    public override bool IsEnable => On;
    public Team Team => Team.Impostor;
    public int Chance => CustomRoles.Locator.GetMode();
    public int Count => CustomRoles.Locator.GetCount();
    public float RoleSpeed => Speed.GetFloat();
    public float RoleVision => Vision.GetFloat();

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(69_211_901, TabGroup.ImpostorRoles, CustomRoles.Locator, CustomGameMode.HideAndSeek);

        Vision = new FloatOptionItem(69_211_903, "LocatorVision", new(0.05f, 5f, 0.05f), 0.25f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(245, 158, 66, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);

        Speed = new FloatOptionItem(69_213_904, "LocatorSpeed", new(0.05f, 5f, 0.05f), 1.5f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(245, 158, 66, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);

        ArrowFrequency = new IntegerOptionItem(69_213_905, "LocatorFrequency", new(0, 60, 1), 20, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(245, 158, 66, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);

        ArrowDuration = new FloatOptionItem(69_213_906, "LocatorDuration", new(1f, 30f, 1f), 5f, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(245, 158, 66, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);

        HidersKnowTheyAreLocated = new BooleanOptionItem(69_213_907, "LocatorTargetKnows", true, TabGroup.ImpostorRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(new(245, 158, 66, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Locator]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        TargetId = byte.MaxValue;
        int arrowDuration = ArrowDuration.GetInt();
        int arrowFrequency = ArrowFrequency.GetInt();
        var timer = new CountdownTimer(8 + arrowFrequency, OnElapsed, cancelOnMeeting: false);
        LocatorId = playerId;
        return;

        void OnElapsed()
        {
            PlayerControl pc = Utils.GetPlayerById(playerId);
            if (pc == null || !pc.IsAlive()) return;

            PlayerControl target = CustomHnS.PlayerRoles.Where(x => x.Value.Interface.Team != Team.Impostor).Select(x => Utils.GetPlayerById(x.Key)).Where(x => x != null && x.IsAlive()).Shuffle().FirstOrDefault();

            if (target != null)
            {
                TargetId = target.PlayerId;
                TargetArrow.Add(pc.PlayerId, target.PlayerId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                if (HidersKnowTheyAreLocated.GetBool()) target.Notify(Translator.GetString("LocatorNotify"));
                
                timer = new CountdownTimer(arrowFrequency + arrowDuration, OnElapsed, cancelOnMeeting: false);
                
                LateTask.New(() =>
                {
                    if (timer.IsCanceled())
                    {
                        timer.Dispose();
                        return;
                    }
                    
                    TargetArrow.Remove(pc.PlayerId, TargetId);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    TargetId = byte.MaxValue;
                }, arrowDuration);
            }
        }
    }

    public override void Init()
    {
        On = false;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || hud || seer.PlayerId != LocatorId) return string.Empty;
        return TargetId == byte.MaxValue ? string.Empty : TargetArrow.GetArrows(seer, TargetId);
    }
}