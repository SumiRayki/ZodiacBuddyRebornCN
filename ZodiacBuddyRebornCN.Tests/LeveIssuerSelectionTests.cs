using System.Numerics;
using Xunit;
using ZodiacBuddy.Stages.Animus.Leves;

namespace ZodiacBuddyRebornCN.Tests;

public class LeveIssuerSelectionTests
{
    private static readonly string[] CategoryLabels = ["佣兵作业", "黑涡团", "双蛇党", "恒辉队"];

    [Fact]
    public void Acquire_SelectsClientAssignmentLabel()
    {
        var entries = Entries((0, "恒辉队"), (1, "双蛇党"), (2, "黑涡团"), (3, "取消"));
        var index = LeveIssuerSelection.ChooseAcquire(14, "双蛇党", entries, out var detail);

        Assert.Equal(1, index);
        Assert.Contains("双蛇党", detail);
    }

    [Fact]
    public void Acquire_DoesNotClickTheFirstEntryWhenTheLabelIsMissing()
    {
        var index = LeveIssuerSelection.ChooseAcquire(14, "双蛇党", Entries((0, "随便看看"), (1, "取消")), out _);
        Assert.Null(index);
    }

    [Fact]
    public void Acquire_UsesGrandCompanyBridgeOnlyWhenTheSpecificLabelIsAbsent()
    {
        var index = LeveIssuerSelection.ChooseAcquire(14, "双蛇党", Entries((0, "军队理符"), (1, "取消")), out var detail);
        Assert.Equal(0, index);
        Assert.Contains("军队理符", detail);
    }

    [Fact]
    public void Acquire_PrefersTheSpecificLabelOverTheBridge()
    {
        var index = LeveIssuerSelection.ChooseAcquire(14, "双蛇党", Entries((0, "军队理符"), (1, "双蛇党")), out _);
        Assert.Equal(1, index);
    }

    [Fact]
    public void Acquire_BattlecraftUsesTheClientLabelBeforeTheGuildBridge()
    {
        Assert.Equal(0, LeveIssuerSelection.ChooseAcquire(1, "佣兵作业", Entries((0, "佣兵作业"), (1, "取消")), out _));
        Assert.Equal(0, LeveIssuerSelection.ChooseAcquire(1, "佣兵作业", Entries((0, "行会理符"), (1, "取消")), out _));
    }

    [Fact]
    public void TurnIn_RefusesAnAcquisitionCategory()
    {
        var index = LeveIssuerSelection.ChooseTurnIn(CategoryLabels, Entries((0, "双蛇党"), (1, "取消")), out _);
        Assert.Null(index);
    }

    [Fact]
    public void TurnIn_SelectsReportWhenPresent()
    {
        var index = LeveIssuerSelection.ChooseTurnIn(CategoryLabels, Entries((0, "双蛇党"), (1, "报告任务结果"), (2, "取消")), out _);
        Assert.Equal(1, index);
    }

    [Fact]
    public void TurnIn_SelectsANonCategoryFirstEntry()
    {
        var index = LeveIssuerSelection.ChooseTurnIn(CategoryLabels, Entries((0, "完成并领取报酬"), (1, "取消")), out var detail);
        Assert.Equal(0, index);
        Assert.Contains("完成并领取报酬", detail);
    }

    [Fact]
    public void Exit_RefusesACategoryInTheLastSlot()
    {
        var index = LeveIssuerSelection.ChooseExit(CategoryLabels, Entries((0, "取消"), (1, "双蛇党")), out _);
        Assert.Null(index);
    }

    [Fact]
    public void Exit_SelectsTheLastNonCategoryEntry()
    {
        var index = LeveIssuerSelection.ChooseExit(CategoryLabels, Entries((0, "双蛇党"), (1, "取消")), out _);
        Assert.Equal(1, index);
    }

    [Fact]
    public void Issuer_MatchesTheSheetDataIdNearItsSheetPosition()
    {
        var position = new Vector3(464.6f, -4.05f, -467.4f);
        var matched = LeveIssuerSelection.MatchesIssuer(Candidate(1007070, 1007070, position), 1007070, 156, position, out var distance);

        Assert.True(matched);
        Assert.True(distance < 1f);
    }

    [Fact]
    public void Issuer_RejectsACloserNpcWithTheWrongId()
    {
        var position = new Vector3(464.6f, -4.05f, -467.4f);
        Assert.False(LeveIssuerSelection.MatchesIssuer(Candidate(1004348, 1004348, position), 1007070, 156, position, out _));
    }

    [Fact]
    public void Issuer_AcceptsBaseIdWhenDataIdDoesNotMatch()
    {
        var position = Vector3.Zero;
        Assert.True(LeveIssuerSelection.MatchesIssuer(Candidate(1, 1007070, position), 1007070, 156, position, out _));
    }

    [Theory]
    [InlineData(155u, true, true, 0f, false)]
    [InlineData(156u, false, true, 0f, false)]
    [InlineData(156u, true, false, 0f, false)]
    [InlineData(156u, true, true, 21f, false)]
    public void Issuer_KeepsTerritoryTargetableKindAndDistanceChecks(uint territory, bool targetable, bool eventNpc, float offset, bool expected)
    {
        var position = new Vector3(offset, 0, 0);
        var candidate = new LeveIssuerSelection.Candidate(1007070, 1007070, territory, position, targetable, eventNpc);
        Assert.Equal(expected, LeveIssuerSelection.MatchesIssuer(candidate, 1007070, 156, Vector3.Zero, out _));
    }

    private static LeveIssuerSelection.MenuEntry[] Entries(params (int Index, string Text)[] entries)
        => entries.Select(entry => new LeveIssuerSelection.MenuEntry(entry.Index, entry.Text)).ToArray();

    private static LeveIssuerSelection.Candidate Candidate(uint dataId, uint baseId, Vector3 position)
        => new(dataId, baseId, 156, position, true, true);
}
