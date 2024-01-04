using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Modules;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
class EndGamePatch
{
    public static Dictionary<byte, string> SummaryText = [];
    public static string KillLog = string.Empty;
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        GameStates.InGame = false;

        Logger.Info("-----------Game over-----------", "Phase");
        if (!GameStates.IsModHost) return;
        Main.SetRoles = [];
        Main.SetAddOns = [];
        SummaryText = [];

        foreach (var id in Main.PlayerStates.Keys)
        {
            if (Doppelganger.IsEnable && Doppelganger.DoppelVictim.ContainsKey(id))
            {
                var dpc = Utils.GetPlayerById(id);
                if (dpc != null)
                {
                    //if (id == PlayerControl.LocalPlayer.PlayerId) Main.nickName = Doppelganger.DoppelVictim[id];
                    //else
                    //{ 
                    dpc.RpcSetName(Doppelganger.DoppelVictim[id]);
                    //}
                    Main.AllPlayerNames[id] = Doppelganger.DoppelVictim[id];
                }
            }
            SummaryText[id] = Utils.SummaryTexts(id, disableColor: false);
        }

        var sb = new StringBuilder(GetString("KillLog") + ":");
        foreach (var kvp in Main.PlayerStates.OrderBy(x => x.Value.RealKiller.TIMESTAMP.Ticks))
        {
            var date = kvp.Value.RealKiller.TIMESTAMP;
            if (date == DateTime.MinValue) continue;
            var killerId = kvp.Value.GetRealKiller();
            var targetId = kvp.Key;
            sb.Append($"\n{date:T} {Main.AllPlayerNames[targetId]} ({(Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop ? string.Empty : Utils.GetDisplayRoleName(targetId, true))}{(Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop ? string.Empty : Utils.GetSubRolesText(targetId, summary: true))}) [{Utils.GetVitalText(kvp.Key)}]");
            if (killerId != byte.MaxValue && killerId != targetId)
                sb.Append($"\n\t⇐ {Main.AllPlayerNames[killerId]} ({(Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop ? string.Empty : Utils.GetDisplayRoleName(killerId, true))}{(Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop ? string.Empty : Utils.GetSubRolesText(killerId, summary: true))})");
        }
        KillLog = sb.ToString();
        if (!KillLog.Contains('\n')) KillLog = string.Empty;

        Main.NormalOptions.KillCooldown = Options.DefaultKillCooldown;
        //winnerListリセット
        TempData.winners = new Il2CppSystem.Collections.Generic.List<WinningPlayerData>();

        var winner = Main.AllPlayerControls.Where(pc => CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId)).ToList();

        foreach (var team in CustomWinnerHolder.WinnerRoles)
        {
            winner.AddRange(Main.AllPlayerControls.Where(p => p.Is(team) && !winner.Contains(p)));
        }

        Main.winnerNameList = [];
        Main.winnerList = [];
        Main.winnerRolesList = [];
        foreach (PlayerControl pc in winner.ToArray())
        {
            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw && pc.Is(CustomRoles.GM)) continue;

            TempData.winners.Add(new WinningPlayerData(pc.Data));
            Main.winnerList.Add(pc.PlayerId);
            Main.winnerNameList.Add(pc.GetRealName());
            Main.winnerRolesList.Add(pc.GetCustomRole());
        }

        BountyHunter.ChangeTimer = [];
        Main.isDoused = [];
        Main.isDraw = [];
        Main.isRevealed = [];

        Main.VisibleTasksCount = false;
        if (AmongUsClient.Instance.AmHost)
        {
            Main.RealOptionsData.Restore(GameOptionsManager.Instance.CurrentGameOptions);
            GameOptionsSender.AllSenders.Clear();
            GameOptionsSender.AllSenders.Add(new NormalGameOptionsSender());
            /* Send SyncSettings RPC */
        }
    }
}
[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
class SetEverythingUpPatch
{
    public static string LastWinsText = string.Empty;
    public static string LastWinsReason = string.Empty;

    public static void Postfix(EndGameManager __instance)
    {
        if (!Main.playerVersion.ContainsKey(0)) return;
        //#######################################
        //      ==Victory faction display==
        //#######################################

        try
        {
            // ---------- Code from TOR (The Other Roles)! ----------
            if (Options.CurrentGameMode is not CustomGameMode.Standard) goto End;
            int num = Mathf.CeilToInt(7.5f);
            List<WinningPlayerData> winningPlayerDataList = TempData.winners.ToArray().ToList();
            for (int i = 0; i < winningPlayerDataList.Count; i++)
            {
                WinningPlayerData winningPlayerData2 = winningPlayerDataList[i];
                int num2 = (i % 2 == 0) ? -1 : 1;
                int num3 = (i + 1) / 2;
                float num4 = num3 / (float)num;
                float num5 = Mathf.Lerp(1f, 0.75f, num4);
                float num6 = (i == 0) ? -8 : -1;
                PoolablePlayer poolablePlayer = UnityEngine.Object.Instantiate(__instance.PlayerPrefab, __instance.transform);
                poolablePlayer.transform.localPosition = new Vector3(1f * num2 * num3 * num5, FloatRange.SpreadToEdges(-1.125f, 0f, num3, num), num6 + num3 * 0.01f) * 0.9f;
                float num7 = Mathf.Lerp(1f, 0.65f, num4) * 0.9f;
                Vector3 vector = new(num7, num7, 1f);
                poolablePlayer.transform.localScale = vector;
                poolablePlayer.UpdateFromPlayerOutfit(winningPlayerData2, PlayerMaterial.MaskType.ComplexUI, winningPlayerData2.IsDead, true);
                if (winningPlayerData2.IsDead)
                {
                    poolablePlayer.cosmetics.currentBodySprite.BodySprite.sprite = poolablePlayer.cosmetics.currentBodySprite.GhostSprite;
                    poolablePlayer.SetDeadFlipX(i % 2 == 0);
                }
                else
                {
                    poolablePlayer.SetFlipX(i % 2 == 0);
                }

                bool lowered = i is 1 or 2 or 5 or 6 or 9 or 10 or 13 or 14;

                poolablePlayer.cosmetics.nameText.color = Color.white;
                poolablePlayer.cosmetics.nameText.transform.localScale = new Vector3(1f / vector.x, 1f / vector.y, 1f / vector.z);
                poolablePlayer.cosmetics.nameText.text = winningPlayerData2.PlayerName;

                Vector3 defaultPos = poolablePlayer.cosmetics.nameText.transform.localPosition;

                for (int i1 = 0; i1 < Main.winnerList.Count; i1++)
                {
                    byte id = Main.winnerList[i1];
                    if (Main.winnerNameList[i1].RemoveHtmlTags() != winningPlayerData2?.PlayerName.RemoveHtmlTags()) continue;
                    var role = Main.PlayerStates[id].MainRole;
                    var color = Main.roleColors[role];
                    var rolename = Utils.GetRoleName(role);
                    poolablePlayer.cosmetics.nameText.text += $"\n<color={color}>{rolename}</color>";
                    poolablePlayer.cosmetics.nameText.transform.localPosition = new Vector3(defaultPos.x, !lowered || role.IsImpostor() ? defaultPos.y - 0.6f : defaultPos.y - 1.4f, -15f);
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString(), "OutroPatch.SetEverythingUpPatch.Postfix");
        }

    End:

        __instance.WinText.alignment = TMPro.TextAlignmentOptions.Center;
        var WinnerTextObject = UnityEngine.Object.Instantiate(__instance.WinText.gameObject);
        WinnerTextObject.transform.position = new(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 0.5f, __instance.WinText.transform.position.z);
        WinnerTextObject.transform.localScale = new(0.6f, 0.6f, 0.6f);
        var WinnerText = WinnerTextObject.GetComponent<TMPro.TextMeshPro>(); //WinTextと同じ型のコンポーネントを取得
        WinnerText.fontSizeMin = 3f;
        WinnerText.text = string.Empty;

        string CustomWinnerText = string.Empty;
        string AdditionalWinnerText = string.Empty;
        string CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Crewmate);

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                {
                    var winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.WinText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    __instance.WinText.fontSize -= 5f;
                    __instance.WinText.color = Main.PlayerColors[winnerId];
                    __instance.BackgroundBar.material.color = new Color32(245, 82, 82, 255);
                    WinnerText.text = $"<color=#f55252>{GetString("ModeSoloKombat")}</color>";
                    WinnerText.color = Color.red;
                    goto EndOfText;
                }
            case CustomGameMode.FFA:
                {
                    var winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(0, 255, 255, 255);
                    WinnerText.text = Main.AllPlayerNames[winnerId] + " wins!";
                    WinnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
            case CustomGameMode.MoveAndStop:
                {
                    var winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(0, 255, 160, 255);
                    WinnerText.text = Main.AllPlayerNames[winnerId] + " wins!";
                    WinnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
        }

        var winnerRole = (CustomRoles)CustomWinnerHolder.WinnerTeam;
        if (winnerRole >= 0)
        {
            CustomWinnerText = GetWinnerRoleName(winnerRole);
            CustomWinnerColor = Utils.GetRoleColorCode(winnerRole);
            //     __instance.WinText.color = Utils.GetRoleColor(winnerRole);
            __instance.BackgroundBar.material.color = Utils.GetRoleColor(winnerRole);
            if (winnerRole.IsNeutral())
            {
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(winnerRole);
            }
        }
        if (AmongUsClient.Instance.AmHost && Main.PlayerStates[0].MainRole == CustomRoles.GM)
        {
            __instance.WinText.text = GetString("GameOver");
            __instance.WinText.color = Utils.GetRoleColor(CustomRoles.GM);
            __instance.BackgroundBar.material.color = Utils.GetRoleColor(winnerRole);
        }
        switch (CustomWinnerHolder.WinnerTeam)
        {
            //通常勝利
            case CustomWinner.Crewmate:
                CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Engineer);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Engineer);
                break;
            case CustomWinner.Impostor:
                CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Impostor);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Impostor);
                break;
            case CustomWinner.Rogue:
                CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Rogue);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Rogue);
                break;
            case CustomWinner.Egoist:
                CustomWinnerColor = Utils.GetRoleColorCode(CustomRoles.Egoist);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Egoist);
                break;
            //特殊勝利
            case CustomWinner.Terrorist:
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Terrorist);
                break;
            case CustomWinner.Lovers:
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Lovers);
                break;
            //引き分け処理
            case CustomWinner.Draw:
                __instance.WinText.text = GetString("ForceEnd");
                __instance.WinText.color = Color.white;
                __instance.BackgroundBar.material.color = Color.gray;
                WinnerText.text = GetString("ForceEndText");
                WinnerText.color = Color.gray;
                break;
            case CustomWinner.Neutrals:
                __instance.WinText.text = GetString("DefeatText");
                __instance.WinText.color = Utils.GetRoleColor(CustomRoles.Impostor);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Executioner);
                WinnerText.text = GetString("NeutralsLeftText");
                WinnerText.color = Utils.GetRoleColor(CustomRoles.Executioner);
                break;
            //全滅
            case CustomWinner.None:
                __instance.WinText.text = string.Empty;
                __instance.WinText.color = Color.black;
                __instance.BackgroundBar.material.color = Color.gray;
                WinnerText.text = GetString("EveryoneDied");
                WinnerText.color = Color.gray;
                break;
            case CustomWinner.Error:
                __instance.WinText.text = GetString("ErrorEndText");
                __instance.WinText.color = Color.red;
                __instance.BackgroundBar.material.color = Color.red;
                WinnerText.text = GetString("ErrorEndTextDescription");
                WinnerText.color = Color.white;
                break;
        }

        foreach (var additionalWinners in CustomWinnerHolder.AdditionalWinnerTeams)
        {
            var addWinnerRole = (CustomRoles)additionalWinners;
            if (addWinnerRole == CustomRoles.Sidekick) continue;
            AdditionalWinnerText += "\n" + Utils.ColorString(Utils.GetRoleColor(addWinnerRole), GetAdditionalWinnerRoleName(addWinnerRole));
        }
        if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None and not CustomWinner.Error)
        {
            if (AdditionalWinnerText == string.Empty) WinnerText.text = $"<size=100%><color={CustomWinnerColor}>{CustomWinnerText}</color></size>";
            else WinnerText.text = $"<size=100%><color={CustomWinnerColor}>{CustomWinnerText}</color></size><size=50%>{AdditionalWinnerText}</size>";
        }

        static string GetWinnerRoleName(CustomRoles role)
        {
            var name = GetString($"WinnerRoleText.{Enum.GetName(typeof(CustomRoles), role)}");
            if (name == string.Empty || name.StartsWith("*") || name.StartsWith("<INVALID")) name = Utils.GetRoleName(role);
            return name;
        }
        static string GetAdditionalWinnerRoleName(CustomRoles role)
        {
            var name = GetString($"AdditionalWinnerRoleText.{Enum.GetName(typeof(CustomRoles), role)}");
            if (name == string.Empty || name.StartsWith("*") || name.StartsWith("<INVALID")) name = Utils.GetRoleName(role);
            return name;
        }

    EndOfText:

        LastWinsText = WinnerText.text/*.RemoveHtmlTags()*/;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //########################################
        //     ==The final result indicates==
        //########################################

        var Pos = Camera.main.ViewportToWorldPoint(new Vector3(0f, 1f, Camera.main.nearClipPlane));
        var RoleSummaryObject = UnityEngine.Object.Instantiate(__instance.WinText.gameObject);
        RoleSummaryObject.transform.position = new Vector3(__instance.Navigation.ExitButton.transform.position.x + 0.1f, Pos.y - 0.1f, -15f);
        RoleSummaryObject.transform.localScale = new Vector3(1f, 1f, 1f);

        StringBuilder sb = new($"{GetString("RoleSummaryText")}\n<b>");
        List<byte> cloneRoles = new(Main.PlayerStates.Keys);
        foreach (byte id in Main.winnerList.ToArray())
        {
            if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
            sb.Append('\n').Append(EndGamePatch.SummaryText[id]);
            cloneRoles.Remove(id);
        }
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                {
                    List<(int, byte)> list = [];
                    foreach (byte id in cloneRoles.ToArray())
                    {
                        list.Add((SoloKombatManager.GetRankOfScore(id), id));
                    }

                    list.Sort();
                    foreach (var id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2)))
                        sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id.Item2]);
                    break;
                }
            case CustomGameMode.FFA:
                {
                    List<(int, byte)> list = [];
                    foreach (byte id in cloneRoles.ToArray())
                    {
                        list.Add((FFAManager.GetRankOfScore(id), id));
                    }

                    list.Sort();
                    foreach (var id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2)))
                        sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id.Item2]);
                    break;
                }
            case CustomGameMode.MoveAndStop:
                {
                    List<(int, byte)> list = [];
                    foreach (byte id in cloneRoles.ToArray())
                    {
                        list.Add((MoveAndStopManager.GetRankOfScore(id), id));
                    }

                    list.Sort();
                    foreach (var id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2)))
                        sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id.Item2]);
                    break;
                }
            default:
                {
                    sb.Append($"</b>\n");
                    foreach (byte id in cloneRoles.ToArray())
                    {
                        if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
                        sb.Append('\n').Append(EndGamePatch.SummaryText[id]);
                    }

                    break;
                }
        }
        var RoleSummary = RoleSummaryObject.GetComponent<TMPro.TextMeshPro>();
        RoleSummary.alignment = TMPro.TextAlignmentOptions.TopLeft;
        RoleSummary.color = Color.white;
        RoleSummary.outlineWidth *= 1.2f;
        RoleSummary.fontSizeMin = RoleSummary.fontSizeMax = RoleSummary.fontSize = 1.25f;

        var RoleSummaryRectTransform = RoleSummary.GetComponent<RectTransform>();
        RoleSummaryRectTransform.anchoredPosition = new Vector2(Pos.x + 3.5f, Pos.y - 0.1f);
        RoleSummary.text = sb.ToString();

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //Utils.ApplySuffix();
    }
}