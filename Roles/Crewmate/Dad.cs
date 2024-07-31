using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using UnityEngine;

namespace EHR.Crewmate
{
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

        public static void SetupCustomOption()
        {
            int id = 648450;
            const TabGroup tab = TabGroup.CrewmateRoles;
            const CustomRoles role = CustomRoles.Dad;

            Options.SetupRoleOptions(id++, tab, role);

            var parent = Options.CustomRoleSpawnChances[role];

            AlcoholDecreaseOnKilled = new IntegerOptionItem(++id, "Dad.AlcoholDecreaseOnKilled", new(0, 100, 1), 10, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Percent);
            AlcoholDecreaseOnVotedOut = new IntegerOptionItem(++id, "Dad.AlcoholDecreaseOnVotedOut", new(0, 100, 1), 5, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Percent);
            NormalAlcoholDecreaseFrequency = new IntegerOptionItem(++id, "Dad.NormalAlcoholDecreaseFrequency", new(1, 60, 1), 5, tab)
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
            StartingAlcohol = new IntegerOptionItem(++id, "Dad.StartingAlcohol", new(0, 100, 1), 10, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Percent);
            StartingMoney = new IntegerOptionItem(++id, "Dad.StartingMoney", new(0, 100, 1), 0, tab)
                .SetParent(parent);
            MoneyGainOnTaskComplete = new IntegerOptionItem(++id, "Dad.MoneyGainOnTaskComplete", new(0, 100, 1), 15, tab)
                .SetParent(parent);
            AlcoholCost = new IntegerOptionItem(++id, "Dad.AlcoholCost", new(0, 100, 1), 10, tab)
                .SetParent(parent);
            SuperVisionDuration = new IntegerOptionItem(++id, "Dad.SuperVisionDuration", new(1, 90, 1), 20, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Seconds);
            GivingDrinkRange = new FloatOptionItem(++id, "Dad.GivingDrinkRange", new(0.5f, 10f, 0.5f), 4f, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Multiplier);
            DrunkRoleIncorrectChance = new IntegerOptionItem(++id, "Dad.DrunkRoleIncorrectChance", new(0, 100, 1), 50, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Percent);

            Ability[] abilities = Enum.GetValues<Ability>();

            foreach (var ability in abilities)
            {
                AbilityAlcoholDecreaseOptions[ability] = new IntegerOptionItem(++id, $"Dad.{ability}.AlcoholDecrease", new(0, 100, 1), 10, tab)
                    .SetParent(parent)
                    .SetValueFormat(OptionFormat.Percent);
            }

            foreach (var ability in abilities)
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
            SelectedAbility = default;
            MilkTimer = 315569520;
            UsingAbilities = [];
            SuperVisionTS = 0;
            Arrows = string.Empty;
            StartingSpeed = Main.AllPlayerSpeed[playerId];
            DrunkPlayers = [];
            playerId.SetAbilityUseLimit(StartingMoney.GetInt());
            Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 1, Shop.Id);
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
        }

        public override void OnReportDeadBody()
        {
            AllDeadBodies = [];
            Arrows = string.Empty;
            Main.AllPlayerSpeed[DadId] = StartingSpeed;

            if (DrunkPlayers.Count > 0)
            {
                var chance = DrunkRoleIncorrectChance.GetInt();
                var allRoles = Enum.GetValues<CustomRoles>().Where(x => x.IsEnable() && !x.IsAdditionRole() && !HnSManager.AllHnSRoles.Contains(x) && !x.IsForOtherGameMode()).ToList();
                foreach (var id in DrunkPlayers)
                {
                    var pc = Utils.GetPlayerById(id);
                    if (pc == null) continue;

                    var selfRole = Main.PlayerStates[id].MainRole;
                    var rndRole = allRoles.Without(selfRole).RandomElement();
                    var role = IRandom.Instance.Next(100) < chance ? rndRole : selfRole;
                    var msg = string.Format(Translator.GetString("Dad.DrunkRoleNotify"), Translator.GetString(role.ToString()).ToLower());
                    var delay = 10 + IRandom.Instance.Next(12);

                    LateTask.New(() => pc.RpcSendChat(msg), delay, $"Dad - DrunkRoleNotify - {pc.GetNameWithRole()}");
                }

                DrunkPlayers = [];
            }

            Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 2, Arrows);
        }

        public override void AfterMeetingTasks()
        {
            if (UsingAbilities.Contains(Ability.GoForMilk))
                LateTask.New(() => Utils.GetPlayerById(DadId)?.TP(Pelican.GetBlackRoomPS()), 1f, log: false);
        }

        public override void OnPet(PlayerControl pc)
        {
            SelectedAbility = (Ability)(((int)SelectedAbility + 1) % Enum.GetValues<Ability>().Length);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            if (ventId == Shop.Id || UsingAbilities.Contains(Ability.Sleep)) return;

            if (AbilityAlcoholRequirement[SelectedAbility].GetInt() > Alcohol)
            {
                physics.myPlayer.Notify(Translator.GetString("Dad.AbilityNotAvailable"));
                return;
            }

            switch (SelectedAbility)
            {
                case Ability.GoForMilk when Alcohol >= 0:
                    LateTask.New(() => physics.RpcBootFromVent(ventId), 0.5f, log: false);
                    LateTask.New(() => physics.myPlayer.TP(Pelican.GetBlackRoomPS()), 1f, log: false);
                    Main.AllAlivePlayerControls.Do(x => x.Notify(Translator.GetString("Dad.GoForMilkNotify")));
                    UsingAbilities.Add(SelectedAbility);
                    break;
                case Ability.SuperVision when Alcohol >= 5:
                    SuperVisionTS = Utils.TimeStamp;
                    UsingAbilities.Add(SelectedAbility);
                    physics.myPlayer.MarkDirtySettings();
                    break;
                case Ability.Sniffing when Alcohol >= 10:
                    AllDeadBodies.Do(x => LocateArrow.Add(DadId, x));
                    Arrows = LocateArrow.GetArrows(physics.myPlayer);
                    LocateArrow.RemoveAllTarget(DadId);
                    Utils.NotifyRoles(SpecifySeer: physics.myPlayer, SpecifyTarget: physics.myPlayer);
                    Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 2, Arrows);
                    break;
                case Ability.Sleep when Alcohol >= 15:
                    Main.AllPlayerSpeed[DadId] = Main.MinSpeed;
                    UsingAbilities.Add(SelectedAbility);
                    physics.myPlayer.MarkDirtySettings();
                    break;
                case Ability.Rage when Alcohol >= 20:
                    UsingAbilities.Add(SelectedAbility);
                    break;
                case Ability.GiveDrink when Alcohol >= 25:
                    var pos = physics.myPlayer.Pos();
                    DrunkPlayers = Main.AllAlivePlayerControls.Without(physics.myPlayer).Where(x => Vector2.Distance(x.Pos(), pos) <= GivingDrinkRange.GetFloat()).Select(x => x.PlayerId).ToList();
                    Utils.NotifyRoles(SpecifySeer: physics.myPlayer);
                    break;
                case Ability.BecomeGodOfAlcohol when Alcohol >= 40:
                    Alcohol = 1;
                    UsingAbilities.Add(SelectedAbility);
                    break;
            }

            Alcohol -= AbilityAlcoholDecreaseOptions[SelectedAbility].GetInt();
            NotifyIfNecessary(physics.myPlayer);
        }

        public override void OnExitVent(PlayerControl pc, Vent vent)
        {
            if (vent.Id != Shop.Id) return;

            var cost = AlcoholCost.GetInt();
            var get = AlcoholIncreaseOnBeerPurchase.GetInt();
            for (var money = pc.GetAbilityUseLimit(); money >= cost; money -= cost)
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
                pc.Data.RpcSetTasks(new(0));
                DoneTasks = true;
                GameData.Instance.RecomputeTaskCounts();
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

            if (Count++ < 15) return;
            Count = 0;

            var pos = pc.Pos();
            if (UsingAbilities.Contains(Ability.Rage) && Main.AllAlivePlayerControls.Find(x => Vector2.Distance(pos, x.Pos()) < 1.3f, out var target) && pc.RpcCheckAndMurder(target))
                UsingAbilities.Remove(Ability.Rage);

            bool notify = Vector2.Distance(pc.Pos(), Shop.transform.position) < 2f;

            long now = Utils.TimeStamp;
            if (now - LastUpdate < NormalAlcoholDecreaseFrequency.GetInt())
            {
                if (notify) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                return;
            }

            LastUpdate = now;

            if (UsingAbilities.Contains(Ability.GoForMilk))
            {
                MilkTimer--;
                if (MilkTimer <= 0)
                {
                    pc.TPtoRndVent();
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
                    CustomWinnerHolder.WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.Is(Team.Crewmate)).Select(x => x.PlayerId));
                }
            }

            if (UsingAbilities.Contains(Ability.SuperVision) && SuperVisionTS + SuperVisionDuration.GetInt() <= now)
            {
                UsingAbilities.Remove(Ability.SuperVision);
                SuperVisionTS = 0;
                pc.MarkDirtySettings();
            }

            if (UsingAbilities.Contains(Ability.Sleep)) return;

            if (UsingAbilities.Contains(Ability.BecomeGodOfAlcohol)) Alcohol += NormalAlcoholDecreaseValue.GetInt();
            else Alcohol -= NormalAlcoholDecreaseValue.GetInt();

            if (Alcohol <= 0)
            {
                pc.Suicide();
                return;
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
            var dad = Instances.FirstOrDefault(d => d.DadId == id);
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
            var dad = Instances.FirstOrDefault(d => d.DadId == target.PlayerId);
            if (dad == null) return false;
            return dad.UsingAbilities.Contains(Ability.Sleep);
        }

        void NotifyIfNecessary(PlayerControl pc, bool force = false)
        {
            if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;
            if (force || Alcohol <= ShowWarningWhenAlcoholIsBelow.GetInt())
            {
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }

            Utils.SendRPC(CustomRPC.SyncRoleData, DadId, 3, Alcohol);
        }

        public void ReceiveRPC(Hazel.MessageReader reader)
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

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != DadId || isMeeting || (seer.IsModClient() && !isHUD)) return string.Empty;

            var sb = new System.Text.StringBuilder();

            if (Vector2.Distance(seer.Pos(), Shop.transform.position) <= 2f)
            {
                var canBuyAmount = seer.GetAbilityUseLimit() / AlcoholCost.GetInt();
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

            return sb.ToString();
        }
    }
}