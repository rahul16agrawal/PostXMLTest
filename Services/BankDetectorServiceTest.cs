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

        [Theory]
        [InlineData("HDFC", "HDFC Bank")]
        [InlineData("hdfc", "HDFC Bank")]
        [InlineData("Hdfc", "HDFC Bank")]
        [InlineData("SBI", "State Bank of India")]
        [InlineData("sbi", "State Bank of India")]
        [InlineData("AXIS", "Axis Bank")]
        [InlineData("axis", "Axis Bank")]
        [InlineData("KOTAK", "Kotak Mahindra Bank")]
        [InlineData("kotak", "Kotak Mahindra Bank")]
        [InlineData("IDBI", "IDBI Bank")]
        [InlineData("IOB", "Indian Overseas Bank")]
        public void GetProfileByCode_WithValidCode_ReturnsMatchingProfileCaseInsensitively(string bankCode, string expectedDisplayName)
        {
            var profile = _detector.GetProfileByCode(bankCode);

            Assert.NotNull(profile);
            Assert.Equal(expectedDisplayName, profile!.DisplayName);
            Assert.Equal(bankCode, profile.BankCode, ignoreCase: true);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void GetProfileByCode_WithNullOrWhitespace_ReturnsNull(string? bankCode)
        {
            var profile = _detector.GetProfileByCode(bankCode!);
            Assert.Null(profile);
        }

        [Fact]
        public void GetProfileByCode_WithUnknownCode_ReturnsNull()
        {
            var profile = _detector.GetProfileByCode("NON_EXISTENT_BANK");
            Assert.Null(profile);
        }

        [Fact]
        public void SupportedProfiles_ContainsAllMajorBanksAndIsNonEmpty()
        {
            var profiles = _detector.SupportedProfiles;

            Assert.NotNull(profiles);
            Assert.NotEmpty(profiles);
            Assert.Contains(profiles, p => p.BankCode == "HDFC");
            Assert.Contains(profiles, p => p.BankCode == "SBI");
            Assert.Contains(profiles, p => p.BankCode == "AXIS");
            Assert.Contains(profiles, p => p.BankCode == "KOTAK");
            Assert.Contains(profiles, p => p.BankCode == "IDBI");
            Assert.Contains(profiles, p => p.BankCode == "IOB");
        }
    }
}
