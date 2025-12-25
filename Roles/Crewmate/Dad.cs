using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;

namespace EHR.Crewmate;

public class Dad : RoleBase
{
    public enum Ability
    {
        GoForMilk,
        SuperVision,
        Sniffing,
        Sleep,
        Rage,
        GiveDrink,
        BecomeGodOfAlcohol
    }

    public static bool On;
    private static List<Dad> Instances = [];
    private static List<Vector2> AllDeadBodies = [];

    private static OptionItem AlcoholDecreaseOnKilled;
    private static OptionItem AlcoholDecreaseOnVotedOut;
    private static OptionItem NormalAlcoholDecreaseFrequency;
    private static OptionItem NormalAlcoholDecreaseValue;
    private static OptionItem AlcoholIncreaseOnBeerPurchase;
    private static OptionItem ShowWarningWhenAlcoholIsBelow;
    private static OptionItem StartingAlcohol;
    private static OptionItem StartingMoney;
    private static OptionItem MoneyGainOnTaskComplete;
    private static OptionItem AlcoholCost;
    private static OptionItem SuperVisionDuration;
    private static OptionItem GivingDrinkRange;
    private static OptionItem DrunkRoleIncorrectChance;
    private static readonly Dictionary<Ability, OptionItem> AbilityAlcoholDecreaseOptions = [];
    private static readonly Dictionary<Ability, OptionItem> AbilityAlcoholRequirement = [];

    private int Alcohol;
    private string Arrows;
    private int Count;
    private byte DadId;
    public bool DoneTasks;
    public List<byte> DrunkPlayers;
    private long LastUpdate;
    private long MilkTimer;
    private Ability SelectedAbility;
    private Vent Shop;
    private float StartingSpeed;
    private long SuperVisionTS;
    public HashSet<Ability> UsingAbilities;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 648450;
        const TabGroup tab = TabGroup.CrewmateRoles;
        const CustomRoles role = CustomRoles.Dad;

        Options.SetupRoleOptions(id++, tab, role);

        StringOptionItem parent = Options.CustomRoleSpawnChances[role];

        AlcoholDecreaseOnKilled = new IntegerOptionItem(++id, "Dad.AlcoholDecreaseOnKilled", new(0, 100, 1), 10, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Percent);

        AlcoholDecreaseOnVotedOut = new IntegerOptionItem(++id, "Dad.AlcoholDecreaseOnVotedOut", new(0, 100, 1), 5, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Percent);

        NormalAlcoholDecreaseFrequency = new IntegerOptionItem(++id, "Dad.NormalAlcoholDecreaseFrequency", new(1, 60, 1), 10, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);

        NormalAlcoholDecreaseValue = new IntegerOptionItem(++id, "Dad.NormalAlcoholDecreaseValue", new(0, 100, 1), 1, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Percent);

        AlcoholIncreaseOnBeerPurchase = new IntegerOptionItem(++id, "Dad.AlcoholIncreaseOnBeerPurchase", new(0, 100, 1), 10, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Percent);

        ShowWarningWhenAlcoholIsBelow = new IntegerOptionItem(++id, "Dad.ShowWarningWhenAlcoholIsBelow", new(0, 100, 1), 20, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Percent);

        StartingAlcohol = new IntegerOptionItem(++id, "Dad.StartingAlcohol", new(0, 100, 1), 25, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Percent);

        StartingMoney = new IntegerOptionItem(++id, "Dad.StartingMoney", new(0, 100, 1), 0, tab)
            .SetParent(parent);

        MoneyGainOnTaskComplete = new IntegerOptionItem(++id, "Dad.MoneyGainOnTaskComplete", new(0, 100, 1), 15, tab)
            .SetParent(parent);

        AlcoholCost = new IntegerOptionItem(++id, "Dad.AlcoholCost", new(0, 100, 1), 10, tab)
            .SetParent(parent);

        SuperVisionDuration = new IntegerOptionItem(++id, "Dad.SuperVisionDuration", new(1, 90, 1), 40, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Seconds);

        GivingDrinkRange = new FloatOptionItem(++id, "Dad.GivingDrinkRange", new(0.5f, 10f, 0.5f), 6f, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Multiplier);

        DrunkRoleIncorrectChance = new IntegerOptionItem(++id, "Dad.DrunkRoleIncorrectChance", new(0, 100, 1), 10, tab)
            .SetParent(parent)
            .SetValueFormat(OptionFormat.Percent);

        Ability[] abilities = Enum.GetValues<Ability>();

        foreach (Ability ability in abilities)
        {
            AbilityAlcoholDecreaseOptions[ability] = new IntegerOptionItem(++id, $"Dad.{ability}.AlcoholDecrease", new(0, 100, 1), 10, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Percent);
        }

        foreach (Ability ability in abilities)
        {
            AbilityAlcoholRequirement[ability] = new IntegerOptionItem(++id, $"Dad.{ability}.AlcoholRequirement", new(0, 100, 1), ability == Ability.BecomeGodOfAlcohol ? 40 : (int)ability * 5, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Percent);
        }
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
        Main.AllPlayerSpeed[playerId] *= -1;
        DadId = playerId;
        Alcohol = StartingAlcohol.GetInt();
        LastUpdate = Utils.TimeStamp;
        Count = 0;
        Shop = ShipStatus.Instance.AllVents.RandomElement();
        DoneTasks = false;
        SelectedAbility = default(Ability);
        MilkTimer = 315569520;
        UsingAbilities = [];
        SuperVisionTS = 0;
        Arrows = string.Empty;
        StartingSpeed = Main.AllPlayerSpeed[playerId];
        DrunkPlayers = [];
        playerId.SetAbilityUseLimit(StartingMoney.GetFloat());
        Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 1, Shop.Id);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
        if (!AmongUsClient.Instance.AmHost) return;
        Main.AllPlayerSpeed[playerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        PlayerGameOptionsSender.SetDirty(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsingAbilities.Contains(Ability.Sleep))
        {
            opt.SetVision(false);
            opt.BlackOut(true);
            return;
        }

        if (UsingAbilities.Contains(Ability.SuperVision))
        {
            opt.SetVision(true);
            opt.SetFloat(FloatOptionNames.CrewLightMod, 1.5f);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 1.5f);
        }

        AURoleOptions.EngineerCooldown = 0.1f;
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void OnReportDeadBody()
    {
        AllDeadBodies = [];
        Arrows = string.Empty;
        Main.AllPlayerSpeed[DadId] = StartingSpeed;

        if (DrunkPlayers.Count > 0)
        {
            int chance = DrunkRoleIncorrectChance.GetInt();
            List<CustomRoles> allRoles = Enum.GetValues<CustomRoles>().Where(x => x.IsEnable() && !x.IsAdditionRole() && !CustomHnS.AllHnSRoles.Contains(x) && !x.IsForOtherGameMode()).ToList();

            foreach (byte id in DrunkPlayers)
            {
                PlayerControl pc = Utils.GetPlayerById(id);
                if (pc == null) continue;

                CustomRoles selfRole = Main.PlayerStates[id].MainRole;
                CustomRoles rndRole = allRoles.RandomElement();
                CustomRoles role = IRandom.Instance.Next(100) < chance ? rndRole : selfRole;
                string msg = string.Format(Translator.GetString("Dad.DrunkRoleNotify"), Translator.GetString(role.ToString()).ToLower());
                int delay = 10 + IRandom.Instance.Next(12);

                LateTask.New(() => pc.RpcSendChat(msg), delay, $"Dad - DrunkRoleNotify - {pc.GetNameWithRole()}");
            }

            DrunkPlayers = [];
        }

        Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 2, Arrows);
    }

    public override void AfterMeetingTasks()
    {
        if (UsingAbilities.Contains(Ability.GoForMilk)) LateTask.New(() => Utils.GetPlayerById(DadId)?.TP(Pelican.GetBlackRoomPS()), 1f, log: false);
    }

    public override void OnPet(PlayerControl pc)
    {
        SelectedAbility = (Ability)(((int)SelectedAbility + 1) % Enum.GetValues<Ability>().Length);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (vent.Id == Shop.Id || UsingAbilities.Contains(Ability.Sleep)) return;

        if (AbilityAlcoholRequirement[SelectedAbility].GetInt() > Alcohol)
        {
            pc.Notify(Translator.GetString("Dad.AbilityNotAvailable"));
            return;
        }

        switch (SelectedAbility)
        {
            case Ability.GoForMilk:
                LateTask.New(() => pc.TP(Pelican.GetBlackRoomPS()), 2f, log: false);
                Main.AllAlivePlayerControls.NotifyPlayers(Translator.GetString("Dad.GoForMilkNotify"), 10f);
                UsingAbilities.Add(SelectedAbility);
                break;
            case Ability.SuperVision:
                SuperVisionTS = Utils.TimeStamp;
                UsingAbilities.Add(SelectedAbility);
                pc.MarkDirtySettings();
                break;
            case Ability.Sniffing:
                AllDeadBodies.Do(x => LocateArrow.Add(DadId, x));
                Arrows = LocateArrow.GetArrows(pc);
                LocateArrow.RemoveAllTarget(DadId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 2, Arrows);
                break;
            case Ability.Sleep:
                Main.AllPlayerSpeed[DadId] = Main.MinSpeed;
                UsingAbilities.Add(SelectedAbility);
                pc.MarkDirtySettings();
                break;
            case Ability.Rage:
                UsingAbilities.Add(SelectedAbility);
                break;
            case Ability.GiveDrink:
                Vector2 pos = pc.Pos();
                DrunkPlayers = Main.AllAlivePlayerControls.Without(pc).Where(x => Vector2.Distance(x.Pos(), pos) <= GivingDrinkRange.GetFloat()).Select(x => x.PlayerId).ToList();
                Utils.NotifyRoles(SpecifySeer: pc);
                break;
            case Ability.BecomeGodOfAlcohol:
                Alcohol = 1;
                UsingAbilities.Add(SelectedAbility);
                break;
        }

        Alcohol -= AbilityAlcoholDecreaseOptions[SelectedAbility].GetInt();
        NotifyIfNecessary(pc);
    }

    public override void OnExitVent(PlayerControl pc, Vent vent)
    {
        if (vent.Id != Shop.Id) return;

        int cost = AlcoholCost.GetInt();
        int get = AlcoholIncreaseOnBeerPurchase.GetInt();

        for (float money = pc.GetAbilityUseLimit(); money >= cost; money -= cost)
        {
            pc.SetAbilityUseLimit(money - cost);
            Alcohol += get;
        }

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 3, Alcohol);
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        pc.RpcIncreaseAbilityUseLimitBy(MoneyGainOnTaskComplete.GetInt());

        if (completedTaskCount + 1 >= totalTaskCount)
        {
            pc.RpcResetTasks();
            pc.SyncSettings();
            DoneTasks = true;
            GameData.Instance.RecomputeTaskCounts();
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

        if (Count++ < 15) return;

        Count = 0;

        Vector2 pos = pc.Pos();
        if (UsingAbilities.Contains(Ability.Rage) && Main.AllAlivePlayerControls.FindFirst(x => Vector2.Distance(pos, x.Pos()) < 1.3f, out PlayerControl target) && pc.RpcCheckAndMurder(target)) UsingAbilities.Remove(Ability.Rage);

        bool notify = Vector2.Distance(pc.Pos(), Shop.transform.position) < 2f;

        long now = Utils.TimeStamp;
        long elapsed = now - LastUpdate;

        if (elapsed < NormalAlcoholDecreaseFrequency.GetInt())
        {
            if (notify) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            return;
        }

        if (UsingAbilities.Contains(Ability.GoForMilk))
        {
            MilkTimer -= elapsed;

            if (MilkTimer <= 0)
            {
                pc.TPToRandomVent();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
                CustomWinnerHolder.WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.Is(Team.Crewmate)).Select(x => x.PlayerId));
            }
        }

        LastUpdate = now;

        if (UsingAbilities.Contains(Ability.SuperVision) && SuperVisionTS + SuperVisionDuration.GetInt() <= now)
        {
            UsingAbilities.Remove(Ability.SuperVision);
            SuperVisionTS = 0;
            pc.MarkDirtySettings();
        }

        if (UsingAbilities.Contains(Ability.Sleep) || UsingAbilities.Contains(Ability.GoForMilk)) return;

        if (UsingAbilities.Contains(Ability.BecomeGodOfAlcohol))
            Alcohol += NormalAlcoholDecreaseValue.GetInt();
        else
        {
            Alcohol -= NormalAlcoholDecreaseValue.GetInt();

            if (Alcohol <= 0)
            {
                pc.Suicide();
                return;
            }
        }

        NotifyIfNecessary(pc, notify);
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        LateTask.New(() =>
        {
            Alcohol -= AlcoholDecreaseOnKilled.GetInt();
            NotifyIfNecessary(target);
        }, 5f, log: false);

        return false;
    }

    public static bool OnVotedOut(byte id)
    {
        Dad dad = Instances.FirstOrDefault(d => d.DadId == id);
        if (dad == null) return false;

        dad.Alcohol -= AlcoholDecreaseOnVotedOut.GetInt();
        dad.NotifyIfNecessary(Utils.GetPlayerById(dad.DadId));
        Logger.Info("Ejection prohibited", "Dad");
        return true;
    }

    public static void OnAnyoneDeath(PlayerControl target)
    {
        AllDeadBodies.Add(target.Pos());
    }

    public static bool OnAnyoneCheckMurderStart(PlayerControl target)
    {
        Dad dad = Instances.FirstOrDefault(d => d.DadId == target.PlayerId);
        return dad != null && dad.UsingAbilities.Contains(Ability.Sleep);
    }

    private void NotifyIfNecessary(PlayerControl pc, bool force = false)
    {
        if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

        if (force || Alcohol <= ShowWarningWhenAlcoholIsBelow.GetInt())
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

        Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 3, Alcohol);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                int id = reader.ReadPackedInt32();
                Shop = ShipStatus.Instance.AllVents.FirstOrDefault(x => x.Id == id);
                break;
            case 2:
                Arrows = reader.ReadString();
                break;
            case 3:
                Alcohol = reader.ReadPackedInt32();
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != DadId || meeting || (seer.IsModdedClient() && !hud)) return string.Empty;

        var sb = new StringBuilder();

        if (Vector2.Distance(seer.Pos(), Shop.transform.position) <= 2f)
        {
            float canBuyAmount = seer.GetAbilityUseLimit() / AlcoholCost.GetInt();
            sb.Append(string.Format(Translator.GetString("Dad.ShopSuffix"), canBuyAmount));
        }

        if (Alcohol <= ShowWarningWhenAlcoholIsBelow.GetInt())
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(string.Format(Translator.GetString("Dad.LowAlcoholSuffix"), Alcohol));
        }

        if (Arrows.Length > 0)
        {
            if (sb.Length > 0) sb.Append('\n');

            sb.Append(Arrows);
        }

        if (sb.Length > 0) sb.Append("\n\n<size=70%>");

        sb.Append(string.Format(Translator.GetString("Dad.ShopLocation"), Shop.name));
        sb.Append('\n');
        sb.Append(string.Format(Translator.GetString("Dad.SelectedAbilitySuffix"), Translator.GetString($"Dad.Ability.{SelectedAbility}")));
        sb.Append('\n');
        sb.Append(Translator.GetString($"Dad.{SelectedAbility}.Description"));
        sb.Append("</size>");

        return sb.ToString().Trim();
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        bool wentForMilk = UsingAbilities.Contains(Ability.GoForMilk);
        
        keepGameGoing = !wentForMilk;
        countsAs = wentForMilk ? 0 : 1;
    }
}