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
    public class AxisStatementParserTest
    {
        private readonly AxisStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsAXIS()
        {
            Assert.Equal("AXIS", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_ValidAxisPdf_ExtractsExpectedTransactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "AxisBank", "AxisBank_Statement_001.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "AXIS",
                DisplayName = "Axis Bank",
                DateFormat = "dd-MM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("Axis Bank", doc.BankName);
            Assert.Equal("924020049955809", doc.AccountNumber);
            Assert.Equal("UTIB0001075", doc.IfscCode);
            Assert.Equal("GLOBAL AGRO", doc.AccountHolderName);
            Assert.Equal(12795.00m, doc.OpeningBalance);
            Assert.Equal(42672.10m, doc.ClosingBalance);
            Assert.Equal(89, doc.Transactions.Count);

            // Reconcile
            var engine = new MathReconciliationEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<MathReconciliationEngine>.Instance);
            var summary = engine.Reconcile(doc);

            Assert.True(summary.IsFullyReconciled);
            Assert.Equal(0m, summary.DiscrepancyAmount);
            Assert.Empty(summary.MismatchedRowIndices);

            // Verify order is forward chronological
            Assert.True(doc.Transactions[0].TransactionDate <= doc.Transactions[^1].TransactionDate);
        }

        [Fact]
        public void Parse_WithNullDefinition_UsesDefaultConfigWithoutThrowing()
        {
            var profile = new BankProfile
            {
                BankCode = "AXIS",
                DisplayName = "Axis Bank",
                Definition = null
            };

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "AxisBank", "AxisBank_Statement_001.pdf");
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal(89, doc.Transactions.Count);
        }
    }
}
