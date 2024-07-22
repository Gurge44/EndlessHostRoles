using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral
{
    public class Simon : RoleBase
    {
        public enum Instruction
        {
            None,
            Kill,
            Task
        }

        private const int Id = 12845;
        public static bool On;
        public static List<Simon> Instances = [];

        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;

        private bool DoMode;
        private bool Executed;
        private Dictionary<byte, (bool DoAction, Instruction Instruction)> MarkedPlayers;
        private byte SimonId;

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Simon);
            KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Simon])
                .SetValueFormat(OptionFormat.Seconds);
            HasImpostorVision = new BooleanOptionItem(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Simon]);
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
            MarkedPlayers = [];
            DoMode = true;
            Executed = false;
            SimonId = playerId;
            Utils.SendRPC(CustomRPC.SyncRoleData, playerId, 1, DoMode);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override bool CanUseImpostorVentButton(PlayerControl pc) => pc.IsAlive();
        public override bool CanUseSabotage(PlayerControl pc) => !(UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool());

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(HasImpostorVision.GetBool());
            if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
                AURoleOptions.PhantomCooldown = 1f;
            if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
                AURoleOptions.ShapeshifterCooldown = 1f;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            return killer.CheckDoubleTrigger(target, () =>
            {
                MarkedPlayers[target.PlayerId] = (DoMode, Main.PlayerStates[target.PlayerId].Role.CanUseKillButton(target) ? Instruction.Kill : target.GetTaskState().hasTasks ? Instruction.Task : Instruction.None);
                Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 3, target.PlayerId, DoMode, (int)MarkedPlayers[target.PlayerId].Instruction);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            });
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            if (Executed || MarkedPlayers.Count == 0) return;

            int size = MarkedPlayers.Count;
            MarkedPlayers.Where(x => x.Value.Instruction == Instruction.None).ToList().ForEach(x => MarkedPlayers.Remove(x.Key));
            if (size != MarkedPlayers.Count) Utils.SendRPC(CustomRPC.SyncRoleData, physics.myPlayer.PlayerId, 2);

            Executed = true;
            foreach (var kvp in MarkedPlayers)
            {
                var pc = Utils.GetPlayerById(kvp.Key);
                if (pc == null || !pc.IsAlive()) continue;

                pc.Notify(Translator.GetString(GetNotify(kvp.Value.Instruction, kvp.Value.DoAction, false)), 300f);
            }

            Utils.NotifyRoles(SpecifySeer: physics.myPlayer, SpecifyTarget: physics.myPlayer);
        }

        static string GetNotify(Instruction instruction, bool doAction, bool forSimon)
        {
            if (!forSimon)
            {
                if (instruction == Instruction.Kill) return doAction ? "SimonKill" : "SimonDontKill";
                return doAction ? "SimonTask" : "SimonDontTask";
            }

            return doAction ? "SimonDoShort" : "SimonDontShort";
        }

        public override void OnPet(PlayerControl pc)
        {
            DoMode = !DoMode;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, DoMode);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            OnPet(pc);
            return false;
        }

        public override bool OnVanish(PlayerControl pc)
        {
            OnPet(pc);
            return false;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;
            OnPet(shapeshifter);
            return false;
        }

        public override void OnReportDeadBody()
        {
            if (Executed)
            {
                Executed = false;
                foreach (var kvp in MarkedPlayers)
                {
                    if (!kvp.Value.DoAction || kvp.Value.Instruction == Instruction.None) continue;
                    var pc = Utils.GetPlayerById(kvp.Key);
                    if (pc == null || !pc.IsAlive()) continue;
                    pc.Suicide();
                }
            }

            MarkedPlayers.Clear();
            Utils.SendRPC(CustomRPC.SyncRoleData, SimonId, 5);
        }

        public void ReceiveRPC(MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    DoMode = reader.ReadBoolean();
                    break;
                case 2:
                    MarkedPlayers.Where(x => x.Value.Instruction == Instruction.None).ToList().ForEach(x => MarkedPlayers.Remove(x.Key));
                    break;
                case 3:
                    MarkedPlayers[reader.ReadByte()] = (reader.ReadBoolean(), (Instruction)reader.ReadPackedInt32());
                    break;
                case 4:
                    MarkedPlayers.Remove(reader.ReadByte());
                    break;
                case 5:
                    MarkedPlayers.Clear();
                    break;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
        {
            if (Main.PlayerStates[seer.PlayerId].Role is not Simon simon) return string.Empty;
            bool self = seer.PlayerId == target.PlayerId;
            if (seer.IsModClient() && !hud && self) return string.Empty;

            if (self) return Translator.GetString(simon.DoMode ? "SimonDoMode" : "SimonDontMode");
            else if (simon.MarkedPlayers.TryGetValue(target.PlayerId, out var value)) return Translator.GetString(GetNotify(value.Instruction, value.DoAction, true));

            return string.Empty;
        }

        public static void RemoveTarget(PlayerControl pc, Instruction instruction)
        {
            foreach (var simon in Instances)
            {
                if (!simon.Executed) continue;
                if (simon.MarkedPlayers.TryGetValue(pc.PlayerId, out var value))
                {
                    if (value.Instruction != instruction) continue;
                    if (value.DoAction) pc.Notify(Utils.ColorString(Color.green, "\u2713"));
                    else pc.Suicide();
                    simon.MarkedPlayers.Remove(pc.PlayerId);
                    Utils.SendRPC(CustomRPC.SyncRoleData, simon.SimonId, 4, pc.PlayerId);
                }
            }
        }
    }
}