using BacchusLedger.Domain.Readings;

namespace BacchusLedger.Domain.Tests;

public class FermentationTests
{
    [Fact]
    public void Potential_abv_of_22_brix_is_12_98() => Assert.Equal(12.98, new InitialBrix(22.0).PotentialAbv);

    [Fact]
    public void Brix_22_is_about_sg_1_0920() => Assert.Equal(1.0920, new InitialBrix(22.0).SpecificGravity, 3);

    [Fact]
    public void Progress_is_abv_reached_over_potential() => Assert.Equal(50.0, new InitialBrix(22.0).ProgressPct(6.49));

    [Fact]
    public void Hydrometer_abv_without_initial_brix_has_no_calculation() =>
        Assert.Equal(new ReadingCalculation(null, null, null, null), new ReadingCalculator().Calculate("hydrometer_abv", 5, null));

    [Theory]
    [InlineData("ph", 99, "ph must be between 0 and 14 pH")]
    [InlineData("bogus", 1, "Unknown reading type")]
    [InlineData("temperature", 21, null)]
    public void Validator_refuses_impossible_values(string type, double value, string? error) =>
        Assert.Equal(error, new ReadingValidator().Validate(new ReadingInput(1, null, "2025-09-25T10:00", type, value, null, null, null, "Entered")));
}
