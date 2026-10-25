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
    public class IdbiBankEndToEndIntegrationTest
    {
        private readonly ITestOutputHelper? _output;
        private readonly IBankProfileRegistry _registry;
        private readonly IBankDetectorService _detector;
        private readonly IMathReconciliationEngine _reconciliationEngine;
        private readonly IBankStatementParser _parser;
        private readonly ITallyXmlExportService _exportService;

        public IdbiBankEndToEndIntegrationTest(ITestOutputHelper? output = null)
        {
            _output = output;
            _registry = new BankProfileRegistry(NullLogger<BankProfileRegistry>.Instance);
            _detector = new BankDetectorService(NullLogger<BankDetectorService>.Instance, _registry);
            _reconciliationEngine = new MathReconciliationEngine(NullLogger<MathReconciliationEngine>.Instance);
            _parser = new BankStatementParser(_detector, _reconciliationEngine, NullLogger<BankStatementParser>.Instance);
            _exportService = new TallyXmlExportService(NullLogger<TallyXmlExportService>.Instance);
        }

        [Fact]
        public void IdbiBank_Print_LineByLine_Reconciliation()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_001.pdf");

            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                doc = _parser.ParsePdf(stream);
            }

            _output?.WriteLine($"Total transactions: {doc.Transactions.Count}");
            _output?.WriteLine($"| Row | Date | Description | Debit (Dr) | Credit (Cr) | Bank Balance | Engine Balance | Status |");
            _output?.WriteLine($"|---|---|---|---|---|---|---|---|");

            // Print first 10 and last 10 rows
            var sampleRows = doc.Transactions.Take(10).Concat(doc.Transactions.TakeLast(10));
            foreach (var txn in sampleRows)
            {
                string status = txn.IsBalanceMatched ? "MATCHED" : "MISMATCH";
                string desc = txn.RawNarration.Length > 35 ? txn.RawNarration.Substring(0, 32) + "..." : txn.RawNarration;
                _output?.WriteLine($"| {txn.RowIndex:D4} | {txn.TransactionDate:dd/MM/yyyy} | {desc} | {txn.WithdrawalAmount:N2} | {txn.DepositAmount:N2} | {txn.RunningBalance:N2} | {txn.CalculatedBalance:N2} | {status} |");
            }
        }

        [Fact]
        public void IdbiBank_Statement_001_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 001 test fixture
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Statement_001.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "IDBIBank", "IDBIBank_Expected_001.xml");

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
            Assert.Equal("IDBI Bank", doc.BankName);
            Assert.Equal("1075102000012148", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(1442, doc.Transactions.Count);
            Assert.Equal(25751.95m, doc.OpeningBalance);
            Assert.Equal(439063.01m, doc.ClosingBalance);
            Assert.Equal(19508676.00m, doc.TotalDeposits);
            Assert.Equal(19095364.94m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled across all 1,442 transactions.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // Assert Line-by-Line Continuity across every single transaction
            Assert.All(doc.Transactions, txn =>
            {
                Assert.True(txn.IsBalanceMatched,
                    $"Row {txn.RowIndex} failed line-by-line balance match: Bank reported {txn.RunningBalance:F2}, Engine calculated {txn.CalculatedBalance:F2}");
            });

            // 5. Generate Gold Master XML if not present, then assert XML validity
            string actualXml = _exportService.GenerateTallyXml(doc, "IDBI Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(1442, vouchers.Count);

            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(1442, paymentCount + receiptCount + contraCount);

            if (!File.Exists(goldXmlPath))
            {
                File.WriteAllText(goldXmlPath, actualXml);
            }

            // Compare against Gold Master XML
            string expectedXml = File.ReadAllText(goldXmlPath);
            Assert.Equal(expectedXml.Trim(), actualXml.Trim());
        }
    }
}
