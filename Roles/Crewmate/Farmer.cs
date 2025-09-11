using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Crewmate;

public class Farmer : RoleBase
{
    public static bool On;
    private static List<Farmer> Instances = [];

    public override bool IsEnable => On;

    private List<Seed> Seeds;
    private List<(Seed Seed, EHR.Seed NetObject)> SeedPositions;
    private Dictionary<Seed, long> ActiveSeeds;
    private byte FarmerId;

    private static OptionItem HarvestRange;
    private static OptionItem IncreasedVision;
    private static OptionItem IncreasedVisionDuration;
    private static OptionItem IncreasedSpeed;
    private static OptionItem IncreasedSpeedDuration;
    private static OptionItem ShieldDuration;
    private static OptionItem InvisibilityDuration;

    public override void SetupCustomOption()
    {
        StartSetup(654000)
            .AutoSetupOption(ref HarvestRange, 1.5f, new FloatValueRule(0.25f, 5f, 0.25f), OptionFormat.Multiplier)
            .AutoSetupOption(ref IncreasedVision, 1f, new FloatValueRule(0.1f, 2f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref IncreasedVisionDuration, 15, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref IncreasedSpeed, 2f, new FloatValueRule(0.25f, 3f, 0.25f), OptionFormat.Multiplier)
            .AutoSetupOption(ref IncreasedSpeedDuration, 30, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ShieldDuration, 15, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref InvisibilityDuration, 30, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Seeds = [];
        SeedPositions = [];
        ActiveSeeds = [];
        FarmerId = playerId;
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        Seeds.Add(Enum.GetValues<Seed>().RandomElement());
    }

    public override void OnPet(PlayerControl pc)
    {
        Vector2 pos = pc.Pos();
        float range = HarvestRange.GetFloat();

        if (SeedPositions.FindFirst(x => x.NetObject.Spawned && Vector2.Distance(pos, x.NetObject.Position) < range, out var existing))
        {
            switch (existing.Seed)
            {
                case Seed.Wheat:
                    Utils.MarkEveryoneDirtySettings();
                    goto default;
                case Seed.Apple:
                    pc.RpcMakeInvisible();
                    goto default;
                case Seed.Tomato:
                    Main.AllPlayerSpeed[pc.PlayerId] = IncreasedSpeed.GetFloat();
                    pc.MarkDirtySettings();
                    goto default;
                case Seed.Potato:
                    Main.AllAlivePlayerControls.DoIf(x => !x.Is(Team.Crewmate), x => x.SetKillCooldown(Main.AllPlayerKillCooldown.GetValueOrDefault(x.PlayerId, -1f)));
                    break;
                case Seed.Blueberry:
                    Object.FindObjectsOfType<DeadBody>().Do(x => LocateArrow.Add(pc.PlayerId, x.TruePosition));
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    break;
                default:
                    ActiveSeeds[existing.Seed] = Utils.TimeStamp + existing.Seed switch
                    {
                        Seed.Wheat => IncreasedVisionDuration.GetInt(),
                        Seed.Carrot => ShieldDuration.GetInt(),
                        Seed.Apple => InvisibilityDuration.GetInt(),
                        Seed.Tomato => IncreasedSpeedDuration.GetInt(),
                        _ => 0
                    };
                    break;
            }
            
            existing.NetObject.Despawn();
            SeedPositions.Remove(existing);
            return;
        }
        
        if (Seeds.Count == 0) return;
        Seed seed = Seeds[0];
        SeedPositions.Add((seed, new(pos, GetHexColor(seed))));
        Seeds.RemoveAt(0);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    private static string GetHexColor(Seed s) => s switch
    {
        Seed.Wheat => "FFFF00",
        Seed.Carrot => "FFA500",
        Seed.Apple => "FF0000",
        Seed.Tomato => "FF6347",
        Seed.Potato => "D2B48C",
        Seed.Blueberry => "0000FF",
        _ => "FFFFFF"
    };

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
        
        Seed[] toRemove = ActiveSeeds.Where(x => x.Value <= Utils.TimeStamp).Select(x => x.Key).ToArray();

        foreach (Seed seed in toRemove)
        {
            ActiveSeeds.Remove(seed);

            switch (seed)
            {
                case Seed.Wheat:
                    Utils.MarkEveryoneDirtySettings();
                    break;
                case Seed.Apple:
                    pc.RpcMakeVisible();
                    break;
                case Seed.Tomato:
                    Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                    pc.MarkDirtySettings();
                    break;
            }
        }
    }

    public override void AfterMeetingTasks()
    {
        SeedPositions.ForEach(x => x.NetObject.SpawnIfNotSpawned());
    }

    public override void OnReportDeadBody()
    {
        LocateArrow.RemoveAllTarget(FarmerId);
    }

    public static void OnAnyoneApplyGameOptions(IGameOptions opt, PlayerControl player)
    {
        if (On && player.Is(Team.Crewmate) && Instances.Exists(x => x.ActiveSeeds.ContainsKey(Seed.Wheat)))
        {
            opt.SetFloat(FloatOptionNames.CrewLightMod, IncreasedVision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, IncreasedVision.GetFloat());
        }
    }

    public static bool OnAnyoneCheckMurder(PlayerControl target)
    {
        return !On || !target.Is(Team.Crewmate) || !Instances.Exists(x => x.ActiveSeeds.ContainsKey(Seed.Carrot));
    }

    enum Seed
    {
        Wheat, // Increased vision for all crewmates
        Carrot, // Shield for all crewmates
        Apple, // Invisibility (self only)
        Tomato, // Speed boost (self only)
        Potato, // Reset all killers' cooldowns
        Blueberry // Arrow to all dead bodies
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != FarmerId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;

        StringBuilder sb = new();
        sb.AppendLine(Seeds.Count > 0 ? string.Format(Translator.GetString("Farmer.SelectedSeed"), Main.RoleColors[CustomRoles.Farmer], $"<#{GetHexColor(Seeds[0])}>{Translator.GetString($"Farmer.Seed.{Seeds[0]}")}</color>") : string.Format(Translator.GetString("Farmer.CompleteTaskToGetSeed"), Main.RoleColors[CustomRoles.Farmer]));
        if (Seeds.Count > 1) sb.AppendLine(string.Format(Translator.GetString("Farmer.MoreSeeds"), Seeds.Count - 1));
        sb.Append(LocateArrow.GetArrows(seer));
        return sb.ToString().Trim();
    }
}