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
    public class HdfcBankEndToEndIntegrationTest
    {
        private readonly ITestOutputHelper? _output;
        private readonly IBankProfileRegistry _registry;
        private readonly IBankDetectorService _detector;
        private readonly IMathReconciliationEngine _reconciliationEngine;
        private readonly IBankStatementParser _parser;
        private readonly ITallyXmlExportService _exportService;

        public HdfcBankEndToEndIntegrationTest(ITestOutputHelper? output = null)
        {
            _output = output;
            _registry = new BankProfileRegistry(NullLogger<BankProfileRegistry>.Instance);
            _detector = new BankDetectorService(NullLogger<BankDetectorService>.Instance, _registry);
            _reconciliationEngine = new MathReconciliationEngine(NullLogger<MathReconciliationEngine>.Instance);
            _parser = new BankStatementParser(_detector, _reconciliationEngine, NullLogger<BankStatementParser>.Instance);
            _exportService = new TallyXmlExportService(NullLogger<TallyXmlExportService>.Instance);
        }

        [Fact]
        public void HdfcBank_Print_LineByLine_Reconciliation()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_001.pdf");

            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                doc = _parser.ParsePdf(stream);
            }

            _output?.WriteLine($"| Row | Date | Description | Debit (Dr) | Credit (Cr) | Bank Balance | Engine Balance | Status |");
            _output?.WriteLine($"|---|---|---|---|---|---|---|---|");

            foreach (var txn in doc.Transactions)
            {
                string status = txn.IsBalanceMatched ? "MATCHED" : "MISMATCH";
                string desc = txn.RawNarration.Length > 35 ? txn.RawNarration.Substring(0, 32) + "..." : txn.RawNarration;
                _output?.WriteLine($"| {txn.RowIndex:D3} | {txn.TransactionDate:dd-MMM-yyyy} | {desc} | {txn.WithdrawalAmount:N2} | {txn.DepositAmount:N2} | {txn.RunningBalance:N2} | {txn.CalculatedBalance:N2} | {status} |");
            }
        }

        [Fact]
        public void HdfcBank_Statement_001_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 001 test fixture
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_001.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Expected_001.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            // 2. Parse PDF (unencrypted)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF should be unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("HDFC Bank", doc.BankName);
            Assert.Equal("50200120952504", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(37, doc.Transactions.Count);
            Assert.Equal(136368.54m, doc.OpeningBalance);
            Assert.Equal(101324.54m, doc.ClosingBalance);
            Assert.Equal(673000.00m, doc.TotalDeposits);
            Assert.Equal(708044.00m, doc.TotalWithdrawals);

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

            // 5. Generate Gold Master XML if not present, then assert XML validity
            string actualXml = _exportService.GenerateTallyXml(doc, "HDFC Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(37, vouchers.Count);

            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(37, paymentCount + receiptCount + contraCount);

            if (!File.Exists(goldXmlPath))
            {
                File.WriteAllText(goldXmlPath, actualXml);
            }

            // Compare against Gold Master XML
            string expectedXml = File.ReadAllText(goldXmlPath);
            Assert.Equal(expectedXml.Trim(), actualXml.Trim());
        }

        [Fact]
        public void HdfcBank_Statement_002_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 002 test fixture (Jai International)
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_002.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Expected_002.xml");

            // Also find the source repo TestData folder to persist gold master if missing
            string projectRoot = Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));
            string sourceXmlPath = Path.Combine(projectRoot, "TestData", "HDFCBank", "HDFCBank_Expected_002.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            // 2. Parse PDF (unencrypted)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF should be unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("HDFC Bank", doc.BankName);
            Assert.Equal("50200037586423", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(162, doc.Transactions.Count);
            Assert.Equal(0.00m, doc.OpeningBalance);
            Assert.Equal(1200042.80m, doc.ClosingBalance);
            Assert.Equal(11460030.40m, doc.TotalDeposits);
            Assert.Equal(10259987.60m, doc.TotalWithdrawals);

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

            // 5. Generate Gold Master XML if not present, then assert XML validity
            string actualXml = _exportService.GenerateTallyXml(doc, "HDFC Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(162, vouchers.Count);

            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(162, paymentCount + receiptCount + contraCount);

            if (!File.Exists(goldXmlPath))
            {
                File.WriteAllText(goldXmlPath, actualXml);
            }
            if (Directory.Exists(Path.GetDirectoryName(sourceXmlPath)) && !File.Exists(sourceXmlPath))
            {
                File.WriteAllText(sourceXmlPath, actualXml);
            }

            // Compare against Gold Master XML
            string expectedXml = File.ReadAllText(goldXmlPath);
            Assert.Equal(expectedXml.Trim(), actualXml.Trim());
        }

        [Fact]
        public void HdfcBank_Statement_003_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 003 test fixture (Hanuman)
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Statement_003.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "HDFCBank", "HDFCBank_Expected_003.xml");

            // Also find the source repo TestData folder to persist gold master if missing
            string projectRoot = Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));
            string sourceXmlPath = Path.Combine(projectRoot, "TestData", "HDFCBank", "HDFCBank_Expected_003.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            // 2. Parse PDF (unencrypted)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF should be unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("HDFC Bank", doc.BankName);
            Assert.Equal("50200075012327", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(1001, doc.Transactions.Count);
            Assert.Equal(3269.83m, doc.OpeningBalance);
            Assert.Equal(11262.63m, doc.ClosingBalance);
            Assert.Equal(55241375.00m, doc.TotalDeposits);
            Assert.Equal(55233382.20m, doc.TotalWithdrawals);

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

            // 5. Generate Gold Master XML if not present, then assert XML validity
            string actualXml = _exportService.GenerateTallyXml(doc, "HDFC Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(1001, vouchers.Count);

            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(1001, paymentCount + receiptCount + contraCount);

            if (!File.Exists(goldXmlPath))
            {
                File.WriteAllText(goldXmlPath, actualXml);
            }
            if (Directory.Exists(Path.GetDirectoryName(sourceXmlPath)) && !File.Exists(sourceXmlPath))
            {
                File.WriteAllText(sourceXmlPath, actualXml);
            }

            // Compare against Gold Master XML
            string expectedXml = File.ReadAllText(goldXmlPath);
            Assert.Equal(expectedXml.Trim(), actualXml.Trim());
        }
    }
}
