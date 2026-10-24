using System;
using Microsoft.Extensions.Logging;
using Moq;
using PostXML.Services;
using Xunit;

namespace PostXMLTest.Services
{
    public class BankDetectorServiceTest
    {
        private readonly Mock<ILogger<BankDetectorService>> _mockLogger;
        private readonly BankDetectorService _detector;

        public BankDetectorServiceTest()
        {
            _mockLogger = new Mock<ILogger<BankDetectorService>>();
            _detector = new BankDetectorService(_mockLogger.Object);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void DetectBank_NullOrWhiteSpace_ReturnsGenericProfile(string input)
        {
            var profile = _detector.DetectBank(input);

            Assert.NotNull(profile);
            Assert.Equal("GENERIC", profile.BankCode);
            Assert.Equal("Standard Bank Statement", profile.DisplayName);
        }

        [Fact]
        public void DetectBank_HdfcSignatures_ReturnsHdfcProfile()
        {
            string sample = "Welcome to HDFC BANK\nRTGS/NEFT IFSC: HDFC0000001\nAccount Branch: Mumbai";

            var profile = _detector.DetectBank(sample);

            Assert.NotNull(profile);
            Assert.Equal("HDFC", profile.BankCode);
            Assert.Equal("HDFC Bank", profile.DisplayName);
            Assert.Equal("HDFC", profile.ExpectedIfscPrefix);
            Assert.Equal("dd/MM/yyyy", profile.DateFormat);
        }

        [Fact]
        public void DetectBank_SbiSignatures_ReturnsSbiProfile()
        {
            string sample = "STATE BANK OF INDIA\nTxn Date: 12 Jan 2024\nIFS Code: SBIN0001234";

            var profile = _detector.DetectBank(sample);

            Assert.NotNull(profile);
            Assert.Equal("SBI", profile.BankCode);
            Assert.Equal("State Bank of India", profile.DisplayName);
            Assert.Equal("SBIN", profile.ExpectedIfscPrefix);
        }

        [Fact]
        public void DetectBank_IciciSignatures_ReturnsIciciProfile()
        {
            string sample = "ICICI BANK Statement\nTransaction Remarks: UPI/123\nCheque No: 000123";

            var profile = _detector.DetectBank(sample);

            Assert.NotNull(profile);
            Assert.Equal("ICICI", profile.BankCode);
            Assert.Equal("ICICI Bank", profile.DisplayName);
        }

        [Fact]
        public void DetectBank_AxisSignatures_ReturnsAxisProfile()
        {
            string sample = "AXIS BANK Statement of Account\nTRANSACTION DETAILS for the month";

            var profile = _detector.DetectBank(sample);

            Assert.NotNull(profile);
            Assert.Equal("AXIS", profile.BankCode);
            Assert.Equal("Axis Bank", profile.DisplayName);
        }

        [Fact]
        public void DetectBank_KotakSignatures_ReturnsKotakProfile()
        {
            string sample = "KOTAK MAHINDRA BANK LIMITED\nAmount CR/DR";

            var profile = _detector.DetectBank(sample);

            Assert.NotNull(profile);
            Assert.Equal("KOTAK", profile.BankCode);
            Assert.Equal("Kotak Mahindra Bank", profile.DisplayName);
        }

        [Fact]
        public void DetectBank_UnknownBankWithBankWord_ReturnsCustomProfile()
        {
            string sample = "Some Unrecognized Rural Co-operative Bank\nAccount Statement\nOnly Date and Amount";

            var profile = _detector.DetectBank(sample);

            Assert.NotNull(profile);
            Assert.Equal("CUSTOM", profile.BankCode);
            Assert.Equal("Some Unrecognized Rural Co-operative Bank", profile.DisplayName);
        }

        [Fact]
        public void DetectBank_UnknownTextWithoutBankWord_ReturnsGenericProfile()
        {
            string sample = "Some Financial Ledger\nAccount Statement\nOnly Date and Amount";

            var profile = _detector.DetectBank(sample);

            Assert.NotNull(profile);
            Assert.Equal("GENERIC", profile.BankCode);
        }
    }
}
