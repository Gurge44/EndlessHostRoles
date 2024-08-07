using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR.Crewmate
{
    public class Catcher : RoleBase
    {
        public static bool On;

        public static OptionItem AbilityCooldown;
        public static OptionItem TrapPlaceDelay;
        public static OptionItem CatchRange;
        public static OptionItem MinPlayersTrappedToShowInfo;
        public static OptionItem AbilityUseLimit;
        public static OptionItem AbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        private byte CatcherId;
        private Dictionary<byte, CustomRoles> CaughtRoles;
        private long DelayStartTS;
        private long LastUpdate;
        private Dictionary<Vector2, CatcherTrap> Traps;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            int id = 648650;
            const TabGroup tab = TabGroup.CrewmateRoles;
            const CustomRoles role = CustomRoles.Catcher;

            Options.SetupRoleOptions(id++, tab, role);

            var parent = Options.CustomRoleSpawnChances[role];

            AbilityCooldown = new FloatOptionItem(++id, "AbilityCooldown", new(0f, 180f, 0.5f), 10f, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Seconds);
            TrapPlaceDelay = new FloatOptionItem(++id, "Catcher.TrapPlaceDelay", new(0f, 180f, 1f), 5f, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Seconds);
            CatchRange = new FloatOptionItem(++id, "Catcher.CatchRange", new(0f, 10f, 0.25f), 1.5f, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Multiplier);
            MinPlayersTrappedToShowInfo = new IntegerOptionItem(++id, "Catcher.MinPlayersTrappedToShowInfo", new(1, 10, 1), 2, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Players);
            AbilityUseLimit = new IntegerOptionItem(++id, "AbilityUseLimit", new(0, 20, 1), 3, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Times);
            AbilityUseGainWithEachTaskCompleted = new FloatOptionItem(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = new FloatOptionItem(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, tab)
                .SetParent(parent)
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            CatcherId = playerId;
            Traps = [];
            DelayStartTS = 0;
            LastUpdate = Utils.TimeStamp;
            CaughtRoles = [];
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        void PlaceTrap(PlayerControl pc)
        {
            if (pc.GetAbilityUseLimit() < 1) return;
            pc.RpcRemoveAbilityUse();

            var pos = pc.Pos();
            Traps[pos] = new(pos, pc);

            pc.Notify(Translator.GetString("Catcher.TrapPlaced"));
        }

        public override void OnPet(PlayerControl pc)
        {
            PlaceTrap(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            DelayStartTS = Utils.TimeStamp + 1;
            SendRPC();
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance || Options.UsePets.GetBool() || DelayStartTS == 0) return;

            long now = Utils.TimeStamp;
            if (now == LastUpdate) return;
            LastUpdate = now;

            if (DelayStartTS + TrapPlaceDelay.GetInt() <= now)
            {
                PlaceTrap(pc);
                DelayStartTS = 0;
                SendRPC();
            }

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            pc.RpcResetAbilityCooldown();
        }

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (CaughtRoles.ContainsKey(pc.PlayerId) || pc.PlayerId == CatcherId) return;

            var pos = pc.Pos();
            var range = CatchRange.GetFloat();
            if (Traps.Keys.Any(x => Vector2.Distance(x, pos) <= range))
                CaughtRoles[pc.PlayerId] = pc.GetCustomRole();
        }

        public override void OnReportDeadBody()
        {
            if (Traps.Count == 0) return;

            var catcher = CatcherId.GetPlayer();
            if (catcher == null || !catcher.IsAlive()) return;

            LateTask.New(() =>
            {
                if (CaughtRoles.Count >= MinPlayersTrappedToShowInfo.GetInt())
                {
                    var roles = string.Join(", ", CaughtRoles.Values.Select(x => x.ToColoredString()));
                    Utils.SendMessage("\n", CatcherId, Translator.GetString("Catcher.CaughtRoles") + roles);
                }
                else Utils.SendMessage("\n", CatcherId, Translator.GetString("Catcher.NotEnoughCaughtRoles"));

                CaughtRoles = [];
            }, 10f, "Send Catcher Caught Roles");
        }

        void SendRPC() => Utils.SendRPC(CustomRPC.SyncRoleData, CatcherId, DelayStartTS);
        public void ReceiveRPC(Hazel.MessageReader reader) => DelayStartTS = long.Parse(reader.ReadString());

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != CatcherId || isMeeting || (seer.IsModClient() && !isHUD) || DelayStartTS == 0 || Options.UsePets.GetBool()) return string.Empty;
            return string.Format(Translator.GetString("Catcher.Suffix"), TrapPlaceDelay.GetInt() - (Utils.TimeStamp - DelayStartTS));
        }
    }
}