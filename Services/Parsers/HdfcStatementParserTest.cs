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

        [Fact]
        public void Parse_Statement004_ExtractsAllTransactionsAndReconciles()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_004.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "HDFC",
                DisplayName = "HDFC Bank",
                DateFormat = "dd/MM/yy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(stream, new ParsingOptions { Password = "253526429" });

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("50200080140208", doc.AccountNumber);
            Assert.Equal(196, doc.Transactions.Count);
            Assert.Equal(226624.06m, doc.OpeningBalance);
            Assert.Equal(9220.87m, doc.ClosingBalance);

            var engine = new PostXML.Services.MathReconciliationEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<PostXML.Services.MathReconciliationEngine>.Instance);
            var summary = engine.Reconcile(doc);
            Assert.True(summary.IsFullyReconciled);
            Assert.Equal(0m, summary.DiscrepancyAmount);
        }

        [Fact]
        public void Parse_Statement005_ExtractsAllTransactionsAndReconciles()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_005.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "HDFC",
                DisplayName = "HDFC Bank",
                DateFormat = "dd/MM/yy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(stream, new ParsingOptions { Password = "187182423" });

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("59209661713385", doc.AccountNumber);
            Assert.Equal(426, doc.Transactions.Count);
            Assert.Equal(33182.26m, doc.OpeningBalance);
            Assert.Equal(254753.00m, doc.ClosingBalance);

            var engine = new PostXML.Services.MathReconciliationEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<PostXML.Services.MathReconciliationEngine>.Instance);
            var summary = engine.Reconcile(doc);
            Assert.True(summary.IsFullyReconciled);
            Assert.Equal(0m, summary.DiscrepancyAmount);
            Assert.Empty(summary.MismatchedRowIndices);
        }

        #region Intra-Day Running Balance Reordering Tests

        [Fact]
        public void ReorderIntraDayByRunningBalance_EmptyList_ReturnsEmptyList()
        {
            var empty = new List<BankTransaction>();
            var result = HdfcStatementParser.ReorderIntraDayByRunningBalance(empty, 1000m);
            Assert.Empty(result);
        }

        [Fact]
        public void ReorderIntraDayByRunningBalance_SingleTransaction_ReturnsSameTransaction()
        {
            var single = new List<BankTransaction>
            {
                new BankTransaction
                {
                    TransactionDate = new DateTime(2026, 5, 1),
                    DepositAmount = 500m,
                    WithdrawalAmount = 0m,
                    RunningBalance = 1500m
                }
            };

            var result = HdfcStatementParser.ReorderIntraDayByRunningBalance(single, 1000m);
            Assert.Single(result);
            Assert.Equal(1500m, result[0].RunningBalance);
        }

        [Fact]
        public void ReorderIntraDayByRunningBalance_ScrambledSameDayTransactions_RestoresContinuousChain()
        {
            // Opening Balance: 10,000
            // Txn 1: Dep 5,000 -> Running: 15,000
            // Txn 2: Wdl 3,000 -> Running: 12,000
            // Txn 3: Dep 2,000 -> Running: 14,000
            var t1 = new BankTransaction { RawNarration = "T1", TransactionDate = new DateTime(2026, 5, 10), DepositAmount = 5000m, WithdrawalAmount = 0m, RunningBalance = 15000m };
            var t2 = new BankTransaction { RawNarration = "T2", TransactionDate = new DateTime(2026, 5, 10), DepositAmount = 0m, WithdrawalAmount = 3000m, RunningBalance = 12000m };
            var t3 = new BankTransaction { RawNarration = "T3", TransactionDate = new DateTime(2026, 5, 10), DepositAmount = 2000m, WithdrawalAmount = 0m, RunningBalance = 14000m };

            // Provide them scrambled: T3, T1, T2
            var scrambled = new List<BankTransaction> { t3, t1, t2 };

            var result = HdfcStatementParser.ReorderIntraDayByRunningBalance(scrambled, 10000m);

            Assert.Equal(3, result.Count);
            Assert.Equal("T1", result[0].RawNarration);
            Assert.Equal("T2", result[1].RawNarration);
            Assert.Equal("T3", result[2].RawNarration);
            Assert.Equal(15000m, result[0].RunningBalance);
            Assert.Equal(12000m, result[1].RunningBalance);
            Assert.Equal(14000m, result[2].RunningBalance);
        }

        [Fact]
        public void ReorderIntraDayByRunningBalance_MultipleSameDayWithdrawalsAndDeposits_OrdersAccurately()
        {
            // Opening Balance: 50,000
            // A: Wdl 10,000 -> 40,000
            // B: Wdl 5,000  -> 35,000
            // C: Dep 25,000 -> 60,000
            // D: Wdl 8,000  -> 52,000
            var ta = new BankTransaction { RawNarration = "A", TransactionDate = new DateTime(2026, 6, 1), WithdrawalAmount = 10000m, DepositAmount = 0m, RunningBalance = 40000m };
            var tb = new BankTransaction { RawNarration = "B", TransactionDate = new DateTime(2026, 6, 1), WithdrawalAmount = 5000m, DepositAmount = 0m, RunningBalance = 35000m };
            var tc = new BankTransaction { RawNarration = "C", TransactionDate = new DateTime(2026, 6, 1), WithdrawalAmount = 0m, DepositAmount = 25000m, RunningBalance = 60000m };
            var td = new BankTransaction { RawNarration = "D", TransactionDate = new DateTime(2026, 6, 1), WithdrawalAmount = 8000m, DepositAmount = 0m, RunningBalance = 52000m };

            // Scrambled input: D, B, C, A
            var scrambled = new List<BankTransaction> { td, tb, tc, ta };

            var result = HdfcStatementParser.ReorderIntraDayByRunningBalance(scrambled, 50000m);

            Assert.Equal(4, result.Count);
            Assert.Equal(new[] { "A", "B", "C", "D" }, result.Select(r => r.RawNarration).ToArray());
        }

        [Fact]
        public void ReorderIntraDayByRunningBalance_MultipleDays_PreservesDayBoundariesAndReordersWithinDays()
        {
            // Day 1 (2026-06-01): Opening 1000
            // D1_1: Dep 500 -> 1500
            // D1_2: Wdl 200 -> 1300
            var d1_1 = new BankTransaction { RawNarration = "D1_1", TransactionDate = new DateTime(2026, 6, 1), DepositAmount = 500m, WithdrawalAmount = 0m, RunningBalance = 1500m };
            var d1_2 = new BankTransaction { RawNarration = "D1_2", TransactionDate = new DateTime(2026, 6, 1), DepositAmount = 0m, WithdrawalAmount = 200m, RunningBalance = 1300m };

            // Day 2 (2026-06-02): Opening 1300
            // D2_1: Dep 1000 -> 2300
            // D2_2: Wdl 800  -> 1500
            var d2_1 = new BankTransaction { RawNarration = "D2_1", TransactionDate = new DateTime(2026, 6, 2), DepositAmount = 1000m, WithdrawalAmount = 0m, RunningBalance = 2300m };
            var d2_2 = new BankTransaction { RawNarration = "D2_2", TransactionDate = new DateTime(2026, 6, 2), DepositAmount = 0m, WithdrawalAmount = 800m, RunningBalance = 1500m };

            // Scramble within each day
            var input = new List<BankTransaction> { d1_2, d1_1, d2_2, d2_1 };

            var result = HdfcStatementParser.ReorderIntraDayByRunningBalance(input, 1000m);

            Assert.Equal(4, result.Count);
            Assert.Equal(new[] { "D1_1", "D1_2", "D2_1", "D2_2" }, result.Select(r => r.RawNarration).ToArray());
        }

        [Fact]
        public void ReorderIntraDayByRunningBalance_ZeroRunningBalance_FallsBackSafely()
        {
            // When candidate has RunningBalance == 0, fallback branch advances current balance by deposit/withdrawal
            var t1 = new BankTransaction { RawNarration = "Unbalanced1", TransactionDate = new DateTime(2026, 7, 1), DepositAmount = 200m, WithdrawalAmount = 0m, RunningBalance = 0m };
            var t2 = new BankTransaction { RawNarration = "Unbalanced2", TransactionDate = new DateTime(2026, 7, 1), DepositAmount = 0m, WithdrawalAmount = 50m, RunningBalance = 0m };

            var input = new List<BankTransaction> { t1, t2 };

            var result = HdfcStatementParser.ReorderIntraDayByRunningBalance(input, 500m);

            Assert.Equal(2, result.Count);
            Assert.Equal("Unbalanced1", result[0].RawNarration);
            Assert.Equal("Unbalanced2", result[1].RawNarration);
        }

        [Fact]
        public void ReorderIntraDayByRunningBalance_EpsilonTolerance_MatchesWithinOneCent()
        {
            // Opening: 100.00
            // Txn has running balance 150.003 (less than 0.01 discrepancy from 150.00)
            var t1 = new BankTransaction { RawNarration = "Epsilon", TransactionDate = new DateTime(2026, 7, 1), DepositAmount = 50m, WithdrawalAmount = 0m, RunningBalance = 150.004m };
            var input = new List<BankTransaction> { t1 };

            var result = HdfcStatementParser.ReorderIntraDayByRunningBalance(input, 100.00m);

            Assert.Single(result);
            Assert.Equal("Epsilon", result[0].RawNarration);
        }

        #endregion
    }
}
