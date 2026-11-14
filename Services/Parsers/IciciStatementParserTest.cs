using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PostXML.Models;
using PostXML.Services;
using PostXML.Services.Parsers;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace PostXMLTest.Services.Parsers
{
    public class IciciStatementParserTest
    {
        private readonly IciciStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsICICI()
        {
            Assert.Equal("ICICI", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_Statement001_ExtractsAll31Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_001.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank",
                DateFormat = "dd-MM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("XXXXXXXX2639", doc.AccountNumber);
            Assert.Equal(24473.06m, doc.OpeningBalance);
            Assert.Equal(49886.19m, doc.ClosingBalance);
            Assert.Equal(31, doc.Transactions.Count);
            Assert.Equal(811387.30m, doc.TotalDeposits);
            Assert.Equal(785974.17m, doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_Statement002_ExtractsAll26Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_002.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank",
                DateFormat = "dd-MM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("XXXXXXXX1198", doc.AccountNumber);
            Assert.Equal(1749439.63m, doc.OpeningBalance);
            Assert.Equal(2277072.93m, doc.ClosingBalance);
            Assert.Equal(26, doc.Transactions.Count);
            Assert.Equal(1366343.68m, doc.TotalDeposits);
            Assert.Equal(838710.38m, doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_Statement003_ExtractsAll166Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_003.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank",
                DateFormat = "dd.MM.yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("162101508552", doc.AccountNumber);
            Assert.Equal("NEHA BANSAL", doc.AccountHolderName);
            Assert.Equal(161737.01m, doc.OpeningBalance);
            Assert.Equal(2889.78m, doc.ClosingBalance);
            Assert.Equal(166, doc.Transactions.Count);
            Assert.Equal(258245.00m, doc.TotalDeposits);
            Assert.Equal(417092.23m, doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_Statement004_ExtractsAll88Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_004.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank",
                DateFormat = "dd.MM.yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("162101508552", doc.AccountNumber);
            Assert.Equal("NEHA BANSAL", doc.AccountHolderName);
            Assert.Equal(19310.32m, doc.OpeningBalance);
            Assert.Equal(8137.51m, doc.ClosingBalance);
            Assert.Equal(88, doc.Transactions.Count);
            Assert.Equal(327516.44m, doc.TotalDeposits);
            Assert.Equal(338689.25m, doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_Statement005_ExtractsAll86Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_005.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank",
                DateFormat = "dd.MM.yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("162101508552", doc.AccountNumber);
            Assert.Equal("NEHA BANSAL", doc.AccountHolderName);
            Assert.Equal(191710.27m, doc.OpeningBalance);
            Assert.Equal(165490.00m, doc.ClosingBalance);
            Assert.Equal(86, doc.Transactions.Count);
            Assert.Equal(321974.57m, doc.TotalDeposits);
            Assert.Equal(348194.84m, doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_WithNullDefinition_UsesDefaultConfigWithoutThrowing()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_001.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank",
                Definition = null
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal(31, doc.Transactions.Count);
        }

        [Fact]
        public void Parse_EmptyPdfDocument_ReturnsEmptyStatementWithoutThrowing()
        {
            var builder = new PdfDocumentBuilder();
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            byte[] pdfBytes = builder.Build();

            using var ms = new MemoryStream(pdfBytes);
            using var pdfDoc = PdfDocument.Open(ms);

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank"
            };

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Empty(doc.Transactions);
            Assert.Equal(0m, doc.OpeningBalance);
            Assert.Equal(0m, doc.ClosingBalance);
            Assert.Equal(0m, doc.TotalDeposits);
            Assert.Equal(0m, doc.TotalWithdrawals);
        }

        [Theory]
        [InlineData("ICICIBank_Statement_003.pdf", 166)]
        [InlineData("ICICIBank_Statement_004.pdf", 88)]
        [InlineData("ICICIBank_Statement_005.pdf", 86)]
        public void Parse_NetBankingLayout_ZeroDiscrepancyAndTotalsReconciliation(string fileName, int expectedCount)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", fileName);

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank",
                DateFormat = "dd.MM.yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.Equal(expectedCount, doc.Transactions.Count);

            decimal expectedDeposits = doc.Transactions.Where(t => t.Type == TransactionType.Deposit).Sum(t => t.DepositAmount);
            decimal expectedWithdrawals = doc.Transactions.Where(t => t.Type == TransactionType.Withdrawal).Sum(t => t.WithdrawalAmount);

            Assert.Equal(expectedDeposits, doc.TotalDeposits);
            Assert.Equal(expectedWithdrawals, doc.TotalWithdrawals);
            Assert.Equal(doc.ClosingBalance, doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_NetBankingLayout_Statement003_ChequeNumberFiltersDashAndNa()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_003.pdf");

            var profile = new BankProfile
            {
                BankCode = "ICICI",
                DisplayName = "ICICI Bank",
                DateFormat = "dd.MM.yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            foreach (var txn in doc.Transactions)
            {
                if (!string.IsNullOrEmpty(txn.ReferenceNumber))
                {
                    Assert.NotEqual("-", txn.ReferenceNumber);
                    Assert.False(txn.ReferenceNumber.Equals("NA", StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }
}
