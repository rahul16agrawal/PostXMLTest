using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PostXML.Models;
using PostXML.Services.Parsers;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace PostXMLTest.Services.Parsers
{
    public class PnbStatementParserTest
    {
        private readonly PnbStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsPNB()
        {
            Assert.Equal("PNB", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_WithNullDefinition_UsesDefaultConfigWithoutThrowing()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "PNBBank", "PNBBank_Statement_001.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "PNB",
                DisplayName = "Punjab National Bank",
                Definition = null
            };

            var options = new ParsingOptions { Password = "9881002100002425" };
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream, options);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal(367, doc.Transactions.Count);
            Assert.Equal("Punjab National Bank", doc.BankName);
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
                BankCode = "PNB",
                DisplayName = "Punjab National Bank"
            };

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Empty(doc.Transactions);
            Assert.Equal(0m, doc.OpeningBalance);
            Assert.Equal(0m, doc.ClosingBalance);
            Assert.Equal(0m, doc.TotalDeposits);
            Assert.Equal(0m, doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_Statement001_SequentialRowIndicesAndForwardChronological()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "PNBBank", "PNBBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "PNB",
                DisplayName = "Punjab National Bank",
                DateFormat = "dd/MM/yyyy"
            };

            var options = new ParsingOptions { Password = "9881002100002425" };
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream, options);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.Equal(367, doc.Transactions.Count);

            for (int i = 0; i < doc.Transactions.Count; i++)
            {
                Assert.Equal(i + 1, doc.Transactions[i].RowIndex);
                if (i > 0)
                {
                    Assert.True(doc.Transactions[i - 1].TransactionDate <= doc.Transactions[i].TransactionDate,
                        $"Transaction at row {i + 1} has date {doc.Transactions[i].TransactionDate:yyyy-MM-dd} earlier than previous {doc.Transactions[i - 1].TransactionDate:yyyy-MM-dd}");
                }
            }
        }

        [Fact]
        public void Parse_Statement001_CalculatesTotalsAndReconcilesClosingBalance()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "PNBBank", "PNBBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "PNB",
                DisplayName = "Punjab National Bank",
                DateFormat = "dd/MM/yyyy"
            };

            var options = new ParsingOptions { Password = "9881002100002425" };
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream, options);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            decimal expectedDeposits = doc.Transactions.Where(t => t.Type == TransactionType.Deposit).Sum(t => t.DepositAmount);
            decimal expectedWithdrawals = doc.Transactions.Where(t => t.Type == TransactionType.Withdrawal).Sum(t => t.WithdrawalAmount);

            Assert.Equal(expectedDeposits, doc.TotalDeposits);
            Assert.Equal(expectedWithdrawals, doc.TotalWithdrawals);
            Assert.Equal(doc.ClosingBalance, doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals);
        }

        [Fact]
        public void Parse_Statement001_ExtractsMetadataFromPage1()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "PNBBank", "PNBBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "PNB",
                DisplayName = "Punjab National Bank",
                DateFormat = "dd/MM/yyyy"
            };

            var options = new ParsingOptions { Password = "9881002100002425" };
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream, options);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.Equal("9881002100002425", doc.AccountNumber);
            Assert.Equal("ANUSHKA COAL BRIQUETTE", doc.AccountHolderName);
            Assert.Equal("PUNB0988100", doc.IfscCode);
            Assert.Equal(new DateTime(2026, 4, 1), doc.StatementStartDate);
            Assert.Equal(new DateTime(2026, 11, 13), doc.StatementEndDate);
        }

        [Fact]
        public void Parse_Statement001_ReferenceNumber_FiltersDashesAndPreservesValidIds()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "PNBBank", "PNBBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "PNB",
                DisplayName = "Punjab National Bank",
                DateFormat = "dd/MM/yyyy"
            };

            var options = new ParsingOptions { Password = "9881002100002425" };
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream, options);

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

        [Fact]
        public void Parse_Statement001_ExtractsAll367Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "PNBBank", "PNBBank_Statement_001.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "PNB",
                DisplayName = "Punjab National Bank",
                DateFormat = "dd/MM/yyyy"
            };

            var options = new ParsingOptions { Password = "9881002100002425" };
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream, options);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("Punjab National Bank", doc.BankName);
            Assert.Equal("9881002100002425", doc.AccountNumber);
            Assert.Equal("ANUSHKA COAL BRIQUETTE", doc.AccountHolderName);
            Assert.Equal("PUNB0988100", doc.IfscCode);
            Assert.Equal(284492.81m, doc.OpeningBalance);
            Assert.Equal(71021.46m, doc.ClosingBalance);
            Assert.Equal(367, doc.Transactions.Count);
            Assert.Equal(23580301.20m, doc.TotalDeposits);
            Assert.Equal(23793772.55m, doc.TotalWithdrawals);
        }
    }
}
