using System;
using System.Linq;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.GhostRoles;
using EHR.AddOns.Impostor;
using EHR.Coven;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Neutral;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using Mathf = UnityEngine.Mathf;

// ReSharper disable ForCanBeConvertedToForeach

namespace EHR.Modules;

public sealed class PlayerGameOptionsSender(PlayerControl player) : GameOptionsSender
{
    public PlayerControl player = player;

    private static IGameOptions BasedGameOptions =>
        Main.RealOptionsData.Restore(new NormalGameOptionsV10(new UnityLogger().CastFast<ILogger>()).CastFast<IGameOptions>());

    protected override bool IsDirty { get; set; }

    public static void SetDirty(byte playerId)
    {
        for (var index = 0; index < AllSenders.Count; index++)
        {
            GameOptionsSender allSender = AllSenders[index];

            if (allSender is PlayerGameOptionsSender sender && sender.player.PlayerId == playerId)
            {
                sender.SetDirty();
                break; // Only one sender can have the same player id
            }
        }
    }

    public static void SetDirtyToAll()
    {
        for (var index = 0; index < AllSenders.Count; index++)
        {
            GameOptionsSender allSender = AllSenders[index];

            if (allSender is PlayerGameOptionsSender sender)
                sender.SetDirty();
        }
    }

    // For lights call/fix
    public static void SetDirtyToAllV2()
    {
        for (var index = 0; index < AllSenders.Count; index++)
        {
            GameOptionsSender allSender = AllSenders[index];

            if (allSender is PlayerGameOptionsSender { IsDirty: false } sender && sender.player.IsAlive() && (sender.player.HasDesyncRole() || sender.player.GetCustomRole() is CustomRoles.Transporter or CustomRoles.Lighter or CustomRoles.Doomsayer || sender.player.Is(CustomRoles.Torch) || sender.player.Is(CustomRoles.Mare) || sender.player.Is(CustomRoles.Sleep) || Beacon.IsAffectedPlayer(sender.player.PlayerId)))
                sender.SetDirty();
        }
    }

    // For Grenadier blidning/restoring
    public static void SetDirtyToAllV3()
    {
        for (var index = 0; index < AllSenders.Count; index++)
        {
            GameOptionsSender allSender = AllSenders[index];

            if (allSender is PlayerGameOptionsSender { IsDirty: false } sender && sender.player.IsAlive() && ((Grenadier.GrenadierBlinding.Count > 0 && (sender.player.IsImpostor() || (sender.player.GetCustomRole().IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool()))) || (Grenadier.MadGrenadierBlinding.Count > 0 && !sender.player.GetCustomRole().IsImpostorTeam() && !sender.player.Is(CustomRoles.Madmate))))
                sender.SetDirty();
        }
    }

    // For players with kill buttons
    public static void SetDirtyToAllV4()
    {
        for (var index = 0; index < AllSenders.Count; index++)
        {
            GameOptionsSender allSender = AllSenders[index];

            if (allSender is PlayerGameOptionsSender { IsDirty: false } sender && sender.player.IsAlive() && sender.player.CanUseKillButton())
                sender.SetDirty();
        }
    }

    private void SetDirty()
    {
        IsDirty = true;
    }

    protected override void SendGameOptions()
    {
        if (player.AmOwner)
        {
            IGameOptions opt = BuildGameOptions();

            if (GameManager.Instance?.LogicComponents != null)
            {
                foreach (GameLogicComponent com in GameManager.Instance.LogicComponents)
                {
                    if (com.TryCast(out LogicOptions lo))
                        lo.SetGameOptions(opt);
                }
            }

            GameOptionsManager.Instance.CurrentGameOptions = opt;
        }
        else
            base.SendGameOptions();
    }

    protected override void SendOptionsArray(Il2CppStructArray<byte> optionArray)
    {
        try
        {
            for (byte i = 0; i < GameManager.Instance.LogicComponents.Count; i++)
            {
                Il2CppSystem.Object logicComponent = GameManager.Instance.LogicComponents[(Index)i];
                if (logicComponent.TryCast<LogicOptions>(out _)) SendOptionsArray(optionArray, i, player.OwnerId);
            }
        }
        catch (Exception ex) { Logger.Fatal(ex.ToString(), "PlayerGameOptionsSender.SendOptionsArray"); }
    }

    public static void RemoveSender(PlayerControl player)
    {
        PlayerGameOptionsSender sender = AllSenders.OfType<PlayerGameOptionsSender>()
            .FirstOrDefault(sender => sender.player.PlayerId == player.PlayerId);

        if (sender == null) return;

        sender.player = null;
        AllSenders.Remove(sender);
    }

    public override IGameOptions BuildGameOptions()
    {
        try
        {
            Main.RealOptionsData ??= new(GameOptionsManager.Instance.CurrentGameOptions);

            IGameOptions opt = BasedGameOptions;
            AURoleOptions.SetOpt(opt);
            PlayerState state = Main.PlayerStates[player.PlayerId];
            opt.BlackOut(state.IsBlackOut);

            CustomRoles role = player.GetCustomRole();
            RoleTypes roleTypes = player.GetRoleTypes();

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.FFA:
                {
                    if (FreeForAll.FFALowerVisionList.ContainsKey(player.PlayerId))
                    {
                        opt.SetVision(true);
                        opt.SetFloat(FloatOptionNames.CrewLightMod, FreeForAll.FFALowerVision.GetFloat());
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, FreeForAll.FFALowerVision.GetFloat());
                    }
                    else SetMaxVision();

                    break;
                }
                case CustomGameMode.CaptureTheFlag:
                {
                    CaptureTheFlag.ApplyGameOptions();
                    goto case CustomGameMode.RoomRush;
                }
                case CustomGameMode.Snowdown:
                {
                    Snowdown.ApplyGameOptions();
                    goto case CustomGameMode.RoomRush;
                }
                case CustomGameMode.NaturalDisasters:
                {
                    SetMaxVision();
                    NaturalDisasters.ApplyGameOptions(opt, player.PlayerId);
                    break;
                }
                case CustomGameMode.RoomRush when RoomRush.VentLimit.TryGetValue(player.PlayerId, out int vl) && vl > 0:
                {
                    AURoleOptions.EngineerCooldown = 0.01f;
                    AURoleOptions.EngineerInVentMaxTime = 0f;
                    goto case CustomGameMode.RoomRush;
                }
                case CustomGameMode.Mingle:
                case CustomGameMode.RoomRush:
                case CustomGameMode.Speedrun:
                case CustomGameMode.HotPotato:
                {
                    SetMaxVision();
                    break;
                }
                case CustomGameMode.Deathrace:
                case CustomGameMode.BedWars:
                {
                    AURoleOptions.PhantomCooldown = 0.1f;
                    goto case CustomGameMode.RoomRush;
                }
                case CustomGameMode.Quiz:
                {
                    try
                    {
                        AURoleOptions.GuardianAngelCooldown = 900f;
                        AURoleOptions.ProtectionDurationSeconds = 0.01f;
                    }
                    catch (Exception e) { Utils.ThrowException(e); }

                    goto case CustomGameMode.RoomRush;
                }
                case CustomGameMode.StopAndGo:
                {
                    try
                    {
                        AURoleOptions.EngineerCooldown = 1f;
                        AURoleOptions.EngineerInVentMaxTime = 300f;
                    }
                    catch (Exception e) { Utils.ThrowException(e); }

                    goto case CustomGameMode.RoomRush;
                }
                case CustomGameMode.HideAndSeek:
                {
                    CustomHnS.ApplyGameOptions(opt, player);
                    break;
                }
                case CustomGameMode.TheMindGame:
                {
                    try { AURoleOptions.PhantomCooldown = 0.1f; }
                    catch (Exception e) { Utils.ThrowException(e); }

                    goto case CustomGameMode.RoomRush;
                }
                case CustomGameMode.KingOfTheZones:
                case CustomGameMode.SoloPVP:
                {
                    try { AURoleOptions.GuardianAngelCooldown = 900f; }
                    catch (Exception e) { Utils.ThrowException(e); }

                    goto case CustomGameMode.RoomRush;
                }
                case CustomGameMode.Standard:
                {
                    President.OnAnyoneApplyGameOptions(opt);
            
                    foreach (CustomRoles subRole in state.SubRoles)
                    {
                        if (subRole.IsGhostRole() && subRole != CustomRoles.EvilSpirit)
                        {
                            AURoleOptions.GuardianAngelCooldown = GhostRolesManager.AssignedGhostRoles.First(x => x.Value.Role == subRole).Value.Instance.Cooldown;
                            continue;
                        }

                        switch (subRole)
                        {
                            case CustomRoles.Watcher:
                            {
                                opt.SetBool(BoolOptionNames.AnonymousVotes, false);
                                break;
                            }
                            case CustomRoles.Flash:
                            {
                                Main.AllPlayerSpeed[player.PlayerId] = Options.FlashSpeed.GetFloat();
                                break;
                            }
                            case CustomRoles.Giant:
                            {
                                Main.AllPlayerSpeed[player.PlayerId] = Options.GiantSpeed.GetFloat();
                                break;
                            }
                            case CustomRoles.Mare when Options.MareHasIncreasedSpeed.GetBool():
                            {
                                Main.AllPlayerSpeed[player.PlayerId] = Options.MareSpeedDuringLightsOut.GetFloat();
                                break;
                            }
                            case CustomRoles.Sleep when player.IsAlive() && Utils.IsActive(SystemTypes.Electrical):
                            {
                                SetBlind();
                                Main.AllPlayerSpeed[player.PlayerId] = Main.MinSpeed;
                                break;
                            }
                            case CustomRoles.Torch:
                            {
                                if (!Utils.IsActive(SystemTypes.Electrical))
                                {
                                    opt.SetVision(true);
                                    opt.SetFloat(FloatOptionNames.CrewLightMod, Options.TorchVision.GetFloat());
                                    opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.TorchVision.GetFloat());
                                }
                                else if (!Options.TorchAffectedByLights.GetBool())
                                {
                                    opt.SetVision(true);
                                    opt.SetFloat(FloatOptionNames.CrewLightMod, Options.TorchVision.GetFloat() * 5);
                                    opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.TorchVision.GetFloat() * 5);
                                }

                                break;
                            }
                            case CustomRoles.Bewilder when !Utils.IsActive(SystemTypes.Electrical):
                            {
                                opt.SetVision(false);
                                opt.SetFloat(FloatOptionNames.CrewLightMod, Options.BewilderVision.GetFloat());
                                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.BewilderVision.GetFloat());
                                break;
                            }
                            case CustomRoles.Sunglasses when !Utils.IsActive(SystemTypes.Electrical):
                            {
                                opt.SetVision(false);
                                opt.SetFloat(FloatOptionNames.CrewLightMod, Options.SunglassesVision.GetFloat());
                                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.SunglassesVision.GetFloat());
                                break;
                            }
                            case CustomRoles.Reach:
                            {
                                opt.SetInt(Int32OptionNames.KillDistance, 2);
                                break;
                            }
                            case CustomRoles.Madmate:
                            {
                                opt.SetVision(Options.MadmateHasImpostorVision.GetBool());
                                break;
                            }
                            case CustomRoles.Lovers when Main.LoversPlayers.Count(x => x.IsAlive()) == 1 && Lovers.LoverDieConsequence.GetValue() == 2:
                            {
                                opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision / 2f);
                                opt.SetFloat(FloatOptionNames.ImpostorFlashlightSize, Main.DefaultImpostorVision / 2f);
                                break;
                            }
                            case CustomRoles.Nimble when roleTypes == RoleTypes.Engineer:
                            {
                                AURoleOptions.EngineerCooldown = Nimble.NimbleCD.GetFloat();
                                AURoleOptions.EngineerInVentMaxTime = Nimble.NimbleInVentTime.GetFloat();
                                break;
                            }
                            case CustomRoles.Physicist when roleTypes == RoleTypes.Scientist:
                            {
                                AURoleOptions.ScientistCooldown = Physicist.PhysicistCD.GetFloat();
                                AURoleOptions.ScientistBatteryCharge = Physicist.PhysicistViewDuration.GetFloat();
                                break;
                            }
                            case CustomRoles.Finder when roleTypes == RoleTypes.Tracker:
                            {
                                AURoleOptions.TrackerCooldown = Finder.FinderCD.GetFloat();
                                AURoleOptions.TrackerDuration = Finder.FinderDuration.GetFloat();
                                AURoleOptions.TrackerDelay = Finder.FinderDelay.GetFloat();
                                break;
                            }
                            case CustomRoles.Noisy when roleTypes == RoleTypes.Noisemaker:
                            {
                                AURoleOptions.NoisemakerImpostorAlert = Noisy.NoisyImpostorAlert.GetBool();
                                AURoleOptions.NoisemakerAlertDuration = Noisy.NoisyAlertDuration.GetFloat();
                                break;
                            }
                            case CustomRoles.Examiner when roleTypes == RoleTypes.Detective:
                            {
                                AURoleOptions.DetectiveSuspectLimit = Examiner.ExaminerSuspectLimit.GetFloat();
                                break;
                            }
                            case CustomRoles.Venom when roleTypes == RoleTypes.Viper:
                            {
                                AURoleOptions.ViperDissolveTime = Venom.VenomDissolveTime.GetFloat();
                                break;
                            }
                        }
                    }

                    break;
                }
            }

            switch (player.GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    AURoleOptions.ShapeshifterCooldown = Options.DefaultShapeshiftCooldown.GetFloat();
                    AURoleOptions.GuardianAngelCooldown = Spiritcaller.SpiritAbilityCooldown.GetFloat();
                    break;
                case CustomRoleTypes.Neutral:
                case CustomRoleTypes.Crewmate:
                    AURoleOptions.GuardianAngelCooldown = Spiritcaller.SpiritAbilityCooldown.GetFloat();
                    break;
            }

            switch (role)
            {
                case CustomRoles.PhantomEHR:
                    AURoleOptions.PhantomCooldown = ImpostorVanillaRoles.PhantomCooldown.GetFloat();
                    AURoleOptions.PhantomDuration = ImpostorVanillaRoles.PhantomDuration.GetFloat();
                    break;
                case CustomRoles.ShapeshifterEHR:
                    AURoleOptions.ShapeshifterCooldown = ImpostorVanillaRoles.ShapeshiftCD.GetFloat();
                    AURoleOptions.ShapeshifterDuration = ImpostorVanillaRoles.ShapeshiftDur.GetFloat();
                    break;
                case CustomRoles.EngineerEHR:
                    AURoleOptions.EngineerCooldown = CrewmateVanillaRoles.EngineerCD.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = CrewmateVanillaRoles.EngineerDur.GetFloat();
                    break;
                case CustomRoles.NoisemakerEHR:
                    AURoleOptions.NoisemakerImpostorAlert = CrewmateVanillaRoles.NoiseMakerImpostorAlert.GetBool();
                    AURoleOptions.NoisemakerAlertDuration = CrewmateVanillaRoles.NoisemakerAlertDuration.GetFloat();
                    break;
                case CustomRoles.ScientistEHR:
                    AURoleOptions.ScientistCooldown = CrewmateVanillaRoles.ScientistCD.GetFloat();
                    AURoleOptions.ScientistBatteryCharge = CrewmateVanillaRoles.ScientistDur.GetFloat();
                    break;
                case CustomRoles.TrackerEHR:
                    AURoleOptions.TrackerCooldown = CrewmateVanillaRoles.TrackerCooldown.GetFloat();
                    AURoleOptions.TrackerDuration = CrewmateVanillaRoles.TrackerDuration.GetFloat();
                    AURoleOptions.TrackerDelay = CrewmateVanillaRoles.TrackerDelay.GetFloat();
                    break;
                case CustomRoles.DetectiveEHR:
                    AURoleOptions.DetectiveSuspectLimit = CrewmateVanillaRoles.DetectiveSuspectLimit.GetFloat();
                    break;
                case CustomRoles.ViperEHR:
                    AURoleOptions.ViperDissolveTime  = ImpostorVanillaRoles.ViperDissolveTime.GetFloat();
                    break;
            }

            // When impostor alert is off, and the player is a desync crewmate, set impostor alert as true
            if (role.IsDesyncRole() && role.IsCrewmate() && !CrewmateVanillaRoles.NoiseMakerImpostorAlert.GetBool())
                AURoleOptions.NoisemakerImpostorAlert = true;
            else
                AURoleOptions.NoisemakerImpostorAlert = CrewmateVanillaRoles.NoiseMakerImpostorAlert.GetBool();

            try
            {
                if (Shifter.WasShifter.Contains(player.PlayerId) && role.IsImpostor()) opt.SetVision(true);
            }
            catch (Exception e) { Utils.ThrowException(e); }

            try { state.Role.ApplyGameOptions(opt, player.PlayerId); }
            catch (Exception e) { Utils.ThrowException(e); }

            if (player.Is(CustomRoles.Bloodlust) && Bloodlust.HasImpVision.GetBool()) opt.SetVision(true);

            if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Bewilder) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == player.PlayerId && !x.Is(CustomRoles.Hangman)))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Options.BewilderVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.BewilderVision.GetFloat());
            }

            if ((Grenadier.GrenadierBlinding.Count > 0 &&
                 (role.IsImpostor() ||
                  (role.IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool()))) ||
                (Grenadier.MadGrenadierBlinding.Count > 0 && !role.IsImpostorTeam() && !player.Is(CustomRoles.Madmate)))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Options.GrenadierCauseVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.GrenadierCauseVision.GetFloat());
            }

            switch (role)
            {
                case CustomRoles.Alchemist when ((Alchemist)state.Role).VisionPotionActive:
                    opt.SetVision(false);

                    if (Utils.IsActive(SystemTypes.Electrical))
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Alchemist.VisionOnLightsOut.GetFloat() * 5);
                    else
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Alchemist.Vision.GetFloat());

                    break;
                case CustomRoles.Mayor when Mayor.MayorSeesVoteColorsWhenDoneTasks.GetBool() && player.GetTaskState().IsTaskFinished:
                    opt.SetBool(BoolOptionNames.AnonymousVotes, false);
                    break;
            }

            Farmer.OnAnyoneApplyGameOptions(opt, player);
            Siren.ApplyGameOptionsForOthers(opt, player.PlayerId);
            Chef.ApplyGameOptionsForOthers(opt, player.PlayerId);
            Negotiator.OnAnyoneApplyGameOptions(opt, player.PlayerId);
            Wizard.OnAnyoneApplyGameOptions(opt, player.PlayerId);
            Curser.OnAnyoneApplyGameOptions(opt, player.PlayerId);
            Auditor.OnAnyoneApplyGameOptions(opt, player.PlayerId);
            Clerk.OnAnyoneApplyGameOptions(opt, player.PlayerId);
            Spider.OnAnyoneApplyGameOptions(opt, player.PlayerId);

            if (Sprayer.LowerVisionList.Contains(player.PlayerId))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Sprayer.LoweredVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Sprayer.LoweredVision.GetFloat());
            }

            if (Minion.BlindPlayers.Contains(player.PlayerId)) SetBlind();
            if (Slenderman.IsBlinded(player.PlayerId)) SetBlind();

            if (Sentinel.IsPatrolling(player.PlayerId))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Sentinel.LoweredVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Sentinel.LoweredVision.GetFloat());
            }

            if (Beacon.IsAffectedPlayer(player.PlayerId))
            {
                opt.SetFloat(FloatOptionNames.CrewLightMod, Beacon.IncreasedVision);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Beacon.IncreasedVision);
            }

            Dazzler.SetDazzled(player, opt);
            Deathpact.SetDeathpactVision(player, opt);

            Spiritcaller.ReduceVision(opt, player);

            if (Randomizer.HasSuperVision(player)) SetMaxVision();
            else if (Randomizer.IsBlind(player)) SetBlind();

            bool energeticIncreaseSpeed = false, energeticDecreaseCooldown = false;

            if (state.SubRoles.Contains(CustomRoles.Energetic))
            {
                if (player.CanUseKillButton())
                    energeticDecreaseCooldown = true;
                else
                    switch (roleTypes)
                {
                    case RoleTypes.Impostor:
                        energeticDecreaseCooldown = true;
                        break;
                    case RoleTypes.Scientist:
                        AURoleOptions.ScientistCooldown *= 0.75f;
                        break;
                    case RoleTypes.Engineer:
                        AURoleOptions.EngineerCooldown *= 0.75f;
                        break;
                    case RoleTypes.GuardianAngel:
                        AURoleOptions.GuardianAngelCooldown *= 0.75f;
                        break;
                    case RoleTypes.Shapeshifter:
                        AURoleOptions.ShapeshifterCooldown *= 0.75f;
                        break;
                    case RoleTypes.Phantom:
                        AURoleOptions.PhantomCooldown *= 0.75f;
                        break;
                    case RoleTypes.Tracker:
                        AURoleOptions.TrackerCooldown *= 0.75f;
                        break;
                    case RoleTypes.Viper:
                        AURoleOptions.ViperDissolveTime *= 0.75f;
                        break;
                    case RoleTypes.Detective:
                        AURoleOptions.DetectiveSuspectLimit *= 1.25f;
                        goto default;
                    case RoleTypes.CrewmateGhost:
                    case RoleTypes.ImpostorGhost:
                        break;
                    default:
                        energeticIncreaseSpeed = true;
                        break;
                }
            }

            if (Magician.BlindPpl.ContainsKey(player.PlayerId))
                SetBlind();

            if (player.IsCrewmate() && Main.PlayerStates.Values.Any(s => s.Role is Adventurer { IsEnable: true } av && av.ActiveWeapons.Contains(Adventurer.Weapon.Lantern)))
                SetMaxVision();

            if (Chemist.Instances.Any(x => x.IsBlinding && player.PlayerId != x.ChemistPC.PlayerId))
                SetBlind();

            if (Changeling.ChangedRole.TryGetValue(player.PlayerId, out bool changed) && changed && roleTypes != RoleTypes.Shapeshifter)
            {
                AURoleOptions.ShapeshifterCooldown = 300f;
                AURoleOptions.ShapeshifterDuration = 1f;
            }

            if ((Options.UsePhantomBasis.GetBool() || role.AlwaysUsesPhantomBase()) && role.SimpleAbilityTrigger())
                AURoleOptions.PhantomDuration = 0.1f;

            // ===================================================================================================================

            if (state.IsBlackOut)
                SetBlind();
            
            AURoleOptions.EngineerCooldown = Mathf.Max(0.01f, AURoleOptions.EngineerCooldown);

            if (Main.AllPlayerKillCooldown.TryGetValue(player.PlayerId, out float killCooldown))
            {
                if (energeticDecreaseCooldown) killCooldown *= 0.75f;
                AURoleOptions.KillCooldown = Mathf.Max(0.01f, killCooldown);
            }

            if (Main.AllPlayerSpeed.TryGetValue(player.PlayerId, out float speed))
            {
                const float limit = 3f;
                if (energeticIncreaseSpeed) speed *= 1.25f;
                if (Mathf.Approximately(speed, 0f)) speed = Main.MinSpeed;
                
                if (Camouflage.IsCamouflage && Options.CommsCamouflageSetSameSpeed.GetBool())
                    speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                
                AURoleOptions.PlayerSpeedMod = Mathf.Clamp(speed, -limit, limit);
            }

            state.TaskState.HasTasks = Utils.HasTasks(player.Data, false);
            if (Options.GhostCanSeeOtherVotes.GetBool() && player.Data.IsDead) opt.SetBool(BoolOptionNames.AnonymousVotes, false);

            if (Options.AdditionalEmergencyCooldown.GetBool() &&
                Options.AdditionalEmergencyCooldownThreshold.GetInt() <= Utils.AllAlivePlayersCount)
            {
                opt.SetInt(
                    Int32OptionNames.EmergencyCooldown,
                    Options.AdditionalEmergencyCooldownTime.GetInt());
            }

            if (CustomRoles.ClockBlocker.RoleExist(ClockBlocker.CountAddedTimeAfterDeath.GetBool()))
            {
                int originalTime = opt.GetInt(Int32OptionNames.EmergencyCooldown);
                opt.SetInt(Int32OptionNames.EmergencyCooldown, ClockBlocker.GetTotalTime(originalTime));
            }

            if (MeetingStates.FirstMeeting)
            {
                int originalTime = opt.GetInt(Int32OptionNames.EmergencyCooldown);
                opt.SetInt(Int32OptionNames.EmergencyCooldown, originalTime + 30);
            }

            if (Options.SyncButtonMode.GetBool() && Options.SyncedButtonCount.GetValue() <= Options.UsedButtonCount)
                opt.SetInt(Int32OptionNames.EmergencyCooldown, 3600);

            MeetingTimeManager.ApplyGameOptions(opt);

            AURoleOptions.ShapeshifterCooldown = Mathf.Max(1f, AURoleOptions.ShapeshifterCooldown);
            AURoleOptions.ProtectionDurationSeconds = 0f;
            AURoleOptions.ImpostorsCanSeeProtect = false;

            Logger.Info($"Updated settings for {player.GetNameWithRole()}: Crew Vision = {opt.GetFloat(FloatOptionNames.CrewLightMod):N2}, Impostor Vision = {opt.GetFloat(FloatOptionNames.ImpostorLightMod):N2}, Speed = {opt.GetFloat(FloatOptionNames.PlayerSpeedMod):N2}", "BuildGameOptions");

            return opt;

            void SetMaxVision()
            {
                opt.SetVision(true);
                opt.SetFloat(FloatOptionNames.CrewLightMod, 1.3f);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, 1.3f);
            }

            void SetBlind()
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, 0f);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0f);
            }
        }
        catch (Exception e)
        {
            Logger.Error($"Error for {player.GetRealName()} ({player.GetCustomRole()}): {e}", "PlayerGameOptionsSender.BuildGameOptions");
            return BasedGameOptions;
        }
    }

    protected override bool AmValid()
    {
        return base.AmValid() && player != null && player.Data != null && !player.Data.Disconnected && Main.RealOptionsData != null;
    }

}
