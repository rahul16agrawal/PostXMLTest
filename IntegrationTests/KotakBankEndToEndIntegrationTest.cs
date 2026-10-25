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
    public class KotakBankEndToEndIntegrationTest
    {
        private readonly ITestOutputHelper? _output;
        private readonly IBankProfileRegistry _registry;
        private readonly IBankDetectorService _detector;
        private readonly IMathReconciliationEngine _reconciliationEngine;
        private readonly IBankStatementParser _parser;
        private readonly ITallyXmlExportService _exportService;

        public KotakBankEndToEndIntegrationTest(ITestOutputHelper? output = null)
        {
            _output = output;
            _registry = new BankProfileRegistry(NullLogger<BankProfileRegistry>.Instance);
            _detector = new BankDetectorService(NullLogger<BankDetectorService>.Instance, _registry);
            _reconciliationEngine = new MathReconciliationEngine(NullLogger<MathReconciliationEngine>.Instance);
            _parser = new BankStatementParser(_detector, _reconciliationEngine, NullLogger<BankStatementParser>.Instance);
            _exportService = new TallyXmlExportService(NullLogger<TallyXmlExportService>.Instance);
        }

        [Fact]
        public void KotakBank_Print_LineByLine_Reconciliation()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "KotakBank", "KotakBank_Statement_001.pdf");

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
        public void KotakBank_Statement_001_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 001 test fixture
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "KotakBank", "KotakBank_Statement_001.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "KotakBank", "KotakBank_Expected_001.xml");

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
            Assert.Equal("Kotak Mahindra Bank", doc.BankName);
            Assert.Equal("5648153064", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(115, doc.Transactions.Count);
            Assert.Equal(349.24m, doc.OpeningBalance);
            Assert.Equal(1.00m, doc.ClosingBalance);
            Assert.Equal(649803.25m, doc.TotalDeposits);
            Assert.Equal(650151.49m, doc.TotalWithdrawals);

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

            // 5. Generate Tally XML & Compare against Gold Master
            string generatedXml = _exportService.GenerateTallyXml(doc, "Kotak Mahindra Bank Ledger");

            Assert.False(string.IsNullOrWhiteSpace(generatedXml), "Generated Tally XML should not be empty.");

            // If Gold XML doesn't exist yet, save it as baseline
            if (!File.Exists(goldXmlPath))
            {
                File.WriteAllText(goldXmlPath, generatedXml);
            }

            // Verify XML schema structure
            var xDoc = XDocument.Parse(generatedXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(115, vouchers.Count);

            // Verify Voucher Types (Payment vs Receipt vs Contra)
            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(115, paymentCount + receiptCount + contraCount);

            // Verify Gold Master exact match
            string goldXml = File.ReadAllText(goldXmlPath);
            Assert.Equal(goldXml.Trim(), generatedXml.Trim());
        }
    }
}
