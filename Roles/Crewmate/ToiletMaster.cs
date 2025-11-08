using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate;

public class ToiletMaster : RoleBase
{
    public static bool On;
    private static List<ToiletMaster> Instances = [];

    public static OptionItem AbilityCooldown;
    private static OptionItem AbilityUses;
    private static OptionItem ToiletDuration;
    private static OptionItem ToiletVisibility;
    private static OptionItem ToiletUseRadius;
    private static OptionItem ToiletUseTime;
    private static OptionItem ToiletMaxUses;
    private static OptionItem BrownPoopSpeedBoost;
    private static OptionItem GreenPoopRadius;
    private static OptionItem RedPoopRadius;
    private static OptionItem RedPoopRoleBlockDuration;
    private static OptionItem PurplePoopNotifyOnKillAttempt;
    public static OptionItem AbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    private static readonly Dictionary<Poop, OptionItem> PoopDurationSettings = [];

    private Dictionary<byte, (Poop Poop, long TimeStamp, object Data)> ActivePoops = [];

    private long LastUpdate;
    private Dictionary<byte, long> PlayersUsingToilet = [];

    private Dictionary<Vector2, (Toilet NetObject, int Uses, long PlaceTimeStamp)> Toilets = [];
    private static ToiletVisibilityOptions ToiletVisible => (ToiletVisibilityOptions)ToiletVisibility.GetValue();
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 644700;
        const TabGroup tab = TabGroup.CrewmateRoles;
        Poop[] poops = Enum.GetValues<Poop>();
        Dictionary<string, string> replacements = poops.ToDictionary(x => x.ToString(), x => Utils.ColorString(GetPoopColor(x), x.ToString()));

        SetupRoleOptions(id++, tab, CustomRoles.ToiletMaster);

        AbilityCooldown = new IntegerOptionItem(++id, "AbilityCooldown", new(0, 60, 1), 5, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
            .SetValueFormat(OptionFormat.Seconds);

        AbilityUses = new IntegerOptionItem(++id, "AbilityUseLimit", new(0, 10, 1), 3, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);

        ToiletDuration = new IntegerOptionItem(++id, "TM.ToiletDuration", new(0, 60, 1), 10, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
            .SetValueFormat(OptionFormat.Seconds);

        ToiletVisibility = new StringOptionItem(++id, "TM.ToiletVisibility", Enum.GetNames<ToiletVisibilityOptions>(), 0, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);

        ToiletUseRadius = new FloatOptionItem(++id, "TM.ToiletUseRadius", new(0f, 5f, 0.25f), 1f, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
            .SetValueFormat(OptionFormat.Multiplier);

        ToiletUseTime = new IntegerOptionItem(++id, "TM.ToiletUseTime", new(0, 60, 1), 5, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
            .SetValueFormat(OptionFormat.Seconds);

        ToiletMaxUses = new IntegerOptionItem(++id, "TM.ToiletMaxUses", new(0, 10, 1), 3, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);

        BrownPoopSpeedBoost = new FloatOptionItem(++id, "TM.BrownPoopSpeedBoost", new(0f, 3f, 0.05f), 0.5f, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
            .SetValueFormat(OptionFormat.Multiplier);

        GreenPoopRadius = new FloatOptionItem(++id, "TM.GreenPoopRadius", new(0f, 5f, 0.25f), 1f, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
            .SetValueFormat(OptionFormat.Multiplier);

        RedPoopRadius = new FloatOptionItem(++id, "TM.RedPoopRadius", new(0f, 5f, 0.25f), 1f, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
            .SetValueFormat(OptionFormat.Multiplier);

        RedPoopRoleBlockDuration = new IntegerOptionItem(++id, "TM.RedPoopRoleBlockDuration", new(0, 60, 1), 10, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
            .SetValueFormat(OptionFormat.Seconds);

        PurplePoopNotifyOnKillAttempt = new BooleanOptionItem(++id, "TM.PurplePoopNotifyOnKillAttempt", false, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);

        AbilityUseGainWithEachTaskCompleted = new FloatOptionItem(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.6f, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.1f, tab)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster]);

        poops.Do(x =>
        {
            PoopDurationSettings[x] = new IntegerOptionItem(++id, $"TM.{x}PoopDuration", new(0, 60, 1), 10, tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ToiletMaster])
                .SetValueFormat(OptionFormat.Seconds);
        });

        PoopDurationSettings.Values
            .CombineWith([BrownPoopSpeedBoost], [GreenPoopRadius], [RedPoopRadius], [RedPoopRoleBlockDuration], [PurplePoopNotifyOnKillAttempt])
            .Do(x => x.ReplacementDictionary = replacements);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
        Toilets = [];
        ActivePoops = [];
        PlayersUsingToilet = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        LastUpdate = 8 + AbilityCooldown.GetInt() + ToiletDuration.GetInt();
        playerId.SetAbilityUseLimit(AbilityUses.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void OnPet(PlayerControl pc)
    {
        Vector2 pos = pc.Pos();

        IEnumerable<PlayerControl> hideList = ToiletVisible switch
        {
            ToiletVisibilityOptions.Instant => [],
            _ => Main.AllPlayerControls.Without(pc)
        };

        Toilets[pos] = (new(pos, hideList), 0, Utils.TimeStamp);
        pc.RpcRemoveAbilityUse();

        if (ToiletVisible == ToiletVisibilityOptions.Delayed) LateTask.New(() => Toilets[pos] = (new(pos, []), Toilets[pos].Uses, Toilets[pos].PlaceTimeStamp), 5f, log: false);
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (lowLoad || !pc.IsAlive() || !GameStates.IsInTask) return;

        if (ActivePoops.TryGetValue(pc.PlayerId, out (Poop Poop, long TimeStamp, object Data) activePoop))
        {
            if (activePoop.TimeStamp + PoopDurationSettings[activePoop.Poop].GetInt() <= Utils.TimeStamp)
            {
                ActivePoops.Remove(pc.PlayerId);

                switch (activePoop.Poop)
                {
                    case Poop.Brown:
                        Main.AllPlayerSpeed[pc.PlayerId] -= BrownPoopSpeedBoost.GetFloat();
                        break;
                    case Poop.Red:
                        float defaultSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

                        ((List<PlayerControl>)activePoop.Data).DoIf(x => x != null, x =>
                        {
                            Main.AllPlayerSpeed[x.PlayerId] = defaultSpeed;
                            x.MarkDirtySettings();
                        });

                        break;
                }
            }

            return;
        }

        try
        {
            Vector2 pos = pc.Pos();
            (Toilet NetObject, int Uses, long PlaceTimeStamp) toilet = Toilets.First(x => Vector2.Distance(x.Key, pos) <= ToiletUseRadius.GetFloat()).Value;

            if (toilet.Uses >= AbilityUses.GetInt()) return;

            if (!PlayersUsingToilet.TryGetValue(pc.PlayerId, out long ts))
            {
                PlayersUsingToilet[pc.PlayerId] = Utils.TimeStamp;
                return;
            }

            if (ts + ToiletUseTime.GetInt() > Utils.TimeStamp) return;

            toilet.Uses++;

            Poop poop = Enum.GetValues<Poop>().RandomElement();
            if (poop != Poop.Green) pc.Notify(Utils.ColorString(GetPoopColor(poop), string.Format(Translator.GetString("TM.GetPoopNotify"), poop)));

            switch (poop)
            {
                case Poop.Brown:
                    Main.AllPlayerSpeed[pc.PlayerId] += BrownPoopSpeedBoost.GetFloat();
                    ActivePoops[pc.PlayerId] = (poop, Utils.TimeStamp, null);
                    break;
                case Poop.Green:
                    float radius = GreenPoopRadius.GetFloat();
                    bool isKillerNearby = Main.AllAlivePlayerControls.Any(x => x.PlayerId != pc.PlayerId && Vector2.Distance(x.Pos(), pos) <= radius && (x.IsImpostor() || x.IsNeutralKiller()));
                    Color color = isKillerNearby ? Color.red : Color.green;
                    string str = Translator.GetString(isKillerNearby ? "TM.GreenPoopKiller" : "TM.GreenPoop");
                    pc.Notify(Utils.ColorString(color, str));
                    break;
                case Poop.Red:
                    int duration = RedPoopRoleBlockDuration.GetInt();
                    List<PlayerControl> affectedPlayers = [];

                    Utils.GetPlayersInRadius(RedPoopRadius.GetFloat(), pos).Without(pc).Do(x =>
                    {
                        x.BlockRole(duration);
                        Main.AllPlayerSpeed[x.PlayerId] = Main.MinSpeed;
                        x.MarkDirtySettings();
                        affectedPlayers.Add(x);

                        if (x.AmOwner)
                            Achievements.Type.TooCold.CompleteAfterGameEnd();
                    });

                    ActivePoops[pc.PlayerId] = (poop, Utils.TimeStamp, affectedPlayers);
                    break;
                case Poop.Blue:
                case Poop.Purple:
                    ActivePoops[pc.PlayerId] = (poop, Utils.TimeStamp, null);
                    break;
            }

            PlayersUsingToilet.Remove(pc.PlayerId);
            Logger.Info($"{pc.GetNameWithRole()} used a toilet => {poop} poop", "ToiletMaster");
        }
        catch { PlayersUsingToilet.Remove(pc.PlayerId); }
    }

    public override void AfterMeetingTasks()
    {
        if (ToiletVisible == ToiletVisibilityOptions.AfterMeeting)
            Toilets.Values.Do(x => x.NetObject = new(Toilets.GetKeyByValue((x.NetObject, x.Uses, x.PlaceTimeStamp)), []));
    }

    private static Color GetPoopColor(Poop poop)
    {
        return poop switch
        {
            Poop.Brown => Palette.Brown,
            Poop.Green => Color.green,
            Poop.Red => Color.red,
            Poop.Purple => Palette.Purple,
            Poop.Blue => Color.blue,
            _ => Color.white
        };
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || !GameStates.IsInTask) return;

        long now = Utils.TimeStamp;
        if (LastUpdate >= now) return;

        LastUpdate = now;

        int duration = ToiletDuration.GetInt();
        int maxUses = ToiletMaxUses.GetInt();

        Toilets.DoIf(x => x.Value.PlaceTimeStamp + duration <= now || x.Value.Uses >= maxUses, x =>
        {
            Toilets.Remove(x.Key);
            x.Value.NetObject.Despawn();
        }, false);
    }

    public static bool OnAnyoneCheckMurderStart(PlayerControl killer, PlayerControl target)
    {
        foreach (ToiletMaster tm in Instances)
        {
            if (tm.ActivePoops.TryGetValue(killer.PlayerId, out (Poop Poop, long TimeStamp, object Data) poop) && poop.Poop == Poop.Blue)
            {
                killer.RpcCheckAndMurder(target);
                return true;
            }
        }

        return false;
    }

    public static bool OnAnyoneCheckMurder(PlayerControl killer, PlayerControl target)
    {
        foreach (ToiletMaster tm in Instances)
        {
            if (tm.ActivePoops.TryGetValue(killer.PlayerId, out (Poop Poop, long TimeStamp, object Data) poop) && poop.Poop == Poop.Purple)
            {
                if (PurplePoopNotifyOnKillAttempt.GetBool())
                    target.Notify(Translator.GetString("TM.TryKillNotify"));

                return false;
            }
        }

        return true;
    }

    private enum ToiletVisibilityOptions
    {
        Instant,
        Delayed,
        AfterMeeting,

        // ReSharper disable once UnusedMember.Local
        Invisible
    }

    private enum Poop
    {
        Brown,
        Green,
        Red,
        Purple,
        Blue
    }
}