/*using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace EHR.Gamemodes;

public static class RRTimeTester
{
    public static string HUDText;
    
    public static System.Collections.IEnumerator DoTest()
    {
        var allRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
        allRooms.Remove(SystemTypes.Hallway);
        allRooms.Remove(SystemTypes.Outside);
        if (SubmergedCompatibility.IsSubmerged()) allRooms.RemoveWhere(x => (byte)x > 135);
        var map = RandomSpawn.SpawnMap.GetSpawnMap();
        var stopwatch = new Stopwatch();
        var mapName = Main.CurrentMap.ToString();
        var doneCombos = new List<(SystemTypes, SystemTypes)>();

        foreach (SystemTypes systemTypes in allRooms)
        {
            foreach (SystemTypes otherSystemTypes in allRooms)
            {
                if (systemTypes == otherSystemTypes || doneCombos.Exists(x => x.Item1 == systemTypes && x.Item2 == otherSystemTypes || x.Item1 == otherSystemTypes && x.Item2 == systemTypes)) continue;
                string otherSystemTypesName = Translator.GetString(otherSystemTypes.ToString());
                Redo:
                HUDText = $"<#ffff00><size=200%>{otherSystemTypesName}</size></color>";
                PlayerControl.LocalPlayer.TP(map.Positions.GetValueOrDefault(systemTypes, systemTypes.GetRoomClass().transform.position));
                yield return new WaitForSeconds(0.5f);
                while (PlayerControl.LocalPlayer.IsInRoom(systemTypes)) yield return null;
                stopwatch.Restart();
                while (PlayerControl.LocalPlayer.onLadder || PlayerControl.LocalPlayer.inMovingPlat || !PlayerControl.LocalPlayer.IsInRoom(otherSystemTypes))
                {
                    HUDText = $"<size=200%>{otherSystemTypesName}</size>\n{stopwatch.Elapsed.TotalSeconds:N1}";
                    yield return null;
                    if (Input.GetKey(KeyCode.Backspace)) goto Redo;
                }
                stopwatch.Stop();
                doneCombos.Add((systemTypes, otherSystemTypes));
                File.AppendAllText($"./RRTestResults-{mapName}.txt", $"\n{systemTypes}-{otherSystemTypes}:{stopwatch.Elapsed.TotalSeconds + 1:N0}");
            }
        }

        HUDText = "<#00ff00><size=200%>TEST COMPLETE</size></color>";
    }
}*/