using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PostXML.Models;
using PostXML.Services.Parsers;
using UglyToad.PdfPig;
using Xunit;

namespace PostXMLTest.Services.Parsers
{
    public class HdfcStatementParserTest
    {
        private readonly HdfcStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsHDFC()
        {
            Assert.Equal("HDFC", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_ValidHdfcPdf_ExtractsExpectedTransactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_001.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "HDFC",
                DisplayName = "HDFC Bank",
                DateFormat = "dd-MMM-yyyy"
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
            Assert.Equal("HDFC Bank", doc.BankName);
            Assert.Equal("50200120952504", doc.AccountNumber);
            Assert.Equal(136368.54m, doc.OpeningBalance);
            Assert.Equal(37, doc.Transactions.Count);
            Assert.Equal(101324.54m, doc.ClosingBalance);

            // Verify order is normalized forward chronological
            Assert.True(doc.Transactions[0].TransactionDate <= doc.Transactions[^1].TransactionDate);
        }

        [Fact]
        public void Parse_WithNullDefinition_UsesDefaultConfigWithoutThrowing()
        {
            var profile = new BankProfile
            {
                BankCode = "HDFC",
                DisplayName = "HDFC Bank",
                Definition = null
            };

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_001.pdf");
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal(37, doc.Transactions.Count);
        }

        [Fact]
        public void Parse_JaiInternationalPdf_ExtractsAllTransactionsAndReconciles()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_002.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "HDFC",
                DisplayName = "HDFC Bank",
                DateFormat = "dd/MM/yy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("50200037586423", doc.AccountNumber);
            Assert.Equal(162, doc.Transactions.Count);
            Assert.Equal(0.00m, doc.OpeningBalance);
            Assert.Equal(1200042.80m, doc.ClosingBalance);

            var engine = new PostXML.Services.MathReconciliationEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<PostXML.Services.MathReconciliationEngine>.Instance);
            var summary = engine.Reconcile(doc);
            Assert.True(summary.IsFullyReconciled);
            Assert.Equal(0m, summary.DiscrepancyAmount);
        }

        [Fact]
        public void Parse_HanumanPdf_ExtractsAllTransactionsAndReconciles()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_003.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "HDFC",
                DisplayName = "HDFC Bank",
                DateFormat = "dd/MM/yy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("50200075012327", doc.AccountNumber);
            Assert.Equal(1001, doc.Transactions.Count);
            Assert.Equal(3269.83m, doc.OpeningBalance);
            Assert.Equal(11262.63m, doc.ClosingBalance);

            var engine = new PostXML.Services.MathReconciliationEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<PostXML.Services.MathReconciliationEngine>.Instance);
            var summary = engine.Reconcile(doc);
            Assert.True(summary.IsFullyReconciled);
            Assert.Equal(0m, summary.DiscrepancyAmount);
        }
    }
}
