﻿using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace EHR;

public static class ServerAddManager
{
    private static readonly ServerManager serverManager = DestroyableSingleton<ServerManager>.Instance;

    public static void Init()
    {
        if (CultureInfo.CurrentCulture.Name.StartsWith("zh") && serverManager.AvailableRegions.Count == 10) return;
        if (!CultureInfo.CurrentCulture.Name.StartsWith("zh") && serverManager.AvailableRegions.Count == 7) return;

        serverManager.AvailableRegions = ServerManager.DefaultRegions;
        List<IRegionInfo> regionInfos =
        [
            //.. CultureInfo.CurrentCulture.Name.StartsWith("zh") ? [CreateHttp("au-sh.pafyx.top", "梦服上海 (新)", 22000, false)] : [], // VS is dumb
            CreateHttp("au-as.duikbo.at", "Modded Asia (MAS)", 443, true),
            CreateHttp("www.aumods.xyz", "Modded NA (MNA)", 443, true),
            CreateHttp("au-eu.duikbo.at", "Modded EU (MEU)", 443, true),
            CreateHttp("35.247.251.253", "Modded SA (MSA)", 22023, false),
        ];

        regionInfos.Where(x => !serverManager.AvailableRegions.Contains(x)).Do(serverManager.AddOrUpdateRegion);
    }

    public static IRegionInfo CreateHttp(string ip, string name, ushort port, bool ishttps)
    {
        string serverIp = (ishttps ? "https://" : "http://") + ip;
        ServerInfo serverInfo = new(name, serverIp, port, false);
        ServerInfo[] ServerInfo = [serverInfo];
        return new StaticHttpRegionInfo(name, (StringNames)1003, ip, ServerInfo).CastFast<IRegionInfo>();
    }

    private static class CastHelper<T> where T : Il2CppObjectBase
    {
        public static Func<IntPtr, T> Cast;

        static CastHelper()
        {
            var constructor = typeof(T).GetConstructor([typeof(IntPtr)]);
            var ptr = Expression.Parameter(typeof(IntPtr));
            var create = Expression.New(constructor!, ptr);
            var lambda = Expression.Lambda<Func<IntPtr, T>>(create, ptr);
            Cast = lambda.Compile();
        }
    }

    private static T CastFast<T>(this Il2CppObjectBase obj) where T : Il2CppObjectBase
    {
        if (obj is T casted) return casted;
        return CastHelper<T>.Cast(obj.Pointer);
    }
}