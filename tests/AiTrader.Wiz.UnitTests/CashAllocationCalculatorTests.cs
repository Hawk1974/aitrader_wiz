using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.UnitTests;

public sealed class CashAllocationCalculatorTests
{
    [Fact]
    public void CalculateDollarValue_FromPercent_UsesBasis()
    {
        var input = new AllocationInput
        {
            Mode = AllocationEntryMode.Percent,
            Value = 10m,
        };

        var result = CashAllocationCalculator.CalculateDollarValue(input, 25000m);

        Assert.Equal(2500m, result);
    }

    [Fact]
    public void CalculatePercentValue_FromDollar_UsesBasis()
    {
        var input = new AllocationInput
        {
            Mode = AllocationEntryMode.Dollar,
            Value = 2500m,
        };

        var result = CashAllocationCalculator.CalculatePercentValue(input, 25000m);

        Assert.Equal(10m, result);
    }

    [Theory]
    [InlineData("25,000.55", "25000.55")]
    [InlineData("$2,500.00", "2500.00")]
    [InlineData("abc10.5def", "10.5")]
    public void SanitizeNumericInput_StripsNonNumericContent(string input, string expected)
    {
        var result = CashAllocationCalculator.SanitizeNumericInput(input, allowDecimal: true);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5x", "5")]
    [InlineData("1.5", "15")]
    public void SanitizeNumericInput_IntegerMode_StripsDecimalAndLetters(string input, string expected)
    {
        var result = CashAllocationCalculator.SanitizeNumericInput(input, allowDecimal: false);

        Assert.Equal(expected, result);
    }
}
