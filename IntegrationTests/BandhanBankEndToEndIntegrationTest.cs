using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using PostXML.Models;
using PostXML.Services;
using Xunit;

namespace PostXMLTest.IntegrationTests
{
    public class BandhanBankEndToEndIntegrationTest
    {
        private readonly IBankProfileRegistry _registry;
        private readonly IBankDetectorService _detector;
        private readonly IMathReconciliationEngine _reconciliationEngine;
        private readonly IBankStatementParser _parser;
        private readonly ITallyXmlExportService _exportService;

        public BandhanBankEndToEndIntegrationTest()
        {
            _registry = new BankProfileRegistry(NullLogger<BankProfileRegistry>.Instance);
            _detector = new BankDetectorService(NullLogger<BankDetectorService>.Instance, _registry);
            _reconciliationEngine = new MathReconciliationEngine(NullLogger<MathReconciliationEngine>.Instance);
            _parser = new BankStatementParser(_detector, _reconciliationEngine, NullLogger<BankStatementParser>.Instance);
            _exportService = new TallyXmlExportService(NullLogger<TallyXmlExportService>.Instance);
        }

        [Fact]
        public void BandhanBank_Statement_001_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 001 test fixture (Bandhan Bank Statement 01 Apr 2026 to 13 Nov 2026)
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Statement_001.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Expected_001.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            // 2. Parse PDF (unencrypted)
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.False(_parser.IsPdfEncrypted(stream), "PDF fixture should be detected as unencrypted.");
                doc = _parser.ParsePdf(stream);
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("Bandhan Bank", doc.BankName);
            Assert.Equal("20100083106505", doc.AccountNumber);
            Assert.Equal("M/S BIMLA ENTERPRISES", doc.AccountHolderName);
            Assert.Equal("BDBL0001432", doc.IfscCode);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(433, doc.Transactions.Count);
            Assert.Equal(0.00m, doc.OpeningBalance);
            Assert.Equal(442797.14m, doc.ClosingBalance);
            Assert.Equal(7928941.20m, doc.TotalDeposits);
            Assert.Equal(7486144.06m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled across all 433 transactions.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // Assert Line-by-Line Continuity across every single transaction
            Assert.All(doc.Transactions, txn =>
            {
                Assert.True(txn.IsBalanceMatched,
                    $"Row {txn.RowIndex} failed line-by-line balance match: Bank reported {txn.RunningBalance:F2}, Engine calculated {txn.CalculatedBalance:F2}");
            });

            // 5. Assert XML export against Gold Master
            string actualXml = _exportService.GenerateTallyXml(doc, "Bandhan Bank A/c");
            Assert.False(string.IsNullOrWhiteSpace(actualXml));

            var xDoc = XDocument.Parse(actualXml);
            var vouchers = xDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(433, vouchers.Count);

            int paymentCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Payment");
            int receiptCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Receipt");
            int contraCount = vouchers.Count(v => v.Element("VOUCHERTYPENAME")?.Value == "Contra");

            Assert.True(paymentCount > 0, "Should contain Payment vouchers");
            Assert.True(receiptCount > 0, "Should contain Receipt vouchers");
            Assert.Equal(433, paymentCount + receiptCount + contraCount);

            // Generate gold master XML if it doesn't exist yet
            if (!File.Exists(goldXmlPath))
            {
                File.WriteAllText(goldXmlPath, actualXml);
                string sourceTestDataDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestData", "BandhanBank"));
                if (Directory.Exists(sourceTestDataDir))
                {
                    File.WriteAllText(Path.Combine(sourceTestDataDir, "BandhanBank_Expected_001.xml"), actualXml);
                }
            }

            string expectedXml = File.ReadAllText(goldXmlPath);
            var expectedXDoc = XDocument.Parse(expectedXml);
            var expectedVouchers = expectedXDoc.Descendants("VOUCHER").ToList();
            Assert.Equal(expectedVouchers.Count, vouchers.Count);
        }
    }
}
