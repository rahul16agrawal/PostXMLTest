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
    public class IdbiStatementParserTest
    {
        private readonly IdbiStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsIDBI()
        {
            Assert.Equal("IDBI", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_ValidIdbiPdf_ExtractsExpectedTransactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_001.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "IDBI",
                DisplayName = "IDBI Bank",
                DateFormat = "dd/MM/yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var allLines = new List<string>();
            for (int i = 1; i <= pdfDoc.NumberOfPages; i++)
            {
                var page = pdfDoc.GetPage(i);
                var words = page.GetWords().ToList();
                var lines = words
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                    .OrderByDescending(g => g.Key)
                    .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
                allLines.AddRange(lines);
            }

            var doc = _parser.Parse(pdfDoc, profile, allLines);

            Assert.NotNull(doc);
            Assert.Equal("IDBI Bank", doc.BankName);
            Assert.Equal("1075102000012148", doc.AccountNumber);
            Assert.Equal(1442, doc.Transactions.Count);
            Assert.Equal(25751.95m, doc.OpeningBalance);
            Assert.Equal(439063.01m, doc.ClosingBalance);

            // Verify order is normalized forward chronological
            Assert.True(doc.Transactions[0].TransactionDate <= doc.Transactions[^1].TransactionDate);
        }

        [Fact]
        public void Parse_WithNullDefinition_UsesDefaultConfigWithoutThrowing()
        {
            var profile = new BankProfile
            {
                BankCode = "IDBI",
                DisplayName = "IDBI Bank",
                Definition = null
            };

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_001.pdf");
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal(1442, doc.Transactions.Count);
        }

        [Fact]
        public void Parse_Statement002_ExtractsAll163Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_002.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "IDBI",
                DisplayName = "IDBI Bank",
                DateFormat = "dd/MM/yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("IDBI Bank", doc.BankName);
            Assert.Equal("0719102000017897", doc.AccountNumber);
            Assert.Equal("ANURAG ENTERPRISES", doc.AccountHolderName);
            Assert.Equal("IBKL0000719", doc.IfscCode);
            Assert.Equal(163, doc.Transactions.Count);
            Assert.Equal(4360.55m, doc.OpeningBalance);
            Assert.Equal(3707.46m, doc.ClosingBalance);
        }

        [Fact]
        public void Parse_Statement003_ExtractsAll294Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_003.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "IDBI",
                DisplayName = "IDBI Bank",
                DateFormat = "dd/MM/yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("IDBI Bank", doc.BankName);
            Assert.Equal("1075653800000198", doc.AccountNumber);
            Assert.Equal("ANIKA COAL BRIQUETTE", doc.AccountHolderName);
            Assert.Equal("IBKL0001075", doc.IfscCode);
            Assert.Equal(294, doc.Transactions.Count);
            Assert.Equal(-8994466.08m, doc.OpeningBalance);
            Assert.Equal(-8190748.38m, doc.ClosingBalance);
        }

        [Fact]
        public void Parse_Statement004_ExtractsAll1202Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_004.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "IDBI",
                DisplayName = "IDBI Bank",
                DateFormat = "dd/MM/yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("IDBI Bank", doc.BankName);
            Assert.Equal("1075102000012148", doc.AccountNumber);
            Assert.Equal("ANIKA COAL BRIQUETTE", doc.AccountHolderName);
            Assert.Equal("IBKL0001075", doc.IfscCode);
            Assert.Equal(1202, doc.Transactions.Count);
            Assert.Equal(439063.01m, doc.OpeningBalance);
            Assert.Equal(6866.00m, doc.ClosingBalance);
            Assert.Equal(23379301.40m, doc.TotalDeposits);
            Assert.Equal(23811498.41m, doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_EmptyPdfDocument_ReturnsEmptyStatementWithZeroTotals()
        {
            var builder = new PdfDocumentBuilder();
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            byte[] pdfBytes = builder.Build();

            using var ms = new MemoryStream(pdfBytes);
            using var pdfDoc = PdfDocument.Open(ms);

            var profile = new BankProfile
            {
                BankCode = "IDBI",
                DisplayName = "IDBI Bank"
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
        [InlineData("IDBIBank_Statement_001.pdf", 1442, 25751.95, 439063.01)]
        [InlineData("IDBIBank_Statement_002.pdf", 163, 4360.55, 3707.46)]
        [InlineData("IDBIBank_Statement_003.pdf", 294, -8994466.08, -8190748.38)]
        public void Parse_Statements_CalculatesTotalsAndReconcilesClosingBalance(
            string fileName,
            int expectedTxnCount,
            decimal expectedOpening,
            decimal expectedClosing)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "IDBIBank", fileName);

            var profile = new BankProfile
            {
                BankCode = "IDBI",
                DisplayName = "IDBI Bank",
                DateFormat = "dd/MM/yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.Equal(expectedTxnCount, doc.Transactions.Count);
            Assert.Equal(expectedOpening, doc.OpeningBalance);
            Assert.Equal(expectedClosing, doc.ClosingBalance);

            decimal expectedDeposits = doc.Transactions.Where(t => t.Type == TransactionType.Deposit).Sum(t => t.DepositAmount);
            decimal expectedWithdrawals = doc.Transactions.Where(t => t.Type == TransactionType.Withdrawal).Sum(t => t.WithdrawalAmount);

            Assert.Equal(expectedDeposits, doc.TotalDeposits);
            Assert.Equal(expectedWithdrawals, doc.TotalWithdrawals);
            Assert.Equal(doc.ClosingBalance, doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_Statement002And003_ExtractsIfscCodeAndDateRange()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            // Statement 002
            string p2 = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_002.pdf");
            var profile = new BankProfile { BankCode = "IDBI", DisplayName = "IDBI Bank" };
            using (var s2 = File.OpenRead(p2))
            using (var doc2 = PdfDocument.Open(s2))
            {
                var res2 = _parser.Parse(doc2, profile, new List<string>());
                Assert.Equal("IBKL0000719", res2.IfscCode);
                Assert.NotNull(res2.StatementStartDate);
                Assert.NotNull(res2.StatementEndDate);
            }

            // Statement 003
            string p3 = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_003.pdf");
            using (var s3 = File.OpenRead(p3))
            using (var doc3 = PdfDocument.Open(s3))
            {
                var res3 = _parser.Parse(doc3, profile, new List<string>());
                Assert.Equal("IBKL0001075", res3.IfscCode);
                Assert.NotNull(res3.StatementStartDate);
                Assert.NotNull(res3.StatementEndDate);
            }
        }
    }
}
