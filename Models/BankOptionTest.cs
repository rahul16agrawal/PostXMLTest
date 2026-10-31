using FluentAssertions;
using PostXML.Models;
using Xunit;

namespace PostXMLTest.Models
{
    public class BankOptionTest
    {
        [Fact]
        public void Constructor_Default_InitializesEmptyStrings()
        {
            var option = new BankOption();

            option.BankCode.Should().BeEmpty();
            option.DisplayName.Should().BeEmpty();
            option.ToString().Should().BeEmpty();
        }

        [Fact]
        public void Constructor_Parameterized_SetsBankCodeAndDisplayName()
        {
            var option = new BankOption("HDFC", "HDFC Bank");

            option.BankCode.Should().Be("HDFC");
            option.DisplayName.Should().Be("HDFC Bank");
            option.ToString().Should().Be("HDFC Bank");
        }

        [Fact]
        public void ToString_ReturnsDisplayName()
        {
            var option = new BankOption("AXIS", "Axis Bank (Retail & Corporate)");

            option.ToString().Should().Be("Axis Bank (Retail & Corporate)");
        }
    }
}
