using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using PostXML.Models;
using PostXML.Services;
using Xunit;
using Xunit.Abstractions;

namespace PostXMLTest.IntegrationTests
{
    public class IciciBankEndToEndIntegrationTest
    {
        private readonly ITestOutputHelper? _output;
        private readonly IBankProfileRegistry _registry;
        private readonly IBankDetectorService _detector;
        private readonly IMathReconciliationEngine _reconciliationEngine;
        private readonly IBankStatementParser _parser;
        private readonly ITallyXmlExportService _exportService;

        public IciciBankEndToEndIntegrationTest(ITestOutputHelper? output = null)
        {
            _output = output;
            _registry = new BankProfileRegistry(NullLogger<BankProfileRegistry>.Instance);
            _detector = new BankDetectorService(NullLogger<BankDetectorService>.Instance, _registry);
            _reconciliationEngine = new MathReconciliationEngine(NullLogger<MathReconciliationEngine>.Instance);
            _parser = new BankStatementParser(_detector, _reconciliationEngine, NullLogger<BankStatementParser>.Instance);
            _exportService = new TallyXmlExportService(NullLogger<TallyXmlExportService>.Instance);
        }

        [Fact]
        public void ICICIBank_Statement_001_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 001 test fixture (Pages 3 & 4 of Statement_2026MTH10_693589079.pdf)
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_001.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Expected_001.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");
            Assert.True(File.Exists(goldXmlPath), $"Gold XML fixture not found at {goldXmlPath}");

            // 2. Parse PDF (unencrypted extracted statement pages)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF fixture should be unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("XXXXXXXX2639", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(31, doc.Transactions.Count);
            Assert.Equal(24473.06m, doc.OpeningBalance);
            Assert.Equal(49886.19m, doc.ClosingBalance);
            Assert.Equal(811387.30m, doc.TotalDeposits);
            Assert.Equal(785974.17m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // Assert Line-by-Line Continuity across every single transaction
            Assert.All(doc.Transactions, txn =>
            {
                Assert.True(txn.IsBalanceMatched,
                    $"Row {txn.RowIndex} failed line-by-line balance match: Bank reported {txn.RunningBalance:F2}, Engine calculated {txn.CalculatedBalance:F2}");
            });

            // 5. Assert XML export against Gold Master
            string actualXml = _exportService.GenerateTallyXml(doc, "ICICI Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(31, vouchers.Count);

            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(31, paymentCount + receiptCount + contraCount);

            string expectedXml = File.ReadAllText(goldXmlPath);
            var expectedXDoc = XDocument.Parse(expectedXml);
            var expectedVouchers = expectedXDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(expectedVouchers.Count, vouchers.Count);
        }

        [Fact]
        public void ICICIBank_Statement_002_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 002 test fixture (Page 3 of Statement_2026MTH10_554536541.pdf)
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_002.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Expected_002.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");
            Assert.True(File.Exists(goldXmlPath), $"Gold XML fixture not found at {goldXmlPath}");

            // 2. Parse PDF (unencrypted extracted statement pages)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF fixture should be unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("XXXXXXXX1198", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(26, doc.Transactions.Count);
            Assert.Equal(1749439.63m, doc.OpeningBalance);
            Assert.Equal(2277072.93m, doc.ClosingBalance);
            Assert.Equal(1366343.68m, doc.TotalDeposits);
            Assert.Equal(838710.38m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // Assert Line-by-Line Continuity across every single transaction
            Assert.All(doc.Transactions, txn =>
            {
                Assert.True(txn.IsBalanceMatched,
                    $"Row {txn.RowIndex} failed line-by-line balance match: Bank reported {txn.RunningBalance:F2}, Engine calculated {txn.CalculatedBalance:F2}");
            });

            // 5. Assert XML export against Gold Master
            string actualXml = _exportService.GenerateTallyXml(doc, "ICICI Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(26, vouchers.Count);

            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(26, paymentCount + receiptCount + contraCount);

            string expectedXml = File.ReadAllText(goldXmlPath);
            var expectedXDoc = XDocument.Parse(expectedXml);
            var expectedVouchers = expectedXDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(expectedVouchers.Count, vouchers.Count);
        }

        [Fact]
        public void ICICIBank_Statement_003_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 003 test fixture (OpTransactionHistory 06-11-2026 - May to Nov 2026)
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_003.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Expected_003.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");
            Assert.True(File.Exists(goldXmlPath), $"Gold XML fixture not found at {goldXmlPath}");

            // 2. Parse PDF (unencrypted NetBanking transaction history)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF fixture should be unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("162101508552", doc.AccountNumber);
            Assert.Equal("NEHA BANSAL", doc.AccountHolderName);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(166, doc.Transactions.Count);
            Assert.Equal(161737.01m, doc.OpeningBalance);
            Assert.Equal(2889.78m, doc.ClosingBalance);
            Assert.Equal(258245.00m, doc.TotalDeposits);
            Assert.Equal(417092.23m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // Assert Line-by-Line Continuity across every single transaction
            Assert.All(doc.Transactions, txn =>
            {
                Assert.True(txn.IsBalanceMatched,
                    $"Row {txn.RowIndex} failed line-by-line balance match: Bank reported {txn.RunningBalance:F2}, Engine calculated {txn.CalculatedBalance:F2}");
            });

            // 5. Assert XML export against Gold Master
            string actualXml = _exportService.GenerateTallyXml(doc, "ICICI Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(166, vouchers.Count);

            string expectedXml = File.ReadAllText(goldXmlPath);
            var expectedXDoc = XDocument.Parse(expectedXml);
            var expectedVouchers = expectedXDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(expectedVouchers.Count, vouchers.Count);
        }

        [Fact]
        public void ICICIBank_Statement_004_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 004 test fixture (OpTransactionHistory CY 2025)
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_004.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Expected_004.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");
            Assert.True(File.Exists(goldXmlPath), $"Gold XML fixture not found at {goldXmlPath}");

            // 2. Parse PDF (unencrypted NetBanking transaction history)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF fixture should be unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("162101508552", doc.AccountNumber);
            Assert.Equal("NEHA BANSAL", doc.AccountHolderName);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(88, doc.Transactions.Count);
            Assert.Equal(19310.32m, doc.OpeningBalance);
            Assert.Equal(8137.51m, doc.ClosingBalance);
            Assert.Equal(327516.44m, doc.TotalDeposits);
            Assert.Equal(338689.25m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // Assert Line-by-Line Continuity across every single transaction
            Assert.All(doc.Transactions, txn =>
            {
                Assert.True(txn.IsBalanceMatched,
                    $"Row {txn.RowIndex} failed line-by-line balance match: Bank reported {txn.RunningBalance:F2}, Engine calculated {txn.CalculatedBalance:F2}");
            });

            // 5. Assert XML export against Gold Master
            string actualXml = _exportService.GenerateTallyXml(doc, "ICICI Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(88, vouchers.Count);

            string expectedXml = File.ReadAllText(goldXmlPath);
            var expectedXDoc = XDocument.Parse(expectedXml);
            var expectedVouchers = expectedXDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(expectedVouchers.Count, vouchers.Count);
        }

        [Fact]
        public void ICICIBank_Statement_005_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 005 test fixture (OpTransactionHistory FY 2025-26)
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Statement_005.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "ICICIBank", "ICICIBank_Expected_005.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");
            Assert.True(File.Exists(goldXmlPath), $"Gold XML fixture not found at {goldXmlPath}");

            // 2. Parse PDF (unencrypted NetBanking transaction history)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF fixture should be unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("ICICI Bank", doc.BankName);
            Assert.Equal("162101508552", doc.AccountNumber);
            Assert.Equal("NEHA BANSAL", doc.AccountHolderName);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(86, doc.Transactions.Count);
            Assert.Equal(191710.27m, doc.OpeningBalance);
            Assert.Equal(165490.00m, doc.ClosingBalance);
            Assert.Equal(321974.57m, doc.TotalDeposits);
            Assert.Equal(348194.84m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // Assert Line-by-Line Continuity across every single transaction
            Assert.All(doc.Transactions, txn =>
            {
                Assert.True(txn.IsBalanceMatched,
                    $"Row {txn.RowIndex} failed line-by-line balance match: Bank reported {txn.RunningBalance:F2}, Engine calculated {txn.CalculatedBalance:F2}");
            });

            // 5. Assert XML export against Gold Master
            string actualXml = _exportService.GenerateTallyXml(doc, "ICICI Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(86, vouchers.Count);

            string expectedXml = File.ReadAllText(goldXmlPath);
            var expectedXDoc = XDocument.Parse(expectedXml);
            var expectedVouchers = expectedXDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(expectedVouchers.Count, vouchers.Count);
        }
    }
}
