using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;

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

        public override void OnGlobalFixedUpdate(PlayerControl player)
        {
            if (player == null) return;

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
