using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Thanos : RoleBase
{
    public static bool On;
    private static List<Thanos> Instances = [];

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;
    private static OptionItem FirstKillAlwaysSoulStone;
    private static OptionItem PowerStoneReducesKillCooldownBy;
    private static OptionItem PowerStoneKillsIgnoreShields;
    private static OptionItem TimeStoneReducesSpeedBy;
    private static OptionItem CanWinAfterCollectingAllStones;

    public override bool IsEnable => On;

    private List<Stone> StonesWaitingForUse;
    private HashSet<Stone> CollectedStones;
    private Stone? ActiveStone;
    private bool MindStoneUsed;
    private List<byte> PlayersWithStones;
    private byte ThanosId;

    public override void SetupCustomOption()
    {
        StartSetup(654200)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true)
            .AutoSetupOption(ref FirstKillAlwaysSoulStone, false)
            .AutoSetupOption(ref PowerStoneReducesKillCooldownBy, 10f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref PowerStoneKillsIgnoreShields, true)
            .AutoSetupOption(ref TimeStoneReducesSpeedBy, 0.5f, new FloatValueRule(0.1f, 3f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref CanWinAfterCollectingAllStones, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        StonesWaitingForUse = [];
        CollectedStones = [];
        ActiveStone = null;
        MindStoneUsed = false;
        PlayersWithStones = Main.AllAlivePlayerControls.Shuffle().Take(Enum.GetValues<Stone>().Length).Select(x => x.PlayerId).ToList();
        ThanosId = playerId;
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        if (ActiveStone is Stone.Power) Main.AllPlayerKillCooldown[id] -= PowerStoneReducesKillCooldownBy.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());
        
        if (Options.UsePhantomBasis.GetBool())
        {
            AURoleOptions.PhantomCooldown = 5f;
            AURoleOptions.PhantomDuration = 1f;
        }
        else
        {
            if (Options.UsePets.GetBool()) return;

            AURoleOptions.ShapeshifterCooldown = 5f;
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (target.Is(CustomRoles.Pestilence)) return ActiveStone is Stone.Power or Stone.Space;

        if (ActiveStone is Stone.Space)
        {
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
            ActiveStone = null;
            Utils.SendRPC(CustomRPC.SyncRoleData, ThanosId, 2);
            target.Data.IsDead = true;
            target.SetRealKiller(killer);
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Scavenged;
            target.RpcExileV2();
            Main.PlayerStates[target.PlayerId].SetDead();
            Utils.AfterPlayerDeathTasks(target);
            target.SetRealKiller(killer);
            killer.SetKillCooldown(KillCooldown.GetFloat());
            OnMurder(killer, target);
            return false;
        }

        return true;
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return ActiveStone is not Stone.Reality;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (PlayersWithStones.Contains(target.PlayerId))
        {
            Stone[] stones = Enum.GetValues<Stone>();
            Stone stone = CollectedStones.Count == 0 && FirstKillAlwaysSoulStone.GetBool() ? Stone.Soul : stones.Except(CollectedStones).RandomElement();
            CollectedStones.Add(stone);
            StonesWaitingForUse.Add(stone);
            Utils.SendRPC(CustomRPC.SyncRoleData, ThanosId, 1, (int)stone);
            
            if (killer.AmOwner && CollectedStones.Count == stones.Length)
                Achievements.Type.MasterOfTheStones.Complete();
        }
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        UseStone(shapeshifter);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        UseStone(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        UseStone(pc);
    }

    void UseStone(PlayerControl pc)
    {
        if (CollectedStones.Count == Enum.GetValues<Stone>().Length && CanWinAfterCollectingAllStones.GetBool())
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Thanos);
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            return;
        }
        
        if (StonesWaitingForUse.Count == 0) return;
        
        if (ActiveStone.HasValue)
        {
            switch (ActiveStone)
            {
                case Stone.Time:
                    Main.AllAlivePlayerControls.DoIf(x => x.PlayerId != pc.PlayerId, x =>
                    {
                        Main.AllPlayerSpeed[x.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        if (GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks) x.MarkDirtySettings();
                    });
                    break;
                case Stone.Soul:
                    TargetArrow.RemoveAllTarget(pc.PlayerId);
                    break;
            }
        }
        
        Stone stone = StonesWaitingForUse[0];
        StonesWaitingForUse.RemoveAt(0);
        ActiveStone = stone;

        switch (stone)
        {
            case Stone.Space:
                break;
            case Stone.Mind:
                MindStoneUsed = true;
                ActiveStone = null;
                break;
            case Stone.Reality:
                break;
            case Stone.Power:
                pc.ResetKillCooldown();
                break;
            case Stone.Time:
                Main.AllAlivePlayerControls.DoIf(x => x.PlayerId != pc.PlayerId, x =>
                {
                    Main.AllPlayerSpeed[x.PlayerId] -= TimeStoneReducesSpeedBy.GetFloat();
                    x.MarkDirtySettings();
                });
                break;
            case Stone.Soul:
                TargetArrow.Add(pc.PlayerId, PlayersWithStones.RandomElement());
                break;
        }
        
        Utils.SendRPC(CustomRPC.SyncRoleData, ThanosId, 3);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public static void OnDeath(PlayerControl killer, PlayerControl target, bool noKiller)
    {
        if (killer == null || killer.PlayerId == target.PlayerId) noKiller = true;

        foreach (Thanos instance in Instances)
        {
            if (!instance.PlayersWithStones.Remove(target.PlayerId)) continue;
            instance.PlayersWithStones.Add(noKiller ? Main.AllAlivePlayerControls.RandomElement().PlayerId : killer.PlayerId);
        }
    }

    public static bool IsImmune(PlayerControl pc) => On && pc != null && Instances.Exists(x => x.ThanosId == pc.PlayerId && x.MindStoneUsed);

    enum Stone
    {
        Space,
        Mind,
        Reality,
        Power,
        Time,
        Soul
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
            {
                Stone stone = (Stone)reader.ReadPackedInt32();
                CollectedStones.Add(stone);
                StonesWaitingForUse.Add(stone);
                break;
            }
            case 2:
            {
                ActiveStone = null;
                break;
            }
            case 3:
            {
                Stone stone = StonesWaitingForUse[0];
                StonesWaitingForUse.RemoveAt(0);
                ActiveStone = stone;

                if (stone == Stone.Mind)
                {
                    MindStoneUsed = true;
                    ActiveStone = null;
                }

                break;
            }
        }
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return base.GetProgressText(playerId, comms) + $" {CollectedStones.Count}/{Enum.GetValues<Stone>().Length}";
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != ThanosId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;

        StringBuilder sb = new();

        if (ActiveStone.HasValue) sb.AppendLine(string.Format(Translator.GetString("Thanos.ActiveStone"), Translator.GetString($"Thanos.Stone.{ActiveStone.Value}")));
        if (MindStoneUsed) sb.AppendLine(Translator.GetString("Thanos.MindStoneUsed"));
        
        if (StonesWaitingForUse.Count > 0)
        {
            string action = Translator.GetString(Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool() ? "AbilityButtonText.Phantom" : Options.UsePets.GetBool() ? "PetButtonText" : "Shapeshift");
            sb.AppendLine(string.Format(Translator.GetString("Thanos.StoneWaitingForUse"), action, Translator.GetString($"Thanos.Stone.{StonesWaitingForUse[0]}")));
        }

        sb.AppendLine(TargetArrow.GetAllArrows(seer.PlayerId));

        return sb.ToString().Trim();
    }
}