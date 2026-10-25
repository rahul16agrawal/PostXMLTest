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
    }
}
