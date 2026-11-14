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
    public class BandhanStatementParserTest
    {
        private readonly BandhanStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsBANDHAN()
        {
            Assert.Equal("BANDHAN", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_WithNullDefinition_UsesDefaultConfigWithoutThrowing()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Statement_001.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "BANDHAN",
                DisplayName = "Bandhan Bank",
                Definition = null
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal(433, doc.Transactions.Count);
            Assert.Equal("Bandhan Bank", doc.BankName);
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
                BankCode = "BANDHAN",
                DisplayName = "Bandhan Bank"
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
            string pdfPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "BANDHAN",
                DisplayName = "Bandhan Bank",
                DateFormat = "dd-MMMM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.Equal(433, doc.Transactions.Count);

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
            string pdfPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "BANDHAN",
                DisplayName = "Bandhan Bank",
                DateFormat = "dd-MMMM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

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
            string pdfPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "BANDHAN",
                DisplayName = "Bandhan Bank",
                DateFormat = "dd-MMMM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.Equal("M/S BIMLA ENTERPRISES", doc.AccountHolderName);
            Assert.Equal("20100083106505", doc.AccountNumber);
            Assert.Equal("BDBL0001432", doc.IfscCode);
            Assert.Equal(new DateTime(2026, 4, 1), doc.StatementStartDate);
            Assert.Equal(new DateTime(2026, 11, 13), doc.StatementEndDate);
        }

        [Fact]
        public void Parse_Statement001_ClassifiesDebitAndCreditTypesAccurately()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Statement_001.pdf");

            var profile = new BankProfile
            {
                BankCode = "BANDHAN",
                DisplayName = "Bandhan Bank",
                DateFormat = "dd-MMMM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            foreach (var txn in doc.Transactions)
            {
                if (txn.Type == TransactionType.Deposit)
                {
                    Assert.True(txn.DepositAmount > 0, $"Deposit row {txn.RowIndex} should have DepositAmount > 0");
                    Assert.Equal(0m, txn.WithdrawalAmount);
                }
                else
                {
                    Assert.True(txn.WithdrawalAmount > 0, $"Withdrawal row {txn.RowIndex} should have WithdrawalAmount > 0");
                    Assert.Equal(0m, txn.DepositAmount);
                }
            }
        }

        [Fact]
        public void Parse_Statement001_ExtractsAll433Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Statement_001.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "BANDHAN",
                DisplayName = "Bandhan Bank",
                DateFormat = "dd-MMMM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("Bandhan Bank", doc.BankName);
            Assert.Equal("20100083106505", doc.AccountNumber);
            Assert.Equal("M/S BIMLA ENTERPRISES", doc.AccountHolderName);
            Assert.Equal("BDBL0001432", doc.IfscCode);
            Assert.Equal(0.00m, doc.OpeningBalance);
            Assert.Equal(442797.14m, doc.ClosingBalance);
            Assert.Equal(433, doc.Transactions.Count);
            Assert.Equal(7928941.20m, doc.TotalDeposits);
            Assert.Equal(7486144.06m, doc.TotalWithdrawals);

            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Chronological ordering check
            var firstTxn = doc.Transactions.First();
            var lastTxn = doc.Transactions.Last();
            Assert.True(firstTxn.TransactionDate <= lastTxn.TransactionDate);
            Assert.Equal(new DateTime(2026, 6, 15), firstTxn.TransactionDate);
            Assert.Contains("Initial Funding Deposit", firstTxn.RawNarration);
            Assert.Equal(100000.00m, firstTxn.DepositAmount);
            Assert.Equal(100000.00m, firstTxn.RunningBalance);

            Assert.Equal(new DateTime(2026, 11, 13), lastTxn.TransactionDate);
            Assert.Contains("UPI/DR/D389339184174", lastTxn.RawNarration);
            Assert.Equal(16000.00m, lastTxn.WithdrawalAmount);
            Assert.Equal(442797.14m, lastTxn.RunningBalance);
        }
    }
}
