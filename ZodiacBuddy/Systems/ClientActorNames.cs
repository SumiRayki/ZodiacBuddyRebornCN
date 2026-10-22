using System;
using System.Collections.Generic;

namespace ZodiacBuddy.Systems;

/// <summary>
/// Client-language names for actors the upstream profiles identify by English strings.
/// Row ids were joined from the English datamine to this CN client's sheets.
/// Eidhart row 1007070 aligned as 艾伊德哈特 before the join was trusted.
/// Battle NPC ids are matched directly. Event objects are matched by the client nameplate.
/// </summary>
internal static class ClientActorNames
{
    private readonly record struct ActorNames(string[] Nameplates, uint[] BattleNpcNameIds);

    private static readonly Dictionary<string, ActorNames> Actors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Destination"] = new(
            ["☓指定地点", "候补地点", "到达地点", "巡视地点", "张望四周", "指定地点", "梦羽城大门", "目标地点", "目的地", "调查港口周围", "调查烧焦的地面", "调查石塔周围", "调查道路"],
            []),
        ["Parchment"] = new(["奇怪的羊皮纸"], []),
        ["prime location"] = new(["指定地点", "设置地点", "调查地点"], []),
        ["Airstone"] = new(["起风珠"], [1860, 5267]),
        ["airship wreckage"] = new(["飞空艇的残骸"], [2243, 7401]),
        ["balor"] = new(["恶神之眼巴罗尔"], [1739]),
        ["balor's bell"] = new(["巴罗尔之铃"], [1738]),
        ["downcast hippocerf"] = new(["低沉骏雕"], [1762]),
        ["Foul River hapalit"] = new(["秽流哈帕利特"], [1770]),
        ["frost aevis"] = new(["冰霜龙鸟"], [2233]),
        ["House Haillenarte Guard"] = new(["艾因哈特家的卫兵"], [1603]),
        ["Ixali Swiftbeak"] = new(["鸟人敏捷战士"], [104]),
        ["Kobold Toolbox"] = new(["地灵族的采矿工具"], [1722]),
        ["Mianne Thousandmalm"] = new(["千里眼 米安娜"], [1729]),
        ["Mimas"] = new(["怒号的米玛斯"], [1786]),
        ["Mine Hound"] = new(["艾玛吉娜看门犬"], [1014]),
        ["Okeanos the Red"] = new(["红发的俄刻阿诺斯"], [1792]),
        ["ragged hippogryph"] = new(["凌乱骏鹰"], [1774]),
        ["research document"] = new(["研究用的资料"], [1795]),
        ["soldiers' effects"] = new(["追击部队的遗物"], [2242]),
        ["stegotaur"] = new(["利剑牛头魔"], [1760]),
        ["Storm Private"] = new(["魔器负责人", "黑涡团一等漩兵", "黑涡团二等漩兵", "黑涡团兵卒"], [1332]),
        ["Wary Merchant"] = new(["慎重的商人"], [1718]),
    };

    internal static bool Matches(string objectName, uint battleNpcNameId, string? englishKey)
    {
        if (string.IsNullOrWhiteSpace(englishKey))
            return false;
        if (!Actors.TryGetValue(englishKey, out var actor))
            return objectName.Equals(englishKey, StringComparison.OrdinalIgnoreCase);

        if (battleNpcNameId != 0)
        {
            foreach (var nameId in actor.BattleNpcNameIds)
            {
                if (nameId == battleNpcNameId)
                    return true;
            }
        }

        var nameplate = objectName.Trim();
        if (nameplate.Length == 0)
            return false;
        foreach (var candidate in actor.Nameplates)
        {
            if (nameplate.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static string Display(string? englishKey)
    {
        if (string.IsNullOrWhiteSpace(englishKey))
            return string.Empty;
        return Actors.TryGetValue(englishKey, out var actor) && actor.Nameplates.Length > 0
            ? actor.Nameplates[0]
            : englishKey;
    }

    internal static IEnumerable<string> Nameplates(string? englishKey)
    {
        if (string.IsNullOrWhiteSpace(englishKey))
            yield break;
        if (!Actors.TryGetValue(englishKey, out var actor))
        {
            yield return englishKey;
            yield break;
        }

        foreach (var nameplate in actor.Nameplates)
            yield return nameplate;
    }
}
