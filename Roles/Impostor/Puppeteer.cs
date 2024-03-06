using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    internal class Puppeteer : RoleBase
    {
        public static Dictionary<byte, byte> PuppeteerList = [];
        public static Dictionary<byte, long> PuppeteerDelayList = [];
        public static Dictionary<byte, int> PuppeteerDelay = [];
        public static Dictionary<byte, int> PuppeteerMaxPuppets = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(3900, TabGroup.ImpostorRoles, CustomRoles.Puppeteer);
            PuppeteerCD = FloatOptionItem.Create(3911, "PuppeteerCD", new(2.5f, 60f, 2.5f), 22.5f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
                .SetValueFormat(OptionFormat.Seconds);
            PuppeteerCanKillNormally = BooleanOptionItem.Create(3917, "PuppeteerCanKillNormally", true, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
            PuppeteerKCD = FloatOptionItem.Create(3912, "PuppeteerKCD", new(2.5f, 60f, 2.5f), 25f, TabGroup.ImpostorRoles, false)
                .SetParent(PuppeteerCanKillNormally)
                .SetValueFormat(OptionFormat.Seconds);
            PuppeteerMinDelay = IntegerOptionItem.Create(3913, "PuppeteerMinDelay", new(0, 90, 1), 3, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
                .SetValueFormat(OptionFormat.Seconds);
            PuppeteerMaxDelay = IntegerOptionItem.Create(3914, "PuppeteerMaxDelay", new(0, 90, 1), 7, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
                .SetValueFormat(OptionFormat.Seconds);
            PuppeteerManipulationEndsAfterFixedTime = BooleanOptionItem.Create(3915, "PuppeteerManipulationEndsAfterFixedTime", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
            PuppeteerManipulationEndsAfterTime = IntegerOptionItem.Create(3916, "PuppeteerManipulationEndsAfterTime", new(0, 90, 1), 30, TabGroup.ImpostorRoles, false)
                .SetParent(PuppeteerManipulationEndsAfterFixedTime)
                .SetValueFormat(OptionFormat.Seconds);
            PuppeteerManipulationBypassesLazy = BooleanOptionItem.Create(3918, "PuppeteerManipulationBypassesLazy", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
            PuppeteerManipulationBypassesLazyGuy = BooleanOptionItem.Create(3922, "PuppeteerManipulationBypassesLazyGuy", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
            PuppeteerPuppetCanKillImpostors = BooleanOptionItem.Create(3919, "PuppeteerPuppetCanKillImpostors", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
            PuppeteerPuppetCanKillPuppeteer = BooleanOptionItem.Create(3920, "PuppeteerPuppetCanKillPuppeteer", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
            Options.PuppeteerMaxPuppets = IntegerOptionItem.Create(3921, "PuppeteerMaxPuppets", new(0, 30, 1), 5, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer])
                .SetValueFormat(OptionFormat.Times);
            PuppeteerDiesAfterMaxPuppets = BooleanOptionItem.Create(3923, "PuppeteerDiesAfterMaxPuppets", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Puppeteer]);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
            PuppeteerList = [];
            PuppeteerDelayList = [];
            PuppeteerDelay = [];
            PuppeteerMaxPuppets = [];
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.PuppeteerCanKillNormally.GetBool() ? Options.PuppeteerKCD.GetFloat() : Options.PuppeteerCD.GetFloat();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("PuppeteerOperateButtonText"));
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (target.Is(CustomRoles.Needy) && Options.PuppeteerManipulationBypassesLazyGuy.GetBool()) return false;
            if (target.Is(CustomRoles.Lazy) && Options.PuppeteerManipulationBypassesLazy.GetBool()) return false;
            if (Medic.ProtectList.Contains(target.PlayerId)) return false;

            if (!PuppeteerMaxPuppets.TryGetValue(killer.PlayerId, out var usesLeft))
            {
                usesLeft = Options.PuppeteerMaxPuppets.GetInt();
                PuppeteerMaxPuppets.Add(killer.PlayerId, usesLeft);
            }

            if (Options.PuppeteerCanKillNormally.GetBool())
            {
                return killer.CheckDoubleTrigger(target, () =>
                {
                    PuppeteerList[target.PlayerId] = killer.PlayerId;
                    PuppeteerDelayList[target.PlayerId] = Utils.TimeStamp;
                    PuppeteerDelay[target.PlayerId] = IRandom.Instance.Next(Options.PuppeteerMinDelay.GetInt(), Options.PuppeteerMaxDelay.GetInt());
                    killer.SetKillCooldown(time: Options.PuppeteerCD.GetFloat());
                    if (usesLeft <= 1)
                    {
                        _ = new LateTask(() => { killer.Suicide(); }, 1.5f, "Puppeteer Max Uses Reached => Suicide");
                    }
                    else killer.Notify(string.Format(Translator.GetString("PuppeteerUsesRemaining"), usesLeft - 1));

                    PuppeteerMaxPuppets[killer.PlayerId]--;
                    killer.RPCPlayCustomSound("Line");
                    Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                });
            }

            PuppeteerList[target.PlayerId] = killer.PlayerId;
            PuppeteerDelayList[target.PlayerId] = Utils.TimeStamp;
            PuppeteerDelay[target.PlayerId] = IRandom.Instance.Next(Options.PuppeteerMinDelay.GetInt(), Options.PuppeteerMaxDelay.GetInt());
            killer.SetKillCooldown();
            if (usesLeft <= 1)
            {
                _ = new LateTask(() => { killer.Suicide(); }, 1.5f, "Puppeteer Max Uses Reached => Suicide");
            }
            else killer.Notify(string.Format(Translator.GetString("PuppeteerUsesRemaining"), usesLeft - 1));

            PuppeteerMaxPuppets[killer.PlayerId]--;
            killer.RPCPlayCustomSound("Line");
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            return false;
        }

        public override void OnGlobalFixedUpdate(PlayerControl player, bool lowLoad)
        {
            if (player == null || lowLoad) return;

            byte playerId = player.PlayerId;
            long now = Utils.TimeStamp;

            if (GameStates.IsInTask && PuppeteerList.ContainsKey(playerId))
            {
                if (!player.IsAlive() || Pelican.IsEaten(playerId))
                {
                    PuppeteerList.Remove(playerId);
                    PuppeteerDelayList.Remove(playerId);
                    PuppeteerDelay.Remove(playerId);
                }
                else if (PuppeteerDelayList[playerId] + Options.PuppeteerManipulationEndsAfterTime.GetInt() < now && Options.PuppeteerManipulationEndsAfterFixedTime.GetBool())
                {
                    PuppeteerList.Remove(playerId);
                    PuppeteerDelayList.Remove(playerId);
                    PuppeteerDelay.Remove(playerId);
                    Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Puppeteer)).Do(x => Utils.NotifyRoles(SpecifySeer: x, SpecifyTarget: player));
                }
                else if (PuppeteerDelayList[playerId] + PuppeteerDelay[playerId] < now)
                {
                    Vector2 puppeteerPos = player.transform.position;
                    Dictionary<byte, float> targetDistance = [];
                    foreach (PlayerControl target in Main.AllAlivePlayerControls)
                    {
                        if (target.PlayerId == playerId || target.Is(CustomRoles.Pestilence)) continue;
                        if (target.Is(CustomRoles.Puppeteer) && !Options.PuppeteerPuppetCanKillPuppeteer.GetBool()) continue;
                        if (target.Is(CustomRoleTypes.Impostor) && !Options.PuppeteerPuppetCanKillImpostors.GetBool()) continue;

                        float dis = Vector2.Distance(puppeteerPos, target.transform.position);
                        targetDistance.Add(target.PlayerId, dis);
                    }

                    if (targetDistance.Count > 0)
                    {
                        var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();
                        PlayerControl target = Utils.GetPlayerById(min.Key);
                        var KillRange = NormalGameOptionsV07.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
                        if (min.Value <= KillRange && player.CanMove && target.CanMove)
                        {
                            if (player.RpcCheckAndMurder(target, true))
                            {
                                var puppeteerId = PuppeteerList[playerId];
                                RPC.PlaySoundRPC(puppeteerId, Sounds.KillSound);
                                target.SetRealKiller(Utils.GetPlayerById(puppeteerId));
                                player.Kill(target);
                                player.MarkDirtySettings();
                                target.MarkDirtySettings();
                                PuppeteerList.Remove(playerId);
                                PuppeteerDelayList.Remove(playerId);
                                PuppeteerDelay.Remove(playerId);
                                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
                            }
                        }
                    }
                }
            }
        }
    }
}
