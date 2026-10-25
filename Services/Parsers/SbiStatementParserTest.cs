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
    public class SbiStatementParserTest
    {
        private readonly SbiStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsSBI()
        {
            Assert.Equal("SBI", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_ValidSbiPdf_ExtractsExpectedTransactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "SBIBank", "SBIBank_Statement_001.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "SBI",
                DisplayName = "State Bank of India",
                DateFormat = "dd MMM yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            var parsingOptions = new ParsingOptions { Password = "72218191" };
            using var pdfDoc = PdfDocument.Open(stream, parsingOptions);

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
            Assert.Equal("State Bank of India", doc.BankName);
            Assert.Equal("00000043463737221", doc.AccountNumber);
            Assert.Equal(90, doc.Transactions.Count);

            var firstTxn = doc.Transactions[0];
            Assert.Equal(new DateTime(2026, 7, 1), firstTxn.TransactionDate);
            Assert.Equal(1.00m, firstTxn.WithdrawalAmount);
            Assert.Equal(0m, firstTxn.DepositAmount);
            Assert.Equal(688559.21m, firstTxn.RunningBalance);
        }

        [Fact]
        public void Parse_WithNullDefinition_UsesDefaultConfigWithoutThrowing()
        {
            var profile = new BankProfile
            {
                BankCode = "SBI",
                DisplayName = "State Bank of India",
                Definition = null
            };

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "SBIBank", "SBIBank_Statement_001.pdf");
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream, new ParsingOptions { Password = "72218191" });

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("State Bank of India", doc.BankName);
            Assert.Equal(90, doc.Transactions.Count);
        }
    }
}
