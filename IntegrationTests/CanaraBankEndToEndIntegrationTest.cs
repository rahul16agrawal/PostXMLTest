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
    public class CanaraBankEndToEndIntegrationTest
    {
        private readonly IBankProfileRegistry _registry;
        private readonly IBankDetectorService _detector;
        private readonly IMathReconciliationEngine _reconciliationEngine;
        private readonly IBankStatementParser _parser;
        private readonly ITallyXmlExportService _exportService;

        public CanaraBankEndToEndIntegrationTest()
        {
            _registry = new BankProfileRegistry(NullLogger<BankProfileRegistry>.Instance);
            _detector = new BankDetectorService(NullLogger<BankDetectorService>.Instance, _registry);
            _reconciliationEngine = new MathReconciliationEngine(NullLogger<MathReconciliationEngine>.Instance);
            _parser = new BankStatementParser(_detector, _reconciliationEngine, NullLogger<BankStatementParser>.Instance);
            _exportService = new TallyXmlExportService(NullLogger<TallyXmlExportService>.Instance);
        }

        [Fact]
        public void CanaraBank_Statement_001_EndToEnd_Reconciles_And_Matches_GoldMasterXml()
        {
            // 1. Locate the Statement 001 test fixture
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "CanaraBank", "CanaraBank_Statement_001.pdf");
            string goldXmlPath = Path.Combine(basePath, "TestData", "CanaraBank", "CanaraBank_Expected_001.xml");

            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");
            Assert.True(File.Exists(goldXmlPath), $"Gold XML fixture not found at {goldXmlPath}");

            // 2. Parse encrypted PDF with password '0463'
            StatementDocument doc;
            using (var stream = File.OpenRead(pdfPath))
            {
                Assert.True(_parser.IsPdfEncrypted(stream), "PDF should be detected as encrypted.");
                doc = _parser.ParsePdf(stream, "0463");
            }

            // 3. Assert Metadata & Header Detection
            Assert.NotNull(doc);
            Assert.Equal("Canara Bank", doc.BankName);
            Assert.Equal("2316261020463", doc.AccountNumber);

            // 4. Assert Transaction Volume & Mathematical Continuity
            Assert.Equal(683, doc.Transactions.Count);
            Assert.Equal(-3054604.43m, doc.OpeningBalance);
            Assert.Equal(-3714745.81m, doc.ClosingBalance);
            Assert.Equal(6105928.20m, doc.TotalDeposits);
            Assert.Equal(6766069.58m, doc.TotalWithdrawals);

            // Mathematical Balance Equation: Opening + Deposits - Withdrawals == Closing
            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Reconciliation Engine State
            Assert.True(doc.Reconciliation.IsFullyReconciled, "Statement must be 100% mathematically reconciled.");
            Assert.Equal(0m, doc.Reconciliation.DiscrepancyAmount);
            Assert.Empty(doc.Reconciliation.MismatchedRowIndices);

            // 5. Assert Key Reversal & POS Classification Edge Cases
            // Row 584: REV:EXCESS SMS SC ENTRY DATED - 20-SEP-26
            var row584 = doc.Transactions[583];
            Assert.Equal(TransactionType.Deposit, row584.Type);
            Assert.Equal(178.76m, row584.DepositAmount);
            Assert.Equal(0m, row584.WithdrawalAmount);

            // Row 585: REV:EXCESS SMS SC ENTRY DATED - 20-SEP-26
            var row585 = doc.Transactions[584];
            Assert.Equal(TransactionType.Deposit, row585.Type);
            Assert.Equal(39.24m, row585.DepositAmount);
            Assert.Equal(0m, row585.WithdrawalAmount);

            // Row 586: POS_PMNT_10061547_22092026
            var row586 = doc.Transactions[585];
            Assert.Equal(TransactionType.Deposit, row586.Type);
            Assert.Equal(99.60m, row586.DepositAmount);
            Assert.Equal(0m, row586.WithdrawalAmount);

            // 6. Generate Tally Prime XML
            string generatedXml = _exportService.GenerateTallyXml(doc, "Canara Bank");
            Assert.False(string.IsNullOrWhiteSpace(generatedXml));

            var xdoc = XDocument.Parse(generatedXml);
            var vouchers = xdoc.Descendants("VOUCHER").ToList();

            Assert.Equal(683, vouchers.Count);

            int receiptCount = vouchers.Count(v => (string?)v.Attribute("VCHTYPE") == "Receipt");
            int paymentCount = vouchers.Count(v => (string?)v.Attribute("VCHTYPE") == "Payment");

            Assert.Equal(586, receiptCount);
            Assert.Equal(97, paymentCount);

            // Assert Bank Ledger Master definition
            var canaraLedger = xdoc.Descendants("LEDGER").FirstOrDefault(l => (string?)l.Attribute("NAME") == "Canara Bank");
            Assert.NotNull(canaraLedger);
            Assert.Null(canaraLedger!.Element("OPENINGBALANCE"));

            // 7. Verify Gold Master Conformance
            var goldDoc = XDocument.Parse(File.ReadAllText(goldXmlPath));
            var goldVouchers = goldDoc.Descendants("VOUCHER").ToList();

            Assert.Equal(goldVouchers.Count, vouchers.Count);

            // Verify voucher type sequence matches Gold Master 100% across all 683 rows
            for (int i = 0; i < vouchers.Count; i++)
            {
                string? genType = (string?)vouchers[i].Attribute("VCHTYPE");
                string? goldType = (string?)goldVouchers[i].Attribute("VCHTYPE");
                Assert.Equal(goldType, genType);
            }
        }
    }
}
