using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Tests;

public class StudentIdTests
{
    [Theory]
    [InlineData("20152691234567890", true)]
    [InlineData("00000000000000000", true)]
    [InlineData("2015269123456789", false)]
    [InlineData("201526912345678901", false)]
    [InlineData("2015269123456789a", false)]
    [InlineData("২০১৫২৬৯১২৩৪৫৬৭৮৯০", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validation_requires_exactly_17_ascii_digits(string? value, bool expected) =>
        Assert.Equal(expected, StudentId.IsValid(value));

    [Fact]
    public void Mask_keeps_only_the_last_four_digits()
    {
        Assert.Equal("*************7890", StudentId.Mask("20152691234567890"));
        Assert.Equal("****", StudentId.Mask("12"));
    }
}
