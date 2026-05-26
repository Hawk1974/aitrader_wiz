namespace AiTrader.Wiz.Core;

public static class CashAllocationCalculator
{
    public static decimal CalculateDollarValue(AllocationInput input, decimal basis)
    {
        if (basis <= 0m)
        {
            return input.Mode == AllocationEntryMode.Dollar ? input.Value : 0m;
        }

        return input.Mode switch
        {
            AllocationEntryMode.Percent => Math.Round(basis * (input.Value / 100m), 2, MidpointRounding.AwayFromZero),
            AllocationEntryMode.Dollar => Math.Round(input.Value, 2, MidpointRounding.AwayFromZero),
            _ => 0m,
        };
    }

    public static decimal CalculatePercentValue(AllocationInput input, decimal basis)
    {
        if (basis <= 0m)
        {
            return input.Mode == AllocationEntryMode.Percent ? input.Value : 0m;
        }

        return input.Mode switch
        {
            AllocationEntryMode.Percent => Math.Round(input.Value, 4, MidpointRounding.AwayFromZero),
            AllocationEntryMode.Dollar => Math.Round((input.Value / basis) * 100m, 4, MidpointRounding.AwayFromZero),
            _ => 0m,
        };
    }

    public static string SanitizeNumericInput(string? input, bool allowDecimal)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var seenDecimal = false;
        var chars = input.Where(ch =>
        {
            if (char.IsDigit(ch))
            {
                return true;
            }

            if (allowDecimal && ch == '.' && !seenDecimal)
            {
                seenDecimal = true;
                return true;
            }

            return false;
        });

        return new string(chars.ToArray());
    }
}
