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
    public class SbiBankEndToEndIntegrationTest
    {
        private readonly IBankProfileRegistry _registry;
        private readonly IBankDetectorService _detector;
        private readonly IMathReconciliationEngine _reconciliationEngine;
        private readonly IBankStatementParser _parser;
        private readonly ITallyXmlExportService _exportService;

        public SbiBankEndToEndIntegrationTest()
        {
            _registry = new BankProfileRegistry(NullLogger<BankProfileRegistry>.Instance);
            _detector = new BankDetectorService(NullLogger<BankDetectorService>.Instance, _registry);
            _reconciliationEngine = new MathReconciliationEngine(NullLogger<MathReconciliationEngine>.Instance);
            _parser = new BankStatementParser(_detector, _reconciliationEngine, NullLogger<BankStatementParser>.Instance);
            _exportService = new TallyXmlExportService(NullLogger<TallyXmlExportService>.Instance);
        }

        [Fact]
        public void SbiBank_Statement_001_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 001 test fixture
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "SBIBank", "SBIBank_Statement_001.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "SBIBank", "SBIBank_Expected_001.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");
            Assert.True(File.Exists(goldXmlPath), $"Gold XML fixture not found at {goldXmlPath}");

            // 2. Parse encrypted PDF with password '72218191'
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.True(_parser.IsPdfEncrypted(stream), "PDF should be detected as encrypted.");
                doc = _parser.ParsePdf(stream, "72218191");
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("State Bank of India", doc.BankName);
            Assert.Equal("00000043463737221", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(90, doc.Transactions.Count);
            Assert.Equal(688560.21m, doc.OpeningBalance);
            Assert.Equal(1523.54m, doc.ClosingBalance);
            Assert.Equal(355364.33m, doc.TotalDeposits);
            Assert.Equal(1042401.00m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // 5. Assert Key Transactions
            // Row 1: Test transfer TO CIAANNSIR8
            var row1 = doc.Transactions[0];
            Assert.Equal(TransactionType.Withdrawal, row1.Type);
            Assert.Equal(1.00m, row1.WithdrawalAmount);
            Assert.Equal(0m, row1.DepositAmount);
            Assert.Equal(688559.21m, row1.RunningBalance);

            // Row 2: DEBIT limit utilisation 6,88,000.00
            var row2 = doc.Transactions[1];
            Assert.Equal(TransactionType.Withdrawal, row2.Type);
            Assert.Equal(688000.00m, row2.WithdrawalAmount);
            Assert.Equal(0m, row2.DepositAmount);
            Assert.Equal(559.21m, row2.RunningBalance);

            // Row 3: BY TRANSFER IMPS 4,400.00
            var row3 = doc.Transactions[2];
            Assert.Equal(TransactionType.Deposit, row3.Type);
            Assert.Equal(4400.00m, row3.DepositAmount);
            Assert.Equal(0m, row3.WithdrawalAmount);
            Assert.Equal(4959.21m, row3.RunningBalance);

            // Final Row 90: BY TRANSFER NEFT 650.00
            var row90 = doc.Transactions[89];
            Assert.Equal(TransactionType.Deposit, row90.Type);
            Assert.Equal(650.00m, row90.DepositAmount);
            Assert.Equal(0m, row90.WithdrawalAmount);
            Assert.Equal(1523.54m, row90.RunningBalance);

            // 6. Generate Tally Prime XML
            string generatedXml = _exportService.GenerateTallyXml(doc, "State Bank of India");
            Assert.False(string.IsNullOrWhiteSpace(generatedXml));

            var xdoc = XDocument.Parse(generatedXml);
            var vouchers = xdoc.Descendants("VOUCHER").ToList();

            Assert.Equal(90, vouchers.Count);

            int receiptCount = vouchers.Count(v => (string?)v.Attribute("VCHTYPE") == "Receipt");
            int paymentCount = vouchers.Count(v => (string?)v.Attribute("VCHTYPE") == "Payment");

            Assert.Equal(65, receiptCount);
            Assert.Equal(25, paymentCount);

            // 7. Verify Gold Master Conformance
            var goldDoc = XDocument.Parse(File.ReadAllText(goldXmlPath));
            var goldVouchers = goldDoc.Descendants("VOUCHER").ToList();

            Assert.Equal(goldVouchers.Count, vouchers.Count);

            // Verify voucher type sequence matches Gold Master 100% across all 90 rows
            for (int i = 0; i < vouchers.Count; i++)
            {
                string? genType = (string?)vouchers[i].Attribute("VCHTYPE");
                string? goldType = (string?)goldVouchers[i].Attribute("VCHTYPE");
                Assert.Equal(goldType, genType);
            }
        }
    }
}
