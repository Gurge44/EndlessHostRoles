using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE.Roles.Impostor
{
    internal class Warlock : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            Main.CursedPlayers.Add(playerId, null);
            Main.isCurseAndKill.Add(playerId, false);
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            try
            {
                AURoleOptions.ShapeshifterCooldown = Main.isCursed ? 1f : Options.DefaultKillCooldown;
                AURoleOptions.ShapeshifterDuration = Options.WarlockShiftDuration.GetFloat();
            }
            catch
            {
            }
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            bool curse = Main.isCurseAndKill.TryGetValue(id, out bool wcs) && wcs;
            bool shapeshifting = id.IsPlayerShifted();
            if (!shapeshifting && !curse)
                hud.KillButton?.OverrideText(Translator.GetString("WarlockCurseButtonText"));
            else
                hud.KillButton?.OverrideText(Translator.GetString("KillButtonText"));
            if (!shapeshifting && curse)
                hud.AbilityButton?.OverrideText(Translator.GetString("WarlockShapeshiftButtonText"));
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            Main.isCurseAndKill.TryAdd(killer.PlayerId, false);

            if (!killer.IsShifted() && !Main.isCurseAndKill[killer.PlayerId])
            {
                if (target.Is(CustomRoles.Needy) || target.Is(CustomRoles.Lazy)) return false;
                Main.isCursed = true;
                killer.SetKillCooldown();
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                killer.RPCPlayCustomSound("Line");
                Main.CursedPlayers[killer.PlayerId] = target;
                Main.WarlockTimer.Add(killer.PlayerId, 0f);
                Main.isCurseAndKill[killer.PlayerId] = true;
                return false;
            }

            if (killer.IsShifted()) return true;

            if (Main.isCurseAndKill[killer.PlayerId]) killer.RpcGuardAndKill(target);
            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            Curse(pc);
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            Curse(shapeshifter);

            return false;
        }

        static void Curse(PlayerControl pc)
        {
            Main.isCurseAndKill.TryAdd(pc.PlayerId, false);
            if (Main.CursedPlayers[pc.PlayerId] != null)
            {
                if (!Main.CursedPlayers[pc.PlayerId].Data.IsDead)
                {
                    var cp = Main.CursedPlayers[pc.PlayerId];
                    Vector2 cppos = cp.Pos();
                    Dictionary<PlayerControl, float> cpdistance = [];
                    foreach (PlayerControl p in Main.AllAlivePlayerControls)
                    {
                        if (p.PlayerId == cp.PlayerId) continue;
                        if (!Options.WarlockCanKillSelf.GetBool() && p.PlayerId == pc.PlayerId) continue;
                        if (!Options.WarlockCanKillAllies.GetBool() && p.GetCustomRole().IsImpostor()) continue;
                        if (p.Is(CustomRoles.Pestilence)) continue;
                        if (Pelican.IsEaten(p.PlayerId) || Medic.ProtectList.Contains(p.PlayerId)) continue;
                        float dis = Vector2.Distance(cppos, p.Pos());
                        cpdistance.Add(p, dis);
                        Logger.Info($"{p.Data?.PlayerName}'s distance: {dis}", "Warlock");
                    }

                    if (cpdistance.Count > 0)
                    {
                        var min = cpdistance.OrderBy(c => c.Value).FirstOrDefault();
                        PlayerControl targetw = min.Key;
                        if (cp.RpcCheckAndMurder(targetw, true))
                        {
                            targetw.SetRealKiller(pc);
                            Logger.Info($"{targetw.GetNameWithRole().RemoveHtmlTags()} was killed", "Warlock");
                            cp.Kill(targetw);
                            pc.SetKillCooldown();
                            pc.Notify(Translator.GetString("WarlockControlKill"));
                        }

                        _ = new LateTask(() => { pc.CmdCheckRevertShapeshift(false); }, 1.5f, "Warlock RpcRevertShapeshift");
                    }
                    else
                    {
                        pc.Notify(Translator.GetString("WarlockNoTarget"));
                    }

                    Main.isCurseAndKill[pc.PlayerId] = false;
                }

                Main.CursedPlayers[pc.PlayerId] = null;
            }
        }
    }
}
