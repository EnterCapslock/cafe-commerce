using Cafe.Api.Services.Orders;
using Xunit;

namespace Cafe.Api.Tests.Unit.Orders;

public class OrderCodeTests
{
    [Theory]
    [InlineData("E18B681F", "e18b681f")]
    [InlineData("e18b681f", "e18b681f")]
    [InlineData("#E18B681F", "e18b681f")]
    [InlineData("  E18B 681F ", "e18b681f")]
    [InlineData("E18B-681F", "e18b681f")]
    public void A_code_is_accepted_however_it_is_typed(string typed, string expected)
    {
        Assert.Equal(expected, OrderCode.Normalize(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("E18B681")]       // too short
    [InlineData("E18B681FF")]     // too long
    [InlineData("ZZZZZZZZ")]      // not hexadecimal
    [InlineData("E18B681G")]
    [InlineData("e18b681f-2201-403b-85ef-5f2a713e4678")]   // a full id is not a code
    public void Anything_else_is_rejected(string? typed)
    {
        Assert.Null(OrderCode.Normalize(typed));
    }

    [Fact]
    public void The_code_is_the_first_eight_characters_of_the_id_in_capitals()
    {
        var id = Guid.Parse("e18b681f-2201-403b-85ef-5f2a713e4678");

        Assert.Equal("E18B681F", OrderCode.For(id));
    }
}