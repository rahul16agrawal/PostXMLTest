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

        [Fact]
        public void Parse_MultiLineNarration_DoesNotBleedIntoAdjacentTransactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "AxisBank", "AxisBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "AXIS",
                DisplayName = "Axis Bank",
                DateFormat = "dd-MM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            // Find the GST transaction on 31-12-2025
            var gstTx = doc.Transactions.FirstOrDefault(t => t.RawNarration.Contains("GST @18% on Monthly Service Chrgs"));
            Assert.NotNull(gstTx);
            // Verify it did not bleed the next transaction's RTGS narration into this one
            Assert.DoesNotContain("RTGS", gstTx.RawNarration);

            // Find the subsequent transaction on 31-12-2025
            var rtgsTx = doc.Transactions.FirstOrDefault(t => t.RawNarration.StartsWith("RTGS/SCBLR12025123100959425"));
            Assert.NotNull(rtgsTx);
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
                BankCode = "AXIS",
                DisplayName = "Axis Bank"
            };

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Empty(doc.Transactions);
            Assert.Equal(0m, doc.OpeningBalance);
            Assert.Equal(0m, doc.ClosingBalance);
        }

        [Fact]
        public void Parse_Statement001_SequentialRowIndicesAndReconciliation()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "AxisBank", "AxisBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "AXIS",
                DisplayName = "Axis Bank",
                DateFormat = "dd-MM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.Equal(89, doc.Transactions.Count);

            for (int i = 0; i < doc.Transactions.Count; i++)
            {
                Assert.Equal(i + 1, doc.Transactions[i].RowIndex);
                if (i > 0)
                {
                    Assert.True(doc.Transactions[i - 1].TransactionDate <= doc.Transactions[i].TransactionDate);
                }
            }

            var engine = new MathReconciliationEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<MathReconciliationEngine>.Instance);
            var summary = engine.Reconcile(doc);

            Assert.True(summary.IsFullyReconciled);
            Assert.Equal(0m, summary.DiscrepancyAmount);
        }
    }
}
