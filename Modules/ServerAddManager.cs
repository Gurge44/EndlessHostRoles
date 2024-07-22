using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using Il2CppInterop.Runtime.InteropTypes;

namespace EHR;

public static class ServerAddManager
{
    private static readonly ServerManager ServerManager = DestroyableSingleton<ServerManager>.Instance;

    public static void Init()
    {
        if (CultureInfo.CurrentCulture.Name.StartsWith("zh") && ServerManager.AvailableRegions.Count == 10) return;
        if (!CultureInfo.CurrentCulture.Name.StartsWith("zh") && ServerManager.AvailableRegions.Count == 7) return;

        ServerManager.AvailableRegions = ServerManager.DefaultRegions;
        List<IRegionInfo> regionInfos = [];
        if (CultureInfo.CurrentCulture.Name.StartsWith("zh"))
        {
            regionInfos.Add((CreateHttp("45yun.cn", "小猫[北京]", 22000, false)));
            regionInfos.Add((CreateHttp("45yun.cn", "小猫[成都]", 2267, false)));
            regionInfos.Add((CreateHttp("mau.kaifuxia.top", "新梦初[上海]", 25000, false)));
        }

        regionInfos.Add(CreateHttp("au-as.duikbo.at", "Modded Asia (MAS)", 443, true));
        regionInfos.Add(CreateHttp("www.aumods.us", "Modded NA (MNA)", 443, true));
        regionInfos.Add(CreateHttp("au-eu.duikbo.at", "Modded EU (MEU)", 443, true));
        // regionInfos.Add(CreateHttp("35.247.251.253", "Modded SA (MSA)", 22023, false));
        regionInfos.Where(x => !ServerManager.AvailableRegions.Contains(x)).Do(ServerManager.AddOrUpdateRegion);

        ServerManager.AvailableRegions = ServerManager.AvailableRegions.OrderByDescending(ServerManager.DefaultRegions.Contains).ToArray();
    }

    private static IRegionInfo CreateHttp(string ip, string name, ushort port, bool ishttps)
    {
        string serverIp = (ishttps ? "https://" : "http://") + ip;
        ServerInfo serverInfo = new(name, serverIp, port, false);
        ServerInfo[] ServerInfo = [serverInfo];
        return new StaticHttpRegionInfo(name, (StringNames)1003, ip, ServerInfo).CastFast<IRegionInfo>();
    }

    private static T CastFast<T>(this Il2CppObjectBase obj) where T : Il2CppObjectBase
    {
        if (obj is T casted) return casted;
        return CastHelper<T>.Cast(obj.Pointer);
    }

    private static class CastHelper<T> where T : Il2CppObjectBase
    {
        public static readonly Func<IntPtr, T> Cast;

        static CastHelper()
        {
            var constructor = typeof(T).GetConstructor([typeof(IntPtr)]);
            var ptr = Expression.Parameter(typeof(IntPtr));
            var create = Expression.New(constructor!, ptr);
            var lambda = Expression.Lambda<Func<IntPtr, T>>(create, ptr);
            Cast = lambda.Compile();
        }
    }
}