using System.Collections.Generic;
using System.Linq;

namespace EHR;

public class DevUser(string code = "", string color = "null", string tag = "null", bool isUp = false, bool deBug = false)
{
    public string Code { get; } = code;
    private string Color { get; } = color;
    private string Tag { get; } = tag;
    public bool IsUp { get; } = isUp;
    public bool DeBug { get; } = deBug;

    public bool HasTag() => Tag != "null";
    public string GetTag() => Color == "null" ? $"<size=1.4>{Tag}</size>\r\n" : $"<color={Color}><size=1.4>{(Tag == "#Dev" ? Translator.GetString("Developer") : Tag)}</size></color>\r\n";
}

public static class DevManager
{
    private static readonly DevUser DefaultDevUser = new();
    private static List<DevUser> DevUserList = [];

    public static void Init()
    {
        DevUserList =
        [
            // Dev
            new(code: "actorour#0029", color: "#ffc0cb", tag: "TOHE/TONX Developer", isUp: true, deBug: true), // KARPED1EM
            new(code: "pinklaze#1776", color: null, tag: "null", isUp: true, deBug: true), // NCSIMON
            new(code: "keepchirpy#6354", color: "#1FF3C6", tag: "TOHE Developer", isUp: true, deBug: false), // TommyXL //Tommy-XL
            new(code: "motelchief#4112", color: "#eb57af", tag: "<alpha=#CC>Twilight Raven<alpha=#FF>", isUp: true, deBug: true), // Drakos //Drakos
            new(code: "eagergaol#1562", color: "#5534eb", tag: "Drakos Alt", isUp: true, deBug: true), // Drakos //Drakos Steam Alt
            new(code: "timedapper#9496", color: null, tag: null, isUp: false, deBug: false), //阿龍
            new(code: "sofaagile#3120", color: "null", tag: "null", isUp: false, deBug: true), //天寸
            new(code: "keyscreech#2151", color: "null", tag: null, isUp: false, deBug: false), //Endrmen40409

            // Up
            new(code: "primether#5348", color: "null", tag: "<color=#FF0000>YouTuber</color>/<color=#8800FF>Streamer</color>", isUp: true, deBug: false), // AnonWorks
            new(code: "truantwarm#9165", color: "null", tag: "null", isUp: true, deBug: false), // 萧暮不姓萧
            new(code: "drilldinky#1386", color: "null", tag: "null", isUp: true, deBug: false), // 爱玩AU的河豚
            new(code: "farardour#6818", color: "null", tag: "null", isUp: true, deBug: false), // -提米SaMa-
            new(code: "vealused#8192", color: "null", tag: "null", isUp: true, deBug: false), // lag丶xy
            new(code: "storyeager#0815", color: "null", tag: "null", isUp: true, deBug: false), // 航娜丽莎
            new(code: "versegame#3885", color: "null", tag: "null", isUp: true, deBug: false), // 柴唔cw
            new(code: "closegrub#6217", color: "null", tag: "null", isUp: true, deBug: false), // 警长不会玩
            new(code: "frownnatty#7935", color: "null", tag: "null", isUp: true, deBug: false), // 鬼灵official
            new(code: "veryscarf#5368", color: "null", tag: "null", isUp: true, deBug: false), // 小武同学102
            new(code: "sparklybee#0275", color: "null", tag: "null", isUp: true, deBug: false), // --红包SaMa--
            new(code: "endingyon#3175", color: "null", tag: "null", isUp: true, deBug: false), // 游侠开摆
            new(code: "firmine#0232", color: "null", tag: "null", isUp: true, deBug: false), // YH永恒_
            new(code: "storkfey#3570", color: "null", tag: "null", isUp: true, deBug: false), // Calypso
            new(code: "fellowsand#1003", color: "null", tag: "null", isUp: true, deBug: false), // C-Faust
            new(code: "jetsafe#8512", color: "null", tag: "null", isUp: true, deBug: false), // Hoream是好人
            new(code: "spoonkey#0792", color: "null", tag: "null", isUp: true, deBug: false), // 没好康的
            new(code: "beakedmire#6099", color: "null", tag: "null", isUp: true, deBug: false), // 茄-au
            new(code: "doggedsize#7892", color: "null", tag: "null", isUp: true, deBug: false), // TronAndRey
            new(code: "openlanded#9533", color: "#9e2424", tag: "God Of Death Love Apples", isUp: true, deBug: true), // ryuk
            new(code: "unlikecity#4086", color: "#eD2F91", tag: "Ward", isUp: true, deBug: false), // Ward
            new(code: "iconicdrop#2727", color: "null", tag: "null", isUp: true, deBug: false), // jackler


            new(code: "goneria#8334", color: "#FFFF00", tag: "The Dev on Phone", isUp: true, deBug: true), // [Developer] The 200IQ guy // Me on phone
            new(code: "neatnet#5851", color: "#FFFF00", tag: "[Developer] The 200IQ guy", isUp: true, deBug: true), // [Developer] The 200IQ guy
            new(code: "contenthue#0404", color: "#FFFF00", tag: "[Developer] The 200IQ guy", isUp: true, deBug: true), // [Developer] The 200IQ guy
            new(code: "theseform#5686", color: "null", tag: "<color=#7800FF>PTBR-Translator</color>/<color=#FF0000>Role Idea Creator</color>", isUp: true, deBug: false), // Tomix
            new(code: "heavyclod#2286", color: "#FFFF00", tag: "小叨.exe已停止运行", isUp: true, deBug: false), // 小叨院长
            new(code: "storeroan#0331", color: "#FF0066", tag: "Night_瓜", isUp: true, deBug: false), // Night_瓜
            new(code: "teamelder#5856", color: "#1379bf", tag: "屑Slok（没信誉的鸽子）", isUp: true, deBug: false), // Slok7565

            new(code: "radarright#2509", color: "null", tag: "null", isUp: false, deBug: true),

            // EHR players
            new(code: "ravenknurl#4562", color: "#008000", tag: "Moderador do FH", isUp: false, deBug: false), // RicardoFumante
            new(code: "crustzonal#9589", color: "#00FFFF", tag: "Translator PT-BR", isUp: true, deBug: false), // artyleague01
            new(code: "tinedpun#6584", color: "#0000ff", tag: "Translator PT-BR", isUp: true, deBug: false), // Dechis
            new(code: "swiftlord#8072", color: "null", tag: "null", isUp: true, deBug: false), // But What About
            new(code: "ovalinstep#2984", color: "null", tag: "null", isUp: true, deBug: false), // Seleneous
            new(code: "seleneous#6930", color: "null", tag: "null", isUp: true, deBug: false), // Seleneous
            new(code: "innerruler#4140", color: "null", tag: "null", isUp: true, deBug: false), // thewhiskas27
            new(code: "urbanecalf#4975", color: "#420773", tag: "Streamer", isUp: true, deBug: false), // Gugutsik
            new(code: "crustzonal#9589", color: "null", tag: "null", isUp: true, deBug: false), // aviiiv0102
            new(code: "tubedilute#9062", color: "null", tag: "null", isUp: true, deBug: false), // Xcl4udioX
            new(code: "ponyholey#5532", color: "#0000FF", tag: "desenvolvedor", isUp: true, deBug: false), // DrawingsZz
            new(code: "akinlaptop#2206", color: "null", tag: "null", isUp: true, deBug: false), // MR Carr
            new(code: "fursilty#4676", color: "#0000ff", tag: "arthurzin", isUp: true, deBug: false), // arthurzin
            new(code: "stonefuzzy#8673", color: "#ff0062", tag: "<size=1.6>Ru Translator</size>", isUp: true, deBug: true), // HyperAtill
            new(code: "frizzytram#2508", color: "#1C87FF", tag: "RafaelBIT50", isUp: false, deBug: false), // RafaelBIT50
            new(code: "ruefulscar#0287", color: "null", tag: "null", isUp: true, deBug: false), // Zendena
            new(code: "foggyzing#6238", color: "null", tag: "null", isUp: true, deBug: false), // LdZinnn
            new(code: "onsideblur#3929", color: "#fc3a51", tag: "YouTuber", isUp: true, deBug: false), // Manelzin
            new(code: "modestspan#7071", color: "null", tag: "null", isUp: true, deBug: false), // Shark
            new(code: "divotbusy#0624", color: "null", tag: "null", isUp: true, deBug: false), // PreCeptorBR
            new(code: "pinsrustic#5496", color: "#fc3a51", tag: "YouTuber", isUp: true, deBug: false), // FHgameplay
            new(code: "opaquedot#5610", color: "null", tag: "null", isUp: true, deBug: false), // Deh_66
            new(code: "furpolitic#3380", color: "#a020f0", tag: "EuOncologico o impostor", isUp: true, deBug: false), // EuOncologico
            new(code: "pithfierce#5073", color: "null", tag: "null", isUp: true, deBug: false), // OrigeTv
            new(code: "cannylink#0564", color: "null", tag: "null", isUp: true, deBug: false), // SpicyPoops
            new(code: "ghostapt#7243", color: "#a48d6b", tag: "AUME", isUp: true, deBug: false), // MasterKy
            new(code: "planegame#5847", color: "#44fff7", tag: "Kopp56", isUp: true, deBug: false), // Kopp56 PRO
            new(code: "clovesorry#6973", color: "#191970", tag: "Master", isUp: true, deBug: false), // MAT
            new(code: "ivorywish#3580", color: "#ff0000", tag: "YouTuber", isUp: true, deBug: false), // Erik Carr
            new(code: "cleardress#6310", color: "#ffffff", tag: "<#00BFFF>一</color><#48D1CC>个</color><#7CFC00>热狗</color><#32CD32>uwu</color>", isUp: true, deBug: false), // ABoringCat
            new(code: "rainypearl#9545", color: "#A020F0", tag: "YouTuber", isUp: true, deBug: false), // bielModzs
            new(code: "kiltedbill#4145", color: "null", tag: "null", isUp: true, deBug: false), // tsaki84
            new(code: "onlyfax#3941", color: "#ff0000", tag: "YouTuber", isUp: true, deBug: false), // The Nick AG
            new(code: "somewallet#5521", color: "#ffff00", tag: "Artist", isUp: true, deBug: false), // YoshiBertil
            new(code: "pagersane#4064", color: "null", tag: "null", isUp: true, deBug: false), // Bluejava3
            new(code: "pocketdoor#9080", color: "null", tag: "null", isUp: true, deBug: false), // Imnot
            new(code: "motorstack#2287", color: "#E34234", tag: "Foxfire", isUp: true, deBug: false), // Esrazraft
            new(code: "motorlace#4741", color: "#DFB722", tag: "\u2756 Assistant Tester \u2756", isUp: true, deBug: false), // PEPPERcula

            // Sponsor
            new(code: "recentduct#6068", color: "#FF00FF", tag: "高冷男模法师", isUp: false, deBug: true),
            new(code: "canneddrum#2370", color: "#fffcbe", tag: "我是喜唉awa", isUp: false, deBug: false),
            new(code: "dovefitted#5329", color: "#1379bf", tag: "不要首刀我", isUp: false, deBug: false),
            new(code: "luckylogo#7352", color: "#f30000", tag: "林@林", isUp: false, deBug: false),
            new(code: "axefitful#8788", color: "#8e8171", tag: "寄才是真理", isUp: false, deBug: false),
            new(code: "raftzonal#8893", color: "#8e8171", tag: "寄才是真理", isUp: false, deBug: false),
            new(code: "twainrobin#8089", color: "#0000FF", tag: "啊哈修maker", isUp: false, deBug: false),
            new(code: "mallcasual#6075", color: "#f89ccb", tag: "波奇酱", isUp: false, deBug: false),
            new(code: "beamelfin#9478", color: "#6495ED", tag: "Amaster-1111", isUp: false, deBug: false),
            new(code: "lordcosy#8966", color: "#FFD6EC", tag: "HostEHR", isUp: false, deBug: false), // K
            new(code: "honestsofa#2870", color: "#D381D9", tag: "Discord: SolarFlare#0700", isUp: true, deBug: false), // SolarFlare //SolarFlare
            new(code: "caseeast#7194", color: "#1c2451", tag: "disc.gg/maul", isUp: false, deBug: false), // laikrai
            // Other
            new(code: "peakcrown#8292", color: "null", tag: "null", isUp: true, deBug: false) // Hakaka
        ];
    }

    private static bool IsDevUser(this string code) => DevUserList.Any(x => x.Code == code);
    public static DevUser GetDevUser(this string code) => code.IsDevUser() ? DevUserList.Find(x => x.Code == code) : DefaultDevUser;
}