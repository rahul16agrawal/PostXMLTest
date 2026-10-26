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
    public class AxisBankEndToEndIntegrationTest
    {
        private readonly ITestOutputHelper? _output;
        private readonly IBankProfileRegistry _registry;
        private readonly IBankDetectorService _detector;
        private readonly IMathReconciliationEngine _reconciliationEngine;
        private readonly IBankStatementParser _parser;
        private readonly ITallyXmlExportService _exportService;

        public AxisBankEndToEndIntegrationTest(ITestOutputHelper? output = null)
        {
            _output = output;
            _registry = new BankProfileRegistry(NullLogger<BankProfileRegistry>.Instance);
            _detector = new BankDetectorService(NullLogger<BankDetectorService>.Instance, _registry);
            _reconciliationEngine = new MathReconciliationEngine(NullLogger<MathReconciliationEngine>.Instance);
            _parser = new BankStatementParser(_detector, _reconciliationEngine, NullLogger<BankStatementParser>.Instance);
            _exportService = new TallyXmlExportService(NullLogger<TallyXmlExportService>.Instance);
        }

        [Fact]
        public void AxisBank_Statement_001_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 001 test fixture
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "AxisBank", "AxisBank_Statement_001.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "AxisBank", "AxisBank_Expected_001.xml");

            // Also find the source repo TestData folder to persist gold master
            string projectRoot = Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));
            string sourceXmlPath = Path.Combine(projectRoot, "TestData", "AxisBank", "AxisBank_Expected_001.xml");

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
            Assert.Equal("Axis Bank", doc.BankName);
            Assert.Equal("924020049955809", doc.AccountNumber);
            Assert.Equal("UTIB0001075", doc.IfscCode);
            Assert.Equal("GLOBAL AGRO", doc.AccountHolderName);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(89, doc.Transactions.Count);
            Assert.Equal(12795.00m, doc.OpeningBalance);
            Assert.Equal(42672.10m, doc.ClosingBalance);
            Assert.Equal(9664156.00m, doc.TotalDeposits);
            Assert.Equal(9634278.90m, doc.TotalWithdrawals);

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
            string actualXml = _exportService.GenerateTallyXml(doc, "Axis Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(89, vouchers.Count);

            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(89, paymentCount + receiptCount + contraCount);

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
