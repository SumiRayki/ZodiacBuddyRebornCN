using ZodiacBuddy.Systems;
using Xunit;

namespace ZodiacBuddyRebornCN.Tests;

public class ClientActorNameTests
{
    [Theory]
    [InlineData("prime location", "设置地点")]
    [InlineData("Destination", "目的地")]
    [InlineData("Destination", "指定地点")]
    [InlineData("Parchment", "奇怪的羊皮纸")]
    [InlineData("research document", "研究用的资料")]
    [InlineData("soldiers' effects", "追击部队的遗物")]
    [InlineData("airship wreckage", "飞空艇的残骸")]
    [InlineData("Mimas", "怒号的米玛斯")]
    [InlineData("frost aevis", "冰霜龙鸟")]
    [InlineData("Okeanos the Red", "红发的俄刻阿诺斯")]
    [InlineData("balor's bell", "巴罗尔之铃")]
    [InlineData("balor", "恶神之眼巴罗尔")]
    [InlineData("prime location", "调查地点")]
    [InlineData("Wary Merchant", "慎重的商人")]
    [InlineData("Storm Private", "黑涡团二等漩兵")]
    [InlineData("Kobold Toolbox", "地灵族的采矿工具")]
    [InlineData("Ixali Swiftbeak", "鸟人敏捷战士")]
    [InlineData("Airstone", "起风珠")]
    public void KnownEnglishKeys_MatchTheChineseNameplate(string englishKey, string nameplate)
    {
        Assert.True(ClientActorNames.Matches(nameplate, 0, englishKey));
    }

    [Fact]
    public void PrimeLocation_DoesNotUseTheEnglishNameplate()
    {
        Assert.False(ClientActorNames.Matches("prime location", 0, "prime location"));
        Assert.False(ClientActorNames.Matches("Destination", 0, "Destination"));
    }

    [Fact]
    public void BattleNpc_MatchesByNameIdWhenTheNameplateDiffers()
    {
        Assert.True(ClientActorNames.Matches("unexpected", 1786, "Mimas"));
        Assert.False(ClientActorNames.Matches("怒号的米玛斯", 1786, "frost aevis"));
    }

    [Fact]
    public void Display_UsesTheClientName()
    {
        Assert.Equal("☓指定地点", ClientActorNames.Display("Destination"));
        Assert.Equal("指定地点", ClientActorNames.Display("prime location"));
        Assert.Equal("研究用的资料", ClientActorNames.Display("research document"));
    }
}
