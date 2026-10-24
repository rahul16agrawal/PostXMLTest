using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using PostXML.Models;
using PostXML.Services;
using Xunit;

namespace PostXMLTest.Services
{
    public class BankProfileRegistryTest
    {
        private readonly Mock<ILogger<BankProfileRegistry>> _mockLogger;
        private readonly BankProfileRegistry _registry;

        public BankProfileRegistryTest()
        {
            _mockLogger = new Mock<ILogger<BankProfileRegistry>>();
            _registry = new BankProfileRegistry(_mockLogger.Object);
        }

        [Fact]
        public void GetAllProfiles_ContainsDefaultEmbeddedProfiles()
        {
            var profiles = _registry.GetAllProfiles();

            Assert.NotEmpty(profiles);
            Assert.Contains(profiles, p => p.BankCode == "CANARA");
            Assert.Contains(profiles, p => p.BankCode == "HDFC");
            Assert.Contains(profiles, p => p.BankCode == "SBI");
            Assert.Contains(profiles, p => p.BankCode == "GENERIC");
        }

        [Fact]
        public void MatchProfile_WithCanaraBankKeywords_MatchesCanaraProfile()
        {
            string header = "CANARA BANK\nSTATEMENT OF ACCOUNT FOR THE PERIOD 01/08/2026 TO 30/09/2026\nIFSC: CNRB0001234";

            var profile = _registry.MatchProfile(header);

            Assert.NotNull(profile);
            Assert.Equal("CANARA", profile.BankCode);
            Assert.Equal("Canara Bank", profile.BankName);
        }

        [Fact]
        public void MatchProfile_WithHdfcIfsc_MatchesHdfcProfile()
        {
            string header = "Customer Name: John Doe\nRTGS/NEFT IFSC: HDFC0000240\nHDFC BANK LIMITED";

            var profile = _registry.MatchProfile(header);

            Assert.NotNull(profile);
            Assert.Equal("HDFC", profile.BankCode);
        }

        [Fact]
        public void MatchProfile_WithUnknownBank_ReturnsGenericProfile()
        {
            string header = "Some Local Rural Cooperative Society\nSavings Account Passbook";

            var profile = _registry.MatchProfile(header);

            Assert.NotNull(profile);
            Assert.Equal("GENERIC", profile.BankCode);
            Assert.Equal("Standard Bank Statement", profile.BankName);
        }

        [Fact]
        public void LoadProfilesFromDirectory_WithCustomJson_OverridesOrAddsProfile()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "PostXML_TestProfiles_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string customJson = @"{
                    ""profileId"": ""CUSTOM_TEST_BANK_V1"",
                    ""bankName"": ""Custom Test Bank"",
                    ""bankCode"": ""CTB"",
                    ""version"": ""1.0"",
                    ""fingerprint"": {
                        ""ifscPrefixes"": [""CTBX""],
                        ""requiredKeywords"": [""CUSTOM TEST BANK""]
                    }
                }";

                File.WriteAllText(Path.Combine(tempDir, "Custom_Bank.json"), customJson);

                _registry.LoadProfilesFromDirectory(tempDir);

                var matched = _registry.MatchProfile("CUSTOM TEST BANK IFSC: CTBX0001");
                Assert.Equal("CTB", matched.BankCode);
                Assert.Equal("Custom Test Bank", matched.BankName);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
