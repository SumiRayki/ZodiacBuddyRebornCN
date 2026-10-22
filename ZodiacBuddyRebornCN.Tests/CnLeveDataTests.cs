using Lumina.Data;
using Lumina.Excel.Sheets;
using Xunit;

namespace ZodiacBuddyRebornCN.Tests;

/// <summary>
/// Locks the Chinese-client rows used by the Animus leve issuer fix.
/// This file is compiled only when DALAMUD_HOME and FFXIV_SQPACK are both set.
/// </summary>
public class CnLeveDataTests
{
    private static readonly (uint LeveId, uint IssuerDataId, string IssuerName, uint TerritoryId)[] Issuers =
    [
        (643, 1002398, "鲁鲁巴纳", 147),
        (644, 1002398, "鲁鲁巴纳", 147),
        (645, 1002398, "鲁鲁巴纳", 147),
        (646, 1002398, "鲁鲁巴纳", 147),
        (647, 1002398, "鲁鲁巴纳", 147),
        (649, 1002401, "瓦力诺", 155),
        (650, 1002401, "瓦力诺", 155),
        (652, 1002401, "瓦力诺", 155),
        (657, 1004348, "克·蕾塔伊", 156),
        (658, 1004348, "克·蕾塔伊", 156),
        (659, 1004348, "克·蕾塔伊", 156),
        (848, 1007069, "洛蒂耶", 155),
        (849, 1007069, "洛蒂耶", 155),
        (853, 1007069, "洛蒂耶", 155),
        (855, 1007069, "洛蒂耶", 155),
        (859, 1007069, "洛蒂耶", 155),
        (860, 1007069, "洛蒂耶", 155),
        (863, 1007070, "艾伊德哈特", 156),
        (865, 1007070, "艾伊德哈特", 156),
        (868, 1007070, "艾伊德哈特", 156),
        (870, 1007070, "艾伊德哈特", 156),
        (873, 1007070, "艾伊德哈特", 156),
        (875, 1007070, "艾伊德哈特", 156),
    ];

    [Fact]
    public void CatalogIssuers_MatchChineseLevelAndResidentRows()
    {
        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Assert.False(string.IsNullOrWhiteSpace(sqpack));

        var data = new Lumina.GameData(sqpack!, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Language.ChineseSimplified,
        });
        var leves = data.Excel.GetSheet<Leve>(Language.ChineseSimplified);
        var residents = data.Excel.GetSheet<ENpcResident>(Language.ChineseSimplified);

        foreach (var expected in Issuers)
        {
            var leve = leves.GetRow(expected.LeveId);
            var level = leve.LevelLevemete.Value;
            Assert.Equal(8, level.Type);
            Assert.Equal(expected.IssuerDataId, level.Object.RowId);
            Assert.Equal(expected.TerritoryId, level.Territory.RowId);

            var resident = residents.GetRow(expected.IssuerDataId);
            Assert.Equal(expected.IssuerName, resident.Singular.ExtractText());
        }
    }

    [Fact]
    public void Leve868_UsesTwinAdderLabel_WhileTheNpcTitleIsImmortalFlames()
    {
        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK")!;
        var data = new Lumina.GameData(sqpack, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Language.ChineseSimplified,
        });
        var leve = data.Excel.GetSheet<Leve>(Language.ChineseSimplified).GetRow(868);
        var resident = data.Excel.GetSheet<ENpcResident>(Language.ChineseSimplified).GetRow(1007070);

        Assert.Equal("防卫指令：阿格里俄斯号的研究任务", leve.Name.ExtractText());
        Assert.Equal(14u, leve.LeveAssignmentType.RowId);
        Assert.Equal("双蛇党", leve.LeveAssignmentType.Value.Name.ExtractText());
        Assert.Equal("艾伊德哈特", resident.Singular.ExtractText());
        Assert.Equal("恒辉队耀士长", resident.Title.ExtractText());
    }

    [Fact]
    public void BattlecraftAssignmentLabel_IsMercenaryWork()
    {
        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK")!;
        var data = new Lumina.GameData(sqpack, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Language.ChineseSimplified,
        });
        var leve = data.Excel.GetSheet<Leve>(Language.ChineseSimplified).GetRow(643);
        Assert.Equal(1u, leve.LeveAssignmentType.RowId);
        Assert.Equal("佣兵作业", leve.LeveAssignmentType.Value.Name.ExtractText());
    }
}
