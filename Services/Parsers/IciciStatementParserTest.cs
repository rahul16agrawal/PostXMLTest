using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PostXML.Models;
using PostXML.Services;
using PostXML.Services.Parsers;
using UglyToad.PdfPig;
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
    }
}
