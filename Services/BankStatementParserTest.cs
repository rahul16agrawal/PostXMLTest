using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using PostXML.Models;
using PostXML.Services;
using PostXML.Services.Parsers;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace PostXMLTest.Services
{
    public class BankStatementParserTest
    {
        private readonly Mock<IBankDetectorService> _mockDetector;
        private readonly Mock<IMathReconciliationEngine> _mockReconciliationEngine;
        private readonly Mock<ILogger<BankStatementParser>> _mockLogger;
        private readonly BankStatementParser _parser;

        public BankStatementParserTest()
        {
            _mockDetector = new Mock<IBankDetectorService>();
            _mockReconciliationEngine = new Mock<IMathReconciliationEngine>();
            _mockLogger = new Mock<ILogger<BankStatementParser>>();

            _parser = new BankStatementParser(
                _mockDetector.Object,
                _mockReconciliationEngine.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void ParsePdf_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _parser.ParsePdf(null!));
        }

        [Fact]
        public void IsPdfEncrypted_WithEncryptedTrailer_ReturnsTrue()
        {
            byte[] fakePdfWithEncrypt = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<</Root 1 0 R/Encrypt 2 0 R>>\n%%EOF");
            using var stream = new MemoryStream(fakePdfWithEncrypt);

            bool isEncrypted = _parser.IsPdfEncrypted(stream);
            Assert.True(isEncrypted);
        }

        [Fact]
        public void IsPdfEncrypted_WithoutEncryptTrailer_ReturnsFalse()
        {
            byte[] fakePdfNormal = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");
            using var stream = new MemoryStream(fakePdfNormal);

            bool isEncrypted = _parser.IsPdfEncrypted(stream);
            Assert.False(isEncrypted);
        }

        [Fact]
        public void ParseLines_ExtractsMetadataAndTransactions()
        {
            var lines = new List<string>
            {
                "HDFC BANK LIMITED",
                "Account No : 50100456789012",
                "Opening Balance: 10,000.00",
                "01/04/2024 UPI/4098234123/ZOMATO/Payment 500.00 0.00 9,500.00",
                "02/04/2024 NEFT/IN987123/SALARY CREDIT 0.00 50,000.00 59,500.00",
                "03/04/2024 CASH WDL ATM SOUTH EX 2,000.00 0.00 57,500.00",
                "04/04/2024 CHG/SMS ALERT FEE 17.70 0.00 57,482.30",
                "Closing Balance: 57,482.30"
            };

            var profile = new BankProfile
            {
                BankCode = "HDFC",
                DisplayName = "HDFC Bank"
            };

            var doc = _parser.ParseLines(lines, profile);

            Assert.NotNull(doc);
            Assert.Equal("HDFC Bank", doc.BankName);
            Assert.Equal("50100456789012", doc.AccountNumber);
            Assert.Equal(10000.00m, doc.OpeningBalance);
            Assert.Equal(57482.30m, doc.ClosingBalance);
            Assert.Equal(4, doc.Transactions.Count);

            // Row 1: Zomato expense
            var t1 = doc.Transactions[0];
            Assert.Equal(new DateTime(2024, 4, 1), t1.TransactionDate);
            Assert.Equal(500.00m, t1.WithdrawalAmount);
            Assert.Equal(0m, t1.DepositAmount);
            Assert.Equal(9500.00m, t1.RunningBalance);
            Assert.Equal(TransactionType.Withdrawal, t1.Type);
            Assert.Equal("Staff Welfare Expense", t1.SuggestedLedgerName);
            Assert.Equal("UPI", t1.PaymentMode);

            // Row 2: Salary credit
            var t2 = doc.Transactions[1];
            Assert.Equal(new DateTime(2024, 4, 2), t2.TransactionDate);
            Assert.Equal(0m, t2.WithdrawalAmount);
            Assert.Equal(50000.00m, t2.DepositAmount);
            Assert.Equal(59500.00m, t2.RunningBalance);
            Assert.Equal(TransactionType.Deposit, t2.Type);
            Assert.Equal("Salary Expense", t2.SuggestedLedgerName);
            Assert.Equal("NEFT", t2.PaymentMode);

            // Row 3: Cash withdrawal -> Contra
            var t3 = doc.Transactions[2];
            Assert.Equal(new DateTime(2024, 4, 3), t3.TransactionDate);
            Assert.Equal(2000.00m, t3.WithdrawalAmount);
            Assert.Equal(TransactionType.Contra, t3.Type);

            // Row 4: Bank charges
            var t4 = doc.Transactions[3];
            Assert.Equal(new DateTime(2024, 4, 4), t4.TransactionDate);
            Assert.Equal(17.70m, t4.WithdrawalAmount);
            Assert.Equal("CHARGES", t4.PaymentMode);
            Assert.Equal("Bank Charges", t4.SuggestedLedgerName);
        }

        [Fact]
        public void ParseLines_MultilineNarration_ConcatenatesCorrectly()
        {
            var lines = new List<string>
            {
                "15/05/2024 UPI/412345/AMAZON SELLER 1,299.00 0.00 25,000.00",
                "ORDER # 402-9871625-112233",
                "BANGALORE KARNATAKA IN",
                "16/05/2024 POS SWIPE PETROL PUMP 500.00 0.00 24,500.00"
            };

            var profile = new BankProfile { BankCode = "GENERIC", DisplayName = "Generic Bank" };

            var doc = _parser.ParseLines(lines, profile);

            Assert.Equal(2, doc.Transactions.Count);

            var t1 = doc.Transactions[0];
            Assert.Contains("AMAZON", t1.RawNarration);
            Assert.Contains("ORDER # 402-9871625-112233", t1.RawNarration);
            Assert.Contains("BANGALORE", t1.RawNarration);
            Assert.Equal("Office Supplies", t1.SuggestedLedgerName);

            var t2 = doc.Transactions[1];
            Assert.Equal("Fuel Expense", t2.SuggestedLedgerName);
        }

        [Fact]
        public void ParseLines_TwoAmountsWithCreditIndicator_CategorizedAsDeposit()
        {
            var lines = new List<string>
            {
                "10-Jun-2024 DIVIDEND CREDIT TCS 1,500.00 CR 45,000.00"
            };

            var profile = new BankProfile { BankCode = "KOTAK", DisplayName = "Kotak Mahindra Bank" };

            var doc = _parser.ParseLines(lines, profile);

            Assert.Single(doc.Transactions);
            var t = doc.Transactions[0];
            Assert.Equal(1500.00m, t.DepositAmount);
            Assert.Equal(0m, t.WithdrawalAmount);
            Assert.Equal(45000.00m, t.RunningBalance);
            Assert.Equal(TransactionType.Deposit, t.Type);
        }

        [Fact]
        public void ParseLines_UpiCreditWithSlash_CategorizedAsDeposit()
        {
            var lines = new List<string>
            {
                "01/08/2026 UPI/CR/621397903960/ARVIND K/SBIN/**87098@OKSBI 330.00 -3,029,604.43"
            };

            var profile = new BankProfile { BankCode = "CANARA", DisplayName = "Canara Bank" };
            var doc = _parser.ParseLines(lines, profile);

            Assert.Single(doc.Transactions);
            var t = doc.Transactions[0];
            Assert.Equal(330.00m, t.DepositAmount);
            Assert.Equal(0m, t.WithdrawalAmount);
            Assert.Equal(TransactionType.Deposit, t.Type);
        }

        [Fact]
        public void ParseLines_WrappedTimestampLineWithoutAmounts_StitchedIntoNarrationNotNewTransaction()
        {
            var lines = new List<string>
            {
                "01/08/2026 UPI/CR/623656267627/KUNAL KUM/YESB/**08511@GOAXB 500.00 -3,029,104.43",
                "01/08/2026 08:40:23 Date Particulars Withdrawals Deposits Balance"
            };

            var profile = new BankProfile { BankCode = "CANARA", DisplayName = "Canara Bank" };
            var doc = _parser.ParseLines(lines, profile);

            Assert.Single(doc.Transactions);
            Assert.DoesNotContain("Date Particulars", doc.Transactions[0].RawNarration);
        }

        [Fact]
        public void ParseLines_ReversalWithChargeKeyword_CategorizedAsDeposit()
        {
            var lines = new List<string>
            {
                "22/09/2026 REV:EXCESS SMS SC ENTRY DATED - 20-SEP-26 178.76 -3,714,745.81"
            };

            var profile = new BankProfile { BankCode = "CANARA", DisplayName = "Canara Bank" };
            var doc = _parser.ParseLines(lines, profile);

            Assert.Single(doc.Transactions);
            var t = doc.Transactions[0];
            Assert.Equal(178.76m, t.DepositAmount);
            Assert.Equal(0m, t.WithdrawalAmount);
            Assert.Equal(TransactionType.Deposit, t.Type);
        }

        [Fact]
        public void ParseLines_PosPaymentWithUnderscore_CategorizedAsDeposit()
        {
            var lines = new List<string>
            {
                "22/09/2026 POS_PMNT_10061547_22092026 99.60 -3,714,646.21"
            };

            var profile = new BankProfile { BankCode = "CANARA", DisplayName = "Canara Bank" };
            var doc = _parser.ParseLines(lines, profile);

            Assert.Single(doc.Transactions);
            var t = doc.Transactions[0];
            Assert.Equal(99.60m, t.DepositAmount);
            Assert.Equal(0m, t.WithdrawalAmount);
            Assert.Equal(TransactionType.Deposit, t.Type);
        }

        [Fact]
        public void ParseLines_DeclarativeProfile_ExtractsPartyAndReferenceFromNarration()
        {
            var definition = new BankProfileDefinition
            {
                BankName = "Canara Bank",
                BankCode = "CANARA",
                NarrationExtractors = new Dictionary<string, string>
                {
                    ["upi"] = @"UPI/(?:CR|DR)/(?<utr>\d+)/(?<party>[^/]+)/(?<vpa>[^/]+)"
                }
            };

            var profile = definition.ToLegacyProfile();

            var lines = new List<string>
            {
                "01/08/2026 UPI/CR/621397903960/ARVIND K/SBIN/**87098@OKSBI/UPI 330.00 -29,86,644.43"
            };

            var doc = _parser.ParseLines(lines, profile);

            Assert.Single(doc.Transactions);
            var t = doc.Transactions[0];
            Assert.Equal("ARVIND K", t.CleanEntityName);
            Assert.Equal("621397903960", t.ReferenceNumber);
        }

        [Fact]
        public void ParsePdf_ValidPdfStream_InvokesDetectorAndReconciliation()
        {
            var builder = new PdfDocumentBuilder();
            var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            // PdfPig doesn't require font embedding for standard 14 fonts when building basic documents
            // Let's create an empty/simple PDF
            byte[] pdfBytes = builder.Build();

            using var ms = new MemoryStream(pdfBytes);

            _mockDetector
                .Setup(d => d.DetectBank(It.IsAny<string>()))
                .Returns(new BankProfile { BankCode = "GENERIC", DisplayName = "Generic Bank" });

            var doc = _parser.ParsePdf(ms);

            Assert.NotNull(doc);
            Assert.Equal("Generic Bank", doc.BankName);
            _mockDetector.Verify(d => d.DetectBank(It.IsAny<string>()), Times.Once);
            _mockReconciliationEngine.Verify(r => r.Reconcile(It.IsAny<StatementDocument>()), Times.Once);
        }

        [Fact]
        public void ParsePdf_RealHdfcStatement_EndToEndSuccess()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_002.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var detectorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BankDetectorService>.Instance;
            var detector = new BankDetectorService(detectorLogger);
            var reconLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MathReconciliationEngine>.Instance;
            var reconEngine = new MathReconciliationEngine(reconLogger);
            var parserLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BankStatementParser>.Instance;

            var parser = new BankStatementParser(detector, reconEngine, parserLogger);

            using var stream = File.OpenRead(pdfPath);
            var doc = parser.ParsePdf(stream);

            Assert.NotNull(doc);
            Assert.Equal("HDFC Bank", doc.BankName);
            Assert.Equal("50200037586423", doc.AccountNumber);
            Assert.Equal(162, doc.Transactions.Count);
            Assert.True(doc.Reconciliation.IsFullyReconciled);
        }

        [Fact]
        public void ParsePdf_RealHdfcHanumanStatement_EndToEndSuccess()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_003.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var detectorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BankDetectorService>.Instance;
            var detector = new BankDetectorService(detectorLogger);
            var reconLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MathReconciliationEngine>.Instance;
            var reconEngine = new MathReconciliationEngine(reconLogger);
            var parserLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BankStatementParser>.Instance;

            var parser = new BankStatementParser(detector, reconEngine, parserLogger);

            using var stream = File.OpenRead(pdfPath);
            var doc = parser.ParsePdf(stream);

            Assert.NotNull(doc);
            Assert.Equal("HDFC Bank", doc.BankName);
            Assert.Equal("50200075012327", doc.AccountNumber);
            Assert.Equal(1001, doc.Transactions.Count);
            Assert.True(doc.Reconciliation.IsFullyReconciled);
        }

        [Fact]
        public void ParsePdf_WithBankCodeOverride_BypassesAutoDetectAndUsesOverriddenProfile()
        {
            var builder = new PdfDocumentBuilder();
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            byte[] pdfBytes = builder.Build();

            using var ms = new MemoryStream(pdfBytes);

            var axisProfile = new BankProfile { BankCode = "AXIS", DisplayName = "Axis Bank" };
            _mockDetector
                .Setup(d => d.GetProfileByCode("AXIS"))
                .Returns(axisProfile);

            var doc = _parser.ParsePdf(ms, password: null, bankCodeOverride: "AXIS");

            Assert.NotNull(doc);
            Assert.Equal("Axis Bank", doc.BankName);
            _mockDetector.Verify(d => d.DetectBank(It.IsAny<string>()), Times.Never);
            _mockDetector.Verify(d => d.GetProfileByCode("AXIS"), Times.Once);
            _mockReconciliationEngine.Verify(r => r.Reconcile(It.IsAny<StatementDocument>()), Times.Once);
        }

        [Fact]
        public void ParsePdf_WithUnknownBankCodeOverride_CreatesFallbackProfileAndBypassesDetector()
        {
            var builder = new PdfDocumentBuilder();
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            byte[] pdfBytes = builder.Build();

            using var ms = new MemoryStream(pdfBytes);

            _mockDetector
                .Setup(d => d.GetProfileByCode("UNKNOWN_COOP"))
                .Returns((BankProfile?)null);

            var doc = _parser.ParsePdf(ms, password: null, bankCodeOverride: "UNKNOWN_COOP");

            Assert.NotNull(doc);
            Assert.Equal("UNKNOWN_COOP", doc.BankName);
            _mockDetector.Verify(d => d.DetectBank(It.IsAny<string>()), Times.Never);
            _mockDetector.Verify(d => d.GetProfileByCode("UNKNOWN_COOP"), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void ParsePdf_WithNullOrWhitespaceBankCodeOverride_FallsBackToDetectBank(string? bankCodeOverride)
        {
            var builder = new PdfDocumentBuilder();
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            byte[] pdfBytes = builder.Build();

            using var ms = new MemoryStream(pdfBytes);

            _mockDetector
                .Setup(d => d.DetectBank(It.IsAny<string>()))
                .Returns(new BankProfile { BankCode = "GENERIC", DisplayName = "Standard Bank Statement" });

            var doc = _parser.ParsePdf(ms, password: null, bankCodeOverride: bankCodeOverride);

            Assert.NotNull(doc);
            Assert.Equal("Standard Bank Statement", doc.BankName);
            _mockDetector.Verify(d => d.DetectBank(It.IsAny<string>()), Times.Once);
            _mockDetector.Verify(d => d.GetProfileByCode(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ParsePdf_WithBankOverride_DelegatesToRegisteredCustomBankParser()
        {
            var builder = new PdfDocumentBuilder();
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            byte[] pdfBytes = builder.Build();

            using var ms = new MemoryStream(pdfBytes);

            var mockCustomParser = new Mock<IBankPdfParser>();
            mockCustomParser.SetupGet(p => p.SupportedBankCode).Returns("CUSTOM_BANK");
            mockCustomParser
                .Setup(p => p.Parse(It.IsAny<PdfDocument>(), It.IsAny<BankProfile>(), It.IsAny<List<string>>()))
                .Returns(new StatementDocument
                {
                    BankName = "Delegated Custom Bank",
                    AccountNumber = "CUSTOM-ACCT-999"
                });

            var customProfile = new BankProfile { BankCode = "CUSTOM_BANK", DisplayName = "Delegated Custom Bank" };
            _mockDetector
                .Setup(d => d.GetProfileByCode("CUSTOM_BANK"))
                .Returns(customProfile);

            var parserWithCustom = new BankStatementParser(
                _mockDetector.Object,
                _mockReconciliationEngine.Object,
                _mockLogger.Object,
                new[] { mockCustomParser.Object });

            var doc = parserWithCustom.ParsePdf(ms, password: null, bankCodeOverride: "CUSTOM_BANK");

            Assert.NotNull(doc);
            Assert.Equal("Delegated Custom Bank", doc.BankName);
            Assert.Equal("CUSTOM-ACCT-999", doc.AccountNumber);
            mockCustomParser.Verify(p => p.Parse(It.IsAny<PdfDocument>(), It.IsAny<BankProfile>(), It.IsAny<List<string>>()), Times.Once);
        }
    }
}
