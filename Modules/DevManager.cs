using System.Collections.Generic;
using System.Linq;

namespace EHR;

public class DevUser(string code = "", string color = "null", string tag = "null", bool isUp = false, bool isDev = false, bool deBug = false, bool colorCmd = false, string upName = "Unknown")
{
    public string Code { get; set; } = code;
    public string Color { get; set; } = color;
    public string Tag { get; set; } = tag;
    public bool IsUp { get; set; } = isUp;
    public bool IsDev { get; set; } = isDev;
    public bool DeBug { get; set; } = deBug;
    public bool ColorCmd { get; set; } = colorCmd;
    public string UpName { get; set; } = upName;

    public bool HasTag() => Tag != "null";
    public string GetTag() => Color == "null" ? $"<size=1.4>{Tag}</size>\r\n" : $"<color={Color}><size=1.4>{(Tag == "#Dev" ? Translator.GetString("Developer") : Tag)}</size></color>\r\n";
}

public static class DevManager
{
    public static DevUser DefaultDevUser = new();
    public static List<DevUser> DevUserList = [];

    public static void Init()
    {
        DevUserList =
        [
            // Dev
            new(code: "actorour#0029", color: "#ffc0cb", tag: "TOHE/TONX Developer", isUp: true, isDev: true, deBug: true, colorCmd: true, upName: "KARPED1EM"),
            new(code: "pinklaze#1776", color: "#30548e", tag: "#Dev", isUp: true, isDev: true, deBug: true, colorCmd: false, upName: "NCSIMON"),
            new(code: "keepchirpy#6354", color: "#1FF3C6", tag: "Переводчик", isUp: true, isDev: false, deBug: false, colorCmd: true, upName: "TommyXL"), //Tommy-XL
            new(code: "taskunsold#2701", color: "null", tag: "<color=#426798>Tem</color><color=#f6e509>mie</color>", isUp: false, isDev: true, deBug: false, colorCmd: false, upName: null), //Tem
            new(code: "timedapper#9496", color: "#48FFFF", tag: "#Dev", isUp: false, isDev: true, deBug: false, colorCmd: false, upName: null), //阿龍
            new(code: "sofaagile#3120", color: "null", tag: "null", isUp: false, isDev: true, deBug: true, colorCmd: false, upName: null), //天寸
            new(code: "keyscreech#2151", color: "null", tag: "<color=#D3A4FF>美術</color><color=#5A5AAD>NotKomi</color>", isUp: false, isDev: true, deBug: false, upName: null), //Endrmen40409

            // Up
            new(code: "primether#5348", color: "null", tag: "<color=#FF0000>YouTuber</color>/<color=#8800FF>Streamer</color>", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "AnonWorks"),
            new(code: "truantwarm#9165", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "萧暮不姓萧"),
            new(code: "drilldinky#1386", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "爱玩AU的河豚"),
            new(code: "farardour#6818", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "-提米SaMa-"),
            new(code: "vealused#8192", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "lag丶xy"),
            new(code: "storyeager#0815", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "航娜丽莎"),
            new(code: "versegame#3885", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "柴唔cw"),
            new(code: "closegrub#6217", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "警长不会玩"),
            new(code: "frownnatty#7935", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "鬼灵official"),
            new(code: "veryscarf#5368", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "小武同学102"),
            new(code: "sparklybee#0275", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "--红包SaMa--"),
            new(code: "endingyon#3175", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "游侠开摆"),
            new(code: "firmine#0232", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "YH永恒_"),
            new(code: "storkfey#3570", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Calypso"),
            new(code: "fellowsand#1003", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "C-Faust"),
            new(code: "jetsafe#8512", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Hoream是好人"),
            new(code: "primether#5348", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "AnonWorks"),
            new(code: "spoonkey#0792", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "没好康的"),
            new(code: "beakedmire#6099", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "茄-au"),
            new(code: "doggedsize#7892", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "TronAndRey"),
            new(code: "openlanded#9533", color: "#9e2424", tag: "God Of Death Love Apples", isUp: true, isDev: true, deBug: true, colorCmd: true, upName: "ryuk"),
            new(code: "unlikecity#4086", color: "#eD2F91", tag: "Ward", isUp: true, isDev: false, deBug: false, colorCmd: true, upName: "Ward"),
            new(code: "iconicdrop#2727", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: true, upName: "jackler"),


            new(code: "goneria#8334", color: "#FFFF00", tag: "The Dev on Phone", isUp: true, isDev: true, deBug: true, colorCmd: true, upName: "[Developer] The 200IQ guy"), // Me on phone
            new(code: "neatnet#5851", color: "#FFFF00", tag: "[Developer] The 200IQ guy", isUp: true, isDev: true, deBug: true, colorCmd: true, upName: "[Developer] The 200IQ guy"),
            new(code: "contenthue#0404", color: "#FFFF00", tag: "[Developer] The 200IQ guy", isUp: true, isDev: true, deBug: true, colorCmd: true, upName: "[Developer] The 200IQ guy"),
            new(code: "theseform#5686", color: "null", tag: "<color=#7800FF>PTBR-Translator</color>/<color=#FF0000>Role Idea Creator</color>", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Role idea creator"),
            new(code: "heavyclod#2286", color: "#FFFF00", tag: "小叨.exe已停止运行", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "小叨院长"),
            new(code: "storeroan#0331", color: "#FF0066", tag: "Night_瓜", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Night_瓜"),
            new(code: "teamelder#5856", color: "#1379bf", tag: "屑Slok（没信誉的鸽子）", isUp: true, isDev: false, colorCmd: false, deBug: false, upName: "Slok7565"),
            new(code: "everyspam#5105", color: "#fc3a51", tag: "YouTuber", isUp: true, isDev: false, colorCmd: false, deBug: false, upName: "Henry Malfy"),

            new(code: "radarright#2509", color: "null", tag: "null", isUp: false, isDev: false, deBug: true, colorCmd: false, upName: null),

            // EHR players
            new(code: "ravenknurl#4562", color: "#008000", tag: "Moderador do FH", isUp: false, isDev: false, deBug: false, colorCmd: false, upName: "RicardoFumante"),
            new(code: "crustzonal#9589", color: "#00FFFF", tag: "Translator PT-BR", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "artyleague01"),
            new(code: "tinedpun#6584", color: "#0000ff", tag: "Translator PT-BR", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Dechis"),
            new(code: "swiftlord#8072", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "But What About"),
            new(code: "ovalinstep#2984", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Seleneous"),
            new(code: "seleneous#6930", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Seleneous"),
            new(code: "innerruler#4140", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "thewhiskas27"),
            new(code: "urbanecalf#4975", color: "#420773", tag: "Streamer", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Gugutsik"),
            new(code: "crustzonal#9589", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "aviiiv0102"),
            new(code: "tubedilute#9062", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Xcl4udioX"),
            new(code: "ponyholey#5532", color: "#0000FF", tag: "desenvolvedor", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "DrawingsZz"),
            new(code: "akinlaptop#2206", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "MR Carr"),
            new(code: "fursilty#4676", color: "#0000ff", tag: "arthurzin", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "arthurzin"),
            new(code: "stonefuzzy#8673", color: "#ff0062", tag: "<size=1.6>Ru Translator</size>", isUp: true, isDev: true, deBug: true, colorCmd: true, upName: "HyperAtill"),
            new(code: "frizzytram#2508", color: "#1C87FF", tag: "RafaelBIT50", isUp: false, isDev: false, deBug: false, colorCmd: false, upName: "RafaelBIT50"),
            new(code: "foggyzing#6238", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "LdZinnn"),
            new(code: "onsideblur#3929", color: "#fc3a51", tag: "YouTuber", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Manelzin"),
            new(code: "modestspan#7071", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Shark"),
            new(code: "divotbusy#0624", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "PreCeptorBR"),
            new(code: "pinsrustic#5496", color: "#fc3a51", tag: "YouTuber", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "FHgameplay"),
            new(code: "opaquedot#5610", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Deh_66"),
            new(code: "furpolitic#3380", color: "#a020f0", tag: "EuOncologico o impostor", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "EuOncologico"),
            new(code: "pithfierce#5073", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "OrigeTv"),
            new(code: "cannylink#0564", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "SpicyPoops"),
            new(code: "ghostapt#7243", color: "#a48d6b", tag: "AUME", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "MasterKy"),
            new(code: "planegame#5847", color: "#44fff7", tag: "Kopp56", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Kopp56 PRO"),
            new(code: "clovesorry#6973", color: "#191970", tag: "Master", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "MAT"),
            new(code: "ivorywish#3580", color: "#ff0000", tag: "YouTuber", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "Erik Carr"),
            new(code: "cleardress#6310", color: "#ffffff", tag: "<#00BFFF>一</color><#48D1CC>个</color><#7CFC00>热狗</color><#32CD32>uwu</color>", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "ABoringCat"),
            new(code: "rainypearl#9545", color: "#A020F0", tag: "roxo", isUp: true, isDev: false, deBug: false, colorCmd: false, upName: "bielModzs"),

            // Sponsor
            new(code: "recentduct#6068", color: "#FF00FF", tag: "高冷男模法师", isUp: false, isDev: false, colorCmd: false, deBug: true, upName: null),
            new(code: "canneddrum#2370", color: "#fffcbe", tag: "我是喜唉awa", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null),
            new(code: "dovefitted#5329", color: "#1379bf", tag: "不要首刀我", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null),
            new(code: "luckylogo#7352", color: "#f30000", tag: "林@林", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null),
            new(code: "axefitful#8788", color: "#8e8171", tag: "寄才是真理", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null),
            new(code: "raftzonal#8893", color: "#8e8171", tag: "寄才是真理", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null),
            new(code: "twainrobin#8089", color: "#0000FF", tag: "啊哈修maker", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null),
            new(code: "mallcasual#6075", color: "#f89ccb", tag: "波奇酱", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null),
            new(code: "beamelfin#9478", color: "#6495ED", tag: "Amaster-1111", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null),
            new(code: "lordcosy#8966", color: "#FFD6EC", tag: "HostEHR", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null), //K
            new(code: "honestsofa#2870", color: "#D381D9", tag: "Discord: SolarFlare#0700", isUp: true, isDev: false, colorCmd: false, deBug: false, upName: "SolarFlare"), //SolarFlare
            new(code: "caseeast#7194", color: "#1c2451", tag: "disc.gg/maul", isUp: false, isDev: false, colorCmd: false, deBug: false, upName: null), //laikrai
            // lol hi go away
            //new(code: "gnuedaphic#7196", color: "#ffc0cb", tag: "TOH-RE Developer", isUp: true, isDev: false, deBug: false, colorCmd: true, upName: "Loonie"), //Loonie
            // Lauryn and Moe
            new(code: "straymovie#6453", color: "#F6B05E", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: true, upName: "Moe"), //Moe
            new(code: "singlesign#1823", color: "#ffb6cd", tag: "Princess", isUp: true, isDev: false, deBug: false, colorCmd: true, upName: "Lauryn"), //Lauryn
            // Other
            new(code: "peakcrown#8292", color: "null", tag: "null", isUp: true, isDev: false, deBug: false, colorCmd: true, upName: null), //Hakaka
        ];
    }

    public static bool IsDevUser(this string code) => DevUserList.Any(x => x.Code == code);
    public static DevUser GetDevUser(this string code) => code.IsDevUser() ? DevUserList.Find(x => x.Code == code) : DefaultDevUser;
}