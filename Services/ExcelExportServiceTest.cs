using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PostXML.Models;
using PostXML.Services;
using Xunit;

namespace PostXMLTest.Services
{
    public class ExcelExportServiceTest
    {
        private readonly ExcelExportService _service;

        public ExcelExportServiceTest()
        {
            _service = new ExcelExportService(NullLogger<ExcelExportService>.Instance);
        }

        [Fact]
        public void GenerateExcel_WithValidStatement_ReturnsValidOpenXmlBytes()
        {
            var statement = new StatementDocument
            {
                BankName = "Canara Bank",
                AccountNumber = "120001090522",
                OpeningBalance = 3054604.43m,
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        RowIndex = 1,
                        TransactionDate = new DateTime(2026, 8, 1),
                        RawNarration = "NEFT CR-SBIN426212493456-MAA BHAWANI & SONS <CO>",
                        ReferenceNumber = "SBIN426212493456",
                        Type = TransactionType.Deposit,
                        DepositAmount = 25000m,
                        WithdrawalAmount = 0m,
                        RunningBalance = 3079604.43m,
                        TargetLedgerName = "Suspense A/c"
                    },
                    new BankTransaction
                    {
                        RowIndex = 2,
                        TransactionDate = new DateTime(2026, 8, 2),
                        RawNarration = "MB-IMPS-DR/GOLDMEDAL ELECT/621317206825",
                        ReferenceNumber = "621317206825",
                        Type = TransactionType.Withdrawal,
                        DepositAmount = 0m,
                        WithdrawalAmount = 100000m,
                        RunningBalance = 2979604.43m,
                        TargetLedgerName = "Office Expenses"
                    }
                }
            };

            byte[] excelBytes = _service.GenerateExcel(statement, "Canara Bank OD A/c");

            excelBytes.Should().NotBeNull();
            excelBytes.Length.Should().BeGreaterThan(500);

            // Verify OpenXML package structure
            using var ms = new MemoryStream(excelBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            zip.GetEntry("[Content_Types].xml").Should().NotBeNull();
            zip.GetEntry("xl/workbook.xml").Should().NotBeNull();
            zip.GetEntry("xl/styles.xml").Should().NotBeNull();
            var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml");
            sheetEntry.Should().NotBeNull();

            using var reader = new StreamReader(sheetEntry!.Open(), Encoding.UTF8);
            string sheetContent = reader.ReadToEnd();

            // Check headers
            sheetContent.Should().Contain("Date");
            sheetContent.Should().Contain("Narration");
            sheetContent.Should().Contain("Ref / Chq No");
            sheetContent.Should().Contain("Withdrawal (Dr)");
            sheetContent.Should().Contain("Deposit (Cr)");
            sheetContent.Should().Contain("Balance");
            sheetContent.Should().Contain("Tally Ledger");
            sheetContent.Should().Contain("Bank Ledger");

            // Check transaction data and escaping
            sheetContent.Should().Contain("MAA BHAWANI &amp; SONS &lt;CO&gt;");
            sheetContent.Should().Contain("SBIN426212493456");
            sheetContent.Should().Contain("Receipt");
            sheetContent.Should().Contain("Payment");
            sheetContent.Should().Contain("25000.00");
            sheetContent.Should().Contain("100000.00");
            sheetContent.Should().Contain("Suspense A/c");
            sheetContent.Should().Contain("Office Expenses");
            sheetContent.Should().Contain("Canara Bank OD A/c");
        }

        [Fact]
        public void GenerateExcel_WithNullStatement_ThrowsArgumentNullException()
        {
            var act = () => _service.GenerateExcel(null!, "Bank");
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task ExportToExcelFileAsync_WritesFileCorrectly()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"statement_{Guid.NewGuid():N}.xlsx");
            try
            {
                var statement = new StatementDocument
                {
                    BankName = "HDFC Bank",
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction
                        {
                            TransactionDate = DateTime.Today,
                            RawNarration = "Test Txn",
                            DepositAmount = 500m,
                            Type = TransactionType.Deposit
                        }
                    }
                };

                await _service.ExportToExcelFileAsync(statement, "HDFC Bank", tempFile);

                File.Exists(tempFile).Should().BeTrue();
                new FileInfo(tempFile).Length.Should().BeGreaterThan(500);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
