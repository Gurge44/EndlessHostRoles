using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Chef : RoleBase
{
    public static bool On;
    private static List<Chef> Instances = [];

    private static OptionItem EventTarget;
    private static OptionItem RottenTime;
    private static OptionItem SpitOutTime;
    private static OptionItem IncreasedSpeed;
    private static OptionItem IncreasedSpeedDuration;
    private static OptionItem IncreasedVision;
    private static OptionItem IncreasedVisionDuration;
    private static OptionItem ShieldDuration;

    private static readonly string[] EventTargets =
    [
        "Chef.ET.RandomPlayer",
        "Chef.ET.NearestPlayer"
    ];

    private Dictionary<byte, List<PlayerEvent>> ActiveEvents = [];
    private byte ChefId;
    private Dictionary<byte, long> RottenFood = [];

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 648750;
        Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Chef);

        EventTarget = new StringOptionItem(++id, "Chef.EventTarget", EventTargets, 0, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Chef]);

        RottenTime = new IntegerOptionItem(++id, "Chef.RottenTime", new(1, 60, 1), 15, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Chef])
            .SetValueFormat(OptionFormat.Seconds);

        SpitOutTime = new IntegerOptionItem(++id, "Chef.SpitOutTime", new(1, 60, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Chef])
            .SetValueFormat(OptionFormat.Seconds);

        IncreasedSpeed = new FloatOptionItem(++id, "IncreasedSpeed", new(0f, 3f, 0.05f), 1.75f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Chef])
            .SetValueFormat(OptionFormat.Multiplier);

        IncreasedSpeedDuration = new IntegerOptionItem(++id, "IncreasedSpeedDuration", new(1, 60, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Chef])
            .SetValueFormat(OptionFormat.Seconds);

        IncreasedVision = new FloatOptionItem(++id, "IncreasedVision", new(0f, 3f, 0.05f), 1.5f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Chef])
            .SetValueFormat(OptionFormat.Multiplier);

        IncreasedVisionDuration = new IntegerOptionItem(++id, "IncreasedVisionDuration", new(1, 60, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Chef])
            .SetValueFormat(OptionFormat.Seconds);

        ShieldDuration = new IntegerOptionItem(++id, "ShieldDuration", new(1, 60, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Chef])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        ChefId = playerId;
        Instances.Add(this);
        ActiveEvents = [];
        RottenFood = [];
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (!pc.IsAlive()) return;

        Vector2 pos = pc.Pos();
        PlayerControl[] aapc = Main.AllAlivePlayerControls.Without(pc).ToArray();
        PlayerControl target = EventTarget.GetValue() == 0 ? aapc.RandomElement() : aapc.MinBy(x => Vector2.Distance(x.Pos(), pos));

        if (target.Is(Team.Impostor) || target.IsNeutralKiller() || target.Is(Team.Coven))
        {
            RottenFood[target.PlayerId] = Utils.TimeStamp;
            NotifyAboutRandomFood(target, "ChefRotten");
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 4, target.PlayerId);
        }
        else
        {
            switch (IRandom.Instance.Next(4))
            {
                case 0:
                    switch (IRandom.Instance.Next(2))
                    {
                        case 0:
                            target.TP(aapc.Without(target).RandomElement());
                            NotifyAboutRandomFood(target, "ChefBoost.TP");
                            break;
                        case 1:
                            target.TPToRandomVent();
                            NotifyAboutRandomFood(target, "ChefBoost.TPToVent");
                            break;
                    }

                    break;
                case 1:
                    new IncreasedSpeedEvent(target, this).Update();
                    NotifyAboutRandomFood(target, "ChefBoost.Speed");
                    Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, target.PlayerId);
                    break;
                case 2:
                    new IncreasedVisionEvent(target, this).Update();
                    NotifyAboutRandomFood(target, "ChefBoost.Vision");
                    Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 2, target.PlayerId);
                    break;
                case 3:
                    new ShieldEvent(target, this).Update();
                    NotifyAboutRandomFood(target, "ChefBoost.Shield");
                    Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 3, target.PlayerId);
                    break;
            }
        }

        if (target.AmOwner)
            Achievements.Type.Delicious.Complete();
    }

    private static void NotifyAboutRandomFood(PlayerControl pc, string cause)
    {
        string food = Translator.GetString($"ChefFood.{IRandom.Instance.Next(35)}");
        string str = string.Format(Translator.GetString("ChefNotify"), food, Translator.GetString(cause));
        pc.Notify(str, 10f);
    }

    public static void ApplyGameOptionsForOthers(IGameOptions opt, byte id)
    {
        foreach (Chef chef in Instances)
        {
            if (chef.ActiveEvents.TryGetValue(id, out List<PlayerEvent> events))
            {
                foreach (PlayerEvent playerEvent in events)
                {
                    if (playerEvent is IncreasedVisionEvent)
                    {
                        opt.SetFloat(FloatOptionNames.CrewLightMod, IncreasedVision.GetFloat());
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, IncreasedVision.GetFloat());
                        return;
                    }
                }
            }
        }
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (lowLoad || !pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

        if (ActiveEvents.TryGetValue(pc.PlayerId, out List<PlayerEvent> events)) events.ToArray().Do(x => x.Update());

        if (RottenFood.TryGetValue(pc.PlayerId, out long ts))
        {
            if (Utils.TimeStamp - ts > RottenTime.GetInt())
            {
                RottenFood.Remove(pc.PlayerId);
                Utils.SendRPC(CustomRPC.SyncRoleData, ChefId, 8, pc.PlayerId);

                float oldSpeed = Main.AllPlayerSpeed[pc.PlayerId];
                Main.PlayerStates[pc.PlayerId].IsBlackOut = true;
                Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                ReportDeadBodyPatch.CanReport[pc.PlayerId] = false;
                pc.MarkDirtySettings();

                LateTask.New(() =>
                {
                    Main.PlayerStates[pc.PlayerId].IsBlackOut = false;
                    Main.AllPlayerSpeed[pc.PlayerId] = oldSpeed;
                    ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
                    pc.MarkDirtySettings();
                }, SpitOutTime.GetInt(), "ChefSpitOut");
            }

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    public override void OnReportDeadBody()
    {
        ActiveEvents.Clear();
        RottenFood.Keys.Do(x => Utils.GetPlayerById(x).Suicide());
        RottenFood.Clear();
    }

    public static void SpitOutFood(PlayerControl pc)
    {
        Instances.Do(x => x.RottenFood.Remove(pc.PlayerId));
        NameNotifyManager.Notifies.Remove(pc.PlayerId);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                new IncreasedSpeedEvent(Utils.GetPlayerById(reader.ReadByte()), this).Update();
                break;
            case 2:
                new IncreasedVisionEvent(Utils.GetPlayerById(reader.ReadByte()), this).Update();
                break;
            case 3:
                new ShieldEvent(Utils.GetPlayerById(reader.ReadByte()), this).Update();
                break;
            case 4:
                RottenFood[reader.ReadByte()] = Utils.TimeStamp;
                break;
            case 5:
            case 6:
            case 7:
                ActiveEvents[reader.ReadByte()].RemoveAt(reader.ReadPackedInt32());
                break;
            case 8:
                RottenFood.Remove(reader.ReadByte());
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (meeting) return string.Empty;

        long now = Utils.TimeStamp;
        if (ActiveEvents.TryGetValue(target.PlayerId, out List<PlayerEvent> events) && (seer.PlayerId == target.PlayerId || seer.PlayerId == ChefId)) return $"<size=80%>{string.Join('\n', events.Select(e => string.Format(Translator.GetString("ChefBoostSuffix"), GetEventString(e), e.Duration - (now - e.StartTimeStamp))))}</size>";

        if (RottenFood.TryGetValue(seer.PlayerId, out long ts) && seer.PlayerId == target.PlayerId) return string.Format(Translator.GetString("ChefRottenSuffix"), RottenTime.GetInt() - (now - ts));

        return string.Empty;

        string GetEventString(PlayerEvent e) =>
            e switch
            {
                IncreasedSpeedEvent => Translator.GetString("ChefBoost.Speed"),
                IncreasedVisionEvent => Translator.GetString("ChefBoost.Vision"),
                ShieldEvent => Translator.GetString("ChefBoost.Shield"),
                _ => string.Empty
            };
    }

    private abstract class PlayerEvent
    {
        protected abstract PlayerControl Player { get; }
        protected abstract Chef Instance { get; }
        internal abstract long StartTimeStamp { get; }
        internal abstract int Duration { get; }
        internal abstract void Update();

        protected void AddEventToDictionary()
        {
            if (!Instance.ActiveEvents.ContainsKey(Player.PlayerId))
                Instance.ActiveEvents[Player.PlayerId] = [this];
            else
                Instance.ActiveEvents[Player.PlayerId].Add(this);
        }
    }

    private sealed class IncreasedSpeedEvent : PlayerEvent
    {
        public IncreasedSpeedEvent(PlayerControl player, Chef instance)
        {
            Player = player;
            StartTimeStamp = Utils.TimeStamp;
            Duration = IncreasedSpeedDuration.GetInt();
            Instance = instance;

            AddEventToDictionary();

            OldSpeed = Main.AllPlayerSpeed[player.PlayerId];
            Main.AllPlayerSpeed[player.PlayerId] = IncreasedSpeed.GetFloat();
            player.MarkDirtySettings();
        }

        protected override PlayerControl Player { get; }
        internal override long StartTimeStamp { get; }
        internal override int Duration { get; }
        protected override Chef Instance { get; }
        private float OldSpeed { get; }

        internal override void Update()
        {
            if (Utils.TimeStamp - StartTimeStamp > Duration)
            {
                Main.AllPlayerSpeed[Player.PlayerId] = OldSpeed;
                Player.MarkDirtySettings();
                int index = Instance.ActiveEvents[Player.PlayerId].IndexOf(this);
                Instance.ActiveEvents[Player.PlayerId].Remove(this);
                Utils.SendRPC(CustomRPC.SyncRoleData, Instance.ChefId, 5, Player.PlayerId, index);
            }

            Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
            Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(Instance.ChefId), SpecifyTarget: Player);
        }
    }

    private sealed class IncreasedVisionEvent : PlayerEvent
    {
        public IncreasedVisionEvent(PlayerControl player, Chef instance)
        {
            Player = player;
            StartTimeStamp = Utils.TimeStamp;
            Duration = IncreasedVisionDuration.GetInt();
            Instance = instance;

            AddEventToDictionary();

            player.MarkDirtySettings();
        }

        protected override PlayerControl Player { get; }
        internal override long StartTimeStamp { get; }
        internal override int Duration { get; }
        protected override Chef Instance { get; }

        internal override void Update()
        {
            if (Utils.TimeStamp - StartTimeStamp > Duration)
            {
                Player.MarkDirtySettings();
                int index = Instance.ActiveEvents[Player.PlayerId].IndexOf(this);
                Instance.ActiveEvents[Player.PlayerId].Remove(this);
                Utils.SendRPC(CustomRPC.SyncRoleData, Instance.ChefId, 6, Player.PlayerId, index);
            }

            Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
            Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(Instance.ChefId), SpecifyTarget: Player);
        }
    }

    private sealed class ShieldEvent : PlayerEvent
    {
        public ShieldEvent(PlayerControl player, Chef instance)
        {
            Player = player;
            StartTimeStamp = Utils.TimeStamp;
            Duration = ShieldDuration.GetInt();
            Instance = instance;

            AddEventToDictionary();
        }

        protected override PlayerControl Player { get; }
        internal override long StartTimeStamp { get; }
        internal override int Duration { get; }
        protected override Chef Instance { get; }

        internal override void Update()
        {
            if (Utils.TimeStamp - StartTimeStamp > Duration)
            {
                int index = Instance.ActiveEvents[Player.PlayerId].IndexOf(this);
                Instance.ActiveEvents[Player.PlayerId].Remove(this);
                Utils.SendRPC(CustomRPC.SyncRoleData, Instance.ChefId, 7, Player.PlayerId, index);
            }

            Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
            Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(Instance.ChefId), SpecifyTarget: Player);
        }
    }
}