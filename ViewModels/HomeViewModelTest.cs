using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Postaze.Models;
using Postaze.Services;
using PostXML.Models;
using PostXML.Services;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest.ViewModels
{
    public class HomeViewModelTest
    {
        private readonly Mock<IBankStatementParser> _parserMock;
        private readonly Mock<ITallyXmlExportService> _tallyXmlExportMock;
        private readonly Mock<IMathReconciliationEngine> _reconciliationEngineMock;
        private readonly Mock<IFileDialogService> _fileDialogMock;
        private readonly Mock<IWinDispatcherService> _dispatcherMock;
        private readonly Mock<ILogger<HomeViewModel>> _loggerMock;
        private readonly HomeViewModel _viewModel;

        public HomeViewModelTest()
        {
            _parserMock = new Mock<IBankStatementParser>();
            _tallyXmlExportMock = new Mock<ITallyXmlExportService>();
            _reconciliationEngineMock = new Mock<IMathReconciliationEngine>();
            _fileDialogMock = new Mock<IFileDialogService>();
            _dispatcherMock = new Mock<IWinDispatcherService>();
            _loggerMock = new Mock<ILogger<HomeViewModel>>();

            _dispatcherMock
                .Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                .Callback<Action>(a => a())
                .Returns(Task.CompletedTask);

            _viewModel = new HomeViewModel(
                _parserMock.Object,
                _tallyXmlExportMock.Object,
                _reconciliationEngineMock.Object,
                _fileDialogMock.Object,
                _dispatcherMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public void Constructor_SetsDefaultValues()
        {
            _viewModel.WelcomeHeadline.Should().Be("Bank Statement to Tally Prime XML Converter");
            _viewModel.WelcomeSubtitle.Should().Contain("Convert bank statements");
            _viewModel.StatusMessage.Should().Be("Ready to upload statement.");
            _viewModel.IsProcessing.Should().BeFalse();
            _viewModel.IsStatementLoaded.Should().BeFalse();
            _viewModel.IsPasswordRequired.Should().BeFalse();
            _viewModel.SelectedStatementFilePath.Should().BeEmpty();
            _viewModel.AuthenticatedCustomer.Should().BeNull();
            _viewModel.AvailableLedgers.Should().Contain("Suspense A/c");
            _viewModel.AvailableLedgers.Should().Contain("Staff Welfare Expense");
            _viewModel.ReconciliationBadgeText.Should().Be("● Ready");
        }

        [Fact]
        public void Initialize_SetsAuthenticatedCustomer()
        {
            var customer = new Customer
            {
                CustomerId = 42,
                Category = CustomerCategory.WHOLESALER,
                Gstin = "27AABCU9603R1ZM",
                EmailId = "user@postxml.com"
            };

            _viewModel.Initialize(customer);

            _viewModel.AuthenticatedCustomer.Should().Be(customer);
        }

        [Fact]
        public async Task BrowseFileAsync_Cancelled_DoesNotLoad()
        {
            _fileDialogMock.Setup(d => d.OpenBankStatementFileDialog()).Returns((string?)null);

            await _viewModel.BrowseFileAsync();

            _viewModel.IsStatementLoaded.Should().BeFalse();
            _parserMock.Verify(p => p.ParsePdf(It.IsAny<Stream>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task LoadStatementFileAsync_FileNotFound_SetsErrorMessage()
        {
            await _viewModel.LoadStatementFileAsync(@"C:\NonExistent\sample.pdf");

            _viewModel.ErrorMessage.Should().Contain("does not exist");
            _viewModel.IsStatementLoaded.Should().BeFalse();
        }

        [Fact]
        public async Task LoadStatementFileAsync_ValidPdf_LoadsStatementSuccessfully()
        {
            // Arrange a temporary file
            string tempPdf = Path.GetTempFileName();
            File.WriteAllBytes(tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF

            try
            {
                var doc = new StatementDocument
                {
                    BankName = "HDFC Bank",
                    AccountNumber = "501002345678",
                    OpeningBalance = 10000m,
                    ClosingBalance = 15000m,
                    Reconciliation = new ReconciliationSummary
                    {
                        IsFullyReconciled = true,
                        ExpectedClosingBalance = 15000m,
                        ActualClosingBalance = 15000m
                    },
                    Transactions = new List<BankTransaction>
                    {
                        new()
                        {
                            RowIndex = 1,
                            TransactionDate = new DateTime(2024, 4, 1),
                            RawNarration = "UPI/123/ZOMATO",
                            WithdrawalAmount = 500m,
                            DepositAmount = 0m,
                            RunningBalance = 9500m,
                            SuggestedLedgerName = "Staff Welfare Expense",
                            TargetLedgerName = "Suspense A/c"
                        }
                    }
                };

                _parserMock.Setup(p => p.ParsePdf(It.IsAny<Stream>(), null)).Returns(doc);

                // Act
                await _viewModel.LoadStatementFileAsync(tempPdf);

                // Assert
                _viewModel.IsStatementLoaded.Should().BeTrue();
                _viewModel.CurrentDocument.Should().Be(doc);
                _viewModel.Transactions.Should().HaveCount(1);
                _viewModel.ReconciliationBadgeText.Should().Contain("100% Mathematically Reconciled");
                _viewModel.StatusMessage.Should().Contain("HDFC Bank");
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public async Task LoadStatementFileAsync_EncryptedPdf_SetsPasswordRequired()
        {
            string tempPdf = Path.GetTempFileName();
            File.WriteAllBytes(tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 });

            try
            {
                _parserMock
                    .Setup(p => p.ParsePdf(It.IsAny<Stream>(), null))
                    .Throws(new InvalidOperationException("This PDF document is encrypted with a password."));

                await _viewModel.LoadStatementFileAsync(tempPdf);

                _viewModel.IsPasswordRequired.Should().BeTrue();
                _viewModel.ErrorMessage.Should().Contain("password-protected");
                _viewModel.StatusMessage.Should().Be("Password required.");
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public async Task LoadStatementFileAsync_WithPdfPigEncryptedException_SetsPasswordRequired()
        {
            string tempPdf = Path.GetTempFileName();
            File.WriteAllBytes(tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 });

            try
            {
                _parserMock
                    .Setup(p => p.ParsePdf(It.IsAny<Stream>(), null))
                    .Throws(new UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException("The document was encrypted and none of the provided passwords were the user or owner password."));

                await _viewModel.LoadStatementFileAsync(tempPdf);

                _viewModel.IsPasswordRequired.Should().BeTrue();
                _viewModel.ErrorMessage.Should().Contain("password-protected");
                _viewModel.StatusMessage.Should().Be("Password required.");
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public async Task LoadStatementFileAsync_WithPreDetectedEncryptedPdf_SetsPasswordRequiredWithoutCallingParsePdf()
        {
            string tempPdf = Path.GetTempFileName();
            File.WriteAllBytes(tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 });

            try
            {
                _parserMock.Setup(p => p.IsPdfEncrypted(It.IsAny<Stream>())).Returns(true);

                await _viewModel.LoadStatementFileAsync(tempPdf);

                _viewModel.IsPasswordRequired.Should().BeTrue();
                _viewModel.ErrorMessage.Should().Contain("password-protected");
                _viewModel.StatusMessage.Should().Be("Password required.");
                _parserMock.Verify(p => p.ParsePdf(It.IsAny<Stream>(), It.IsAny<string>()), Times.Never);
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public async Task LoadStatementFileAsync_WithIncorrectPassword_SetsIncorrectPasswordErrorMessage()
        {
            string tempPdf = Path.GetTempFileName();
            File.WriteAllBytes(tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 });

            try
            {
                _parserMock
                    .Setup(p => p.ParsePdf(It.IsAny<Stream>(), "wrongPassword"))
                    .Throws(new UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException("The document was encrypted and none of the provided passwords were the user or owner password."));

                await _viewModel.LoadStatementFileAsync(tempPdf, "wrongPassword");

                _viewModel.IsPasswordRequired.Should().BeTrue();
                _viewModel.ErrorMessage.Should().Contain("Incorrect password");
                _viewModel.StatusMessage.Should().Be("Password required.");
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public async Task ProcessPasswordAsync_EmptyPassword_SetsErrorMessage()
        {
            _viewModel.PdfPassword = string.Empty;

            await _viewModel.ProcessPasswordAsync();

            _viewModel.ErrorMessage.Should().Contain("Please enter the PDF password");
        }

        [Fact]
        public void CancelPassword_ResetsPasswordState()
        {
            _viewModel.IsPasswordRequired = true;
            _viewModel.PdfPassword = "secretPassword";
            _viewModel.SelectedStatementFilePath = "dummy.pdf";

            _viewModel.CancelPassword();

            _viewModel.IsPasswordRequired.Should().BeFalse();
            _viewModel.PdfPassword.Should().BeEmpty();
            _viewModel.SelectedStatementFilePath.Should().BeEmpty();
        }

        [Fact]
        public void AutoMapLedgers_MapsSuggestedLedgers()
        {
            var txn1 = new BankTransaction
            {
                RowIndex = 1,
                RawNarration = "UPI/ZOMATO",
                SuggestedLedgerName = "Staff Welfare Expense",
                TargetLedgerName = "Suspense A/c"
            };
            var txn2 = new BankTransaction
            {
                RowIndex = 2,
                RawNarration = "PETROL PUMP",
                SuggestedLedgerName = "Fuel Expense",
                TargetLedgerName = "Suspense"
            };

            _viewModel.Transactions.Add(txn1);
            _viewModel.Transactions.Add(txn2);

            _viewModel.AutoMapLedgers();

            txn1.TargetLedgerName.Should().Be("Staff Welfare Expense");
            txn1.IsLedgerConfirmed.Should().BeTrue();
            txn2.TargetLedgerName.Should().Be("Fuel Expense");
            txn2.IsLedgerConfirmed.Should().BeTrue();
            _viewModel.SuccessMessage.Should().Contain("Auto-mapped 2 transaction ledgers");
        }

        [Fact]
        public async Task ExportTallyXmlAsync_NoTransactions_SetsError()
        {
            _viewModel.CurrentDocument = null;

            await _viewModel.ExportTallyXmlAsync();

            _viewModel.ErrorMessage.Should().Contain("No transactions available");
        }

        [Fact]
        public async Task ExportTallyXmlAsync_Success_WritesXmlFile()
        {
            string tempOut = Path.GetTempFileName();
            try
            {
                var doc = new StatementDocument
                {
                    BankName = "ICICI Bank",
                    OpeningBalance = 1000m,
                    ClosingBalance = 2000m,
                    Transactions = new List<BankTransaction>
                    {
                        new() { RowIndex = 1, WithdrawalAmount = 100m, TargetLedgerName = "Office Supplies" }
                    }
                };

                _viewModel.CurrentDocument = doc;
                _viewModel.Transactions.Add(doc.Transactions[0]);

                _fileDialogMock
                    .Setup(d => d.SaveTallyXmlFileDialog(It.IsAny<string>()))
                    .Returns(tempOut);

                _tallyXmlExportMock
                    .Setup(t => t.GenerateTallyXml(doc, "ICICI Bank", "Default Company"))
                    .Returns("<ENVELOPE><BODY>TEST_XML</BODY></ENVELOPE>");

                await _viewModel.ExportTallyXmlAsync();

                _viewModel.SuccessMessage.Should().Contain("Tally XML voucher exported successfully");
                File.ReadAllText(tempOut).Should().Contain("TEST_XML");
            }
            finally
            {
                if (File.Exists(tempOut)) File.Delete(tempOut);
            }
        }

        [Fact]
        public async Task ExportTallyXmlAsync_WithCustomBankLedgerName_PassesCustomNameToExportService()
        {
            string tempOut = Path.Combine(Path.GetTempPath(), $"tally_{Guid.NewGuid():N}.xml");
            try
            {
                var doc = new StatementDocument
                {
                    BankName = "Canara Bank",
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, WithdrawalAmount = 200m, Type = TransactionType.Withdrawal }
                    }
                };

                _viewModel.CurrentDocument = doc;
                _viewModel.Transactions.Add(doc.Transactions[0]);
                _viewModel.BankLedgerName = "Canara Bank OD A/c";

                _fileDialogMock
                    .Setup(d => d.SaveTallyXmlFileDialog(It.IsAny<string>()))
                    .Returns(tempOut);

                _tallyXmlExportMock
                    .Setup(t => t.GenerateTallyXml(doc, "Canara Bank OD A/c", "Default Company"))
                    .Returns("<ENVELOPE><BODY>CUSTOM_LEDGER_XML</BODY></ENVELOPE>");

                await _viewModel.ExportTallyXmlAsync();

                _tallyXmlExportMock.Verify(t => t.GenerateTallyXml(doc, "Canara Bank OD A/c", "Default Company"), Times.Once);
            }
            finally
            {
                if (File.Exists(tempOut)) File.Delete(tempOut);
            }
        }

        [Fact]
        public void ClearStatement_ResetsAllFields()
        {
            _viewModel.CurrentDocument = new StatementDocument();
            _viewModel.BankLedgerName = "Test Bank";
            _viewModel.Transactions.Add(new BankTransaction());
            _viewModel.IsStatementLoaded = true;
            _viewModel.SelectedStatementFilePath = "statement.pdf";
            _viewModel.SuccessMessage = "Loaded";

            _viewModel.ClearStatement();

            _viewModel.CurrentDocument.Should().BeNull();
            _viewModel.BankLedgerName.Should().BeEmpty();
            _viewModel.Transactions.Should().BeEmpty();
            _viewModel.IsStatementLoaded.Should().BeFalse();
            _viewModel.SelectedStatementFilePath.Should().BeEmpty();
            _viewModel.SuccessMessage.Should().BeNull();
            _viewModel.StatusMessage.Should().Be("Ready to upload statement.");
        }

        [Fact]
        public async Task LoadStatementFileAsync_Discrepancy_SetsWarningBadge()
        {
            string tempPdf = Path.GetTempFileName();
            File.WriteAllBytes(tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 });

            try
            {
                var doc = new StatementDocument
                {
                    BankName = "Axis Bank",
                    Reconciliation = new ReconciliationSummary
                    {
                        IsFullyReconciled = false,
                        ExpectedClosingBalance = 10000m,
                        ActualClosingBalance = 9850m,
                        MismatchedRowIndices = new List<int> { 3 }
                    },
                    Transactions = new List<BankTransaction>
                    {
                        new() { RowIndex = 1, WithdrawalAmount = 150m }
                    }
                };

                _parserMock.Setup(p => p.ParsePdf(It.IsAny<Stream>(), null)).Returns(doc);

                await _viewModel.LoadStatementFileAsync(tempPdf);

                _viewModel.ReconciliationBadgeText.Should().Contain("Discrepancy: ₹150.00");
                _viewModel.ReconciliationBadgeBackground.Should().Be("#FEE2E2");
                _viewModel.ReconciliationSummaryNotes.Should().Contain("1 row(s)");
                _viewModel.ReconciliationSummaryNotes.Should().Contain("rows 3");
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public void OpenExportDialog_WhenStatementLoaded_SetsIsExportDialogOpen_LeavesBankLedgerEmpty()
        {
            var doc = new StatementDocument
            {
                BankName = "Kotak Mahindra Bank",
                Transactions = new List<BankTransaction> { new() { RowIndex = 1 } }
            };
            _viewModel.CurrentDocument = doc;
            _viewModel.Transactions.Add(doc.Transactions[0]);
            _viewModel.BankLedgerName = string.Empty;

            _viewModel.OpenExportDialog();

            _viewModel.IsExportDialogOpen.Should().BeTrue();
            _viewModel.BankLedgerName.Should().BeEmpty(); // Must always be explicitly entered by user
        }

        [Fact]
        public async Task ConfirmExportAsync_WhenBankLedgerEmpty_SetsErrorMessage_AndDoesNotExport()
        {
            var doc = new StatementDocument
            {
                BankName = "HDFC Bank",
                Transactions = new List<BankTransaction> { new() { RowIndex = 1 } }
            };
            _viewModel.CurrentDocument = doc;
            _viewModel.Transactions.Add(doc.Transactions[0]);
            _viewModel.BankLedgerName = "   "; // Whitespace / empty
            _viewModel.IsExportDialogOpen = true;

            await _viewModel.ConfirmExportAsync();

            _viewModel.HasExportDialogError.Should().BeTrue();
            _viewModel.ExportDialogErrorMessage.Should().Contain("Please enter the Bank Account Ledger Name (Tally)");
            _viewModel.IsExportDialogOpen.Should().BeTrue(); // Dialog remains open

            _fileDialogMock.Verify(d => d.SaveTallyXmlFileDialog(It.IsAny<string>()), Times.Never);
            _fileDialogMock.Verify(d => d.SaveExcelFileDialog(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void CloseExportDialog_ResetsIsExportDialogOpen()
        {
            _viewModel.IsExportDialogOpen = true;
            _viewModel.CloseExportDialog();
            _viewModel.IsExportDialogOpen.Should().BeFalse();
        }

        [Fact]
        public void FormatToggling_XmlAndExcel_AreMutuallyExclusive()
        {
            _viewModel.IsXmlFormat.Should().BeTrue();
            _viewModel.IsExcelFormat.Should().BeFalse();

            _viewModel.IsExcelFormat = true;
            _viewModel.IsExcelFormat.Should().BeTrue();
            _viewModel.IsXmlFormat.Should().BeFalse();

            _viewModel.IsXmlFormat = true;
            _viewModel.IsXmlFormat.Should().BeTrue();
            _viewModel.IsExcelFormat.Should().BeFalse();
        }

        [Fact]
        public async Task ConfirmExportAsync_WhenExcelSelected_ExportsExcelAndClosesDialog()
        {
            string tempOut = Path.Combine(Path.GetTempPath(), $"excel_export_{Guid.NewGuid():N}.xlsx");
            try
            {
                var doc = new StatementDocument
                {
                    BankName = "Canara Bank",
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, DepositAmount = 500m, Type = TransactionType.Deposit }
                    }
                };

                _viewModel.CurrentDocument = doc;
                _viewModel.Transactions.Add(doc.Transactions[0]);
                _viewModel.BankLedgerName = "Canara Bank Current A/c";
                _viewModel.IsExcelFormat = true;
                _viewModel.IsExportDialogOpen = true;

                _fileDialogMock
                    .Setup(d => d.SaveExcelFileDialog(It.IsAny<string>()))
                    .Returns(tempOut);

                await _viewModel.ConfirmExportAsync();

                _viewModel.IsExportDialogOpen.Should().BeFalse();
                _viewModel.SuccessMessage.Should().Contain("Excel spreadsheet exported successfully");
                File.Exists(tempOut).Should().BeTrue();
            }
            finally
            {
                if (File.Exists(tempOut)) File.Delete(tempOut);
            }
        }

        [Fact]
        public async Task ConfirmExportAsync_WhenXmlSelected_ExportsXmlAndClosesDialog()
        {
            string tempOut = Path.Combine(Path.GetTempPath(), $"xml_export_{Guid.NewGuid():N}.xml");
            try
            {
                var doc = new StatementDocument
                {
                    BankName = "State Bank of India",
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, WithdrawalAmount = 100m, Type = TransactionType.Withdrawal }
                    }
                };

                _viewModel.CurrentDocument = doc;
                _viewModel.Transactions.Add(doc.Transactions[0]);
                _viewModel.BankLedgerName = "SBI";
                _viewModel.IsXmlFormat = true;
                _viewModel.IsExportDialogOpen = true;

                _fileDialogMock
                    .Setup(d => d.SaveTallyXmlFileDialog(It.IsAny<string>()))
                    .Returns(tempOut);

                _tallyXmlExportMock
                    .Setup(t => t.GenerateTallyXml(doc, "SBI", "Default Company"))
                    .Returns("<ENVELOPE>SBI_XML</ENVELOPE>");

                await _viewModel.ConfirmExportAsync();

                _viewModel.IsExportDialogOpen.Should().BeFalse();
                _viewModel.SuccessMessage.Should().Contain("Tally XML voucher exported successfully");
                File.ReadAllText(tempOut).Should().Contain("SBI_XML");
            }
            finally
            {
                if (File.Exists(tempOut)) File.Delete(tempOut);
            }
        }

        [Fact]
        public void AvailableBanks_IsInitializedWithSupportedBanks()
        {
            _viewModel.AvailableBanks.Should().NotBeEmpty();
            _viewModel.AvailableBanks.Select(b => b.DisplayName).Should().Contain(new[] { "HDFC Bank", "Axis Bank", "State Bank of India", "Kotak Mahindra Bank" });
        }

        [Fact]
        public async Task LoadStatementFileAsync_AutoSelectsMatchingBank()
        {
            string tempPdf = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 });
                var doc = new StatementDocument
                {
                    BankName = "Axis Bank",
                    AccountNumber = "924020049955809",
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, RunningBalance = 1000m }
                    }
                };

                _parserMock.Setup(p => p.ParsePdf(It.IsAny<Stream>(), null)).Returns(doc);

                await _viewModel.LoadStatementFileAsync(tempPdf);

                _viewModel.SelectedBank.Should().NotBeNull();
                _viewModel.SelectedBank!.DisplayName.Should().Be("Axis Bank");
                _viewModel.SelectedBank.BankCode.Should().Be("AXIS");
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public async Task SelectedBank_ChangedByUser_TriggersReParsingWithBankOverride()
        {
            string tempPdf = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 });
                var initialDoc = new StatementDocument
                {
                    BankName = "Axis Bank",
                    AccountNumber = "924020049955809",
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, RunningBalance = 1000m }
                    }
                };

                var overriddenDoc = new StatementDocument
                {
                    BankName = "HDFC Bank",
                    AccountNumber = "50200075012327",
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, RunningBalance = 2000m }
                    }
                };

                _parserMock.Setup(p => p.ParsePdf(It.IsAny<Stream>(), null)).Returns(initialDoc);
                _parserMock.Setup(p => p.ParsePdf(It.IsAny<Stream>(), It.IsAny<string>(), "HDFC")).Returns(overriddenDoc);

                await _viewModel.LoadStatementFileAsync(tempPdf);
                _viewModel.SelectedBank!.BankCode.Should().Be("AXIS");

                // User manually switches to HDFC
                var hdfcOption = _viewModel.AvailableBanks.First(b => b.BankCode == "HDFC");
                _viewModel.SelectedBank = hdfcOption;

                // Allow async Task triggered by OnSelectedBankChanged to execute
                await Task.Delay(100);

                _parserMock.Verify(p => p.ParsePdf(It.IsAny<Stream>(), It.IsAny<string>(), "HDFC"), Times.Once);
                _viewModel.CurrentDocument!.BankName.Should().Be("HDFC Bank");
                _viewModel.CurrentDocument.AccountNumber.Should().Be("50200075012327");
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        #region Telemetry & Page Tracking

        [Fact]
        public async Task LoadStatementFileAsync_ComputesSha256FileHash()
        {
            string tempPdf = Path.GetTempFileName();
            try
            {
                byte[] bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x31, 0x32, 0x33 };
                File.WriteAllBytes(tempPdf, bytes);

                var doc = new StatementDocument
                {
                    BankName = "HDFC Bank",
                    TotalPages = 3,
                    Transactions = new List<BankTransaction>()
                };

                _parserMock.Setup(p => p.ParsePdf(It.IsAny<Stream>(), null)).Returns(doc);

                await _viewModel.LoadStatementFileAsync(tempPdf);

                _viewModel.CurrentDocument.Should().NotBeNull();
                _viewModel.CurrentDocument!.FileHashSha256.Should().NotBeNullOrWhiteSpace();
                _viewModel.CurrentDocument.FileHashSha256!.Length.Should().Be(64); // 64 hex chars
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public async Task ConfirmExportAsync_WhenBillingServicePresent_DispatchesConversionTelemetry()
        {
            string tempOut = Path.Combine(Path.GetTempPath(), $"telemetry_export_{Guid.NewGuid():N}.xml");
            try
            {
                var billingMock = new Mock<IPostXmlBillingService>();
                var excelMock = new Mock<IExcelExportService>();
                var detectorMock = new Mock<IBankDetectorService>();
                var vm = new HomeViewModel(
                    _parserMock.Object,
                    _tallyXmlExportMock.Object,
                    excelMock.Object,
                    _reconciliationEngineMock.Object,
                    _fileDialogMock.Object,
                    _dispatcherMock.Object,
                    _loggerMock.Object,
                    detectorMock.Object,
                    billingMock.Object
                );

                var doc = new StatementDocument
                {
                    BankName = "HDFC Bank",
                    AccountNumber = "50200037586423",
                    TotalPages = 4,
                    FileHashSha256 = "dummyhash456",
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, DepositAmount = 1000m, Type = TransactionType.Deposit }
                    }
                };

                vm.CurrentDocument = doc;
                vm.Transactions.Add(doc.Transactions[0]);
                vm.BankLedgerName = "HDFC Bank A/c";
                vm.IsXmlFormat = true;
                vm.IsExportDialogOpen = true;

                _fileDialogMock
                    .Setup(d => d.SaveTallyXmlFileDialog(It.IsAny<string>()))
                    .Returns(tempOut);

                _tallyXmlExportMock
                    .Setup(t => t.GenerateTallyXml(doc, "HDFC Bank A/c", "Default Company"))
                    .Returns("<ENVELOPE>TELEMETRY</ENVELOPE>");

                await vm.ConfirmExportAsync();

                // Wait briefly for fire-and-forget telemetry task
                await Task.Delay(100);

                billingMock.Verify(b => b.ReportStatementConvertedAsync(
                    It.Is<StatementConversionTelemetry>(t =>
                        t.PageCount == 4 &&
                        t.AccountNumberMasked.Contains("XXXX") &&
                        t.ExportFormat == "XML" &&
                        t.FileHashSha256 == "dummyhash456"),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                if (File.Exists(tempOut)) File.Delete(tempOut);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task LoadStatementFileAsync_WhenFilePathIsNullOrWhitespace_SetsErrorMessage(string? invalidPath)
        {
            await _viewModel.LoadStatementFileAsync(invalidPath!);

            _viewModel.ErrorMessage.Should().Be("Selected statement file does not exist or cannot be accessed.");
            _viewModel.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task LoadStatementFileAsync_WhenFileNotFound_SetsErrorMessage()
        {
            string nonExistentPath = Path.Combine(Path.GetTempPath(), $"missing_file_{Guid.NewGuid():N}.pdf");

            await _viewModel.LoadStatementFileAsync(nonExistentPath);

            _viewModel.ErrorMessage.Should().Be("Selected statement file does not exist or cannot be accessed.");
            _viewModel.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task ConfirmExportAsync_TelemetryDispatch_HandlesNullAndShortAccountNumbersGracefully()
        {
            string tempOut = Path.Combine(Path.GetTempPath(), $"telemetry_short_acc_{Guid.NewGuid():N}.xml");
            try
            {
                var billingMock = new Mock<IPostXmlBillingService>();
                var vm = new HomeViewModel(
                    _parserMock.Object,
                    _tallyXmlExportMock.Object,
                    new Mock<IExcelExportService>().Object,
                    _reconciliationEngineMock.Object,
                    _fileDialogMock.Object,
                    _dispatcherMock.Object,
                    _loggerMock.Object,
                    new Mock<IBankDetectorService>().Object,
                    billingMock.Object
                );

                // Document with short account number (<= 8 chars) and TotalPages = 0
                var doc = new StatementDocument
                {
                    BankName = "ICICI Bank",
                    AccountNumber = "12345",
                    TotalPages = 0, // Should default to 1
                    Reconciliation = null!,
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, DepositAmount = 500m, Type = TransactionType.Deposit }
                    }
                };

                vm.CurrentDocument = doc;
                vm.Transactions.Add(doc.Transactions[0]);
                vm.BankLedgerName = "ICICI Bank";
                vm.IsXmlFormat = true;
                vm.IsExportDialogOpen = true;

                _fileDialogMock.Setup(d => d.SaveTallyXmlFileDialog(It.IsAny<string>())).Returns(tempOut);
                _tallyXmlExportMock.Setup(t => t.GenerateTallyXml(doc, "ICICI Bank", "Default Company")).Returns("<XML />");

                await vm.ConfirmExportAsync();
                await Task.Delay(100);

                billingMock.Verify(b => b.ReportStatementConvertedAsync(
                    It.Is<StatementConversionTelemetry>(t =>
                        t.PageCount == 1 &&
                        t.AccountNumberMasked == "12345" &&
                        t.IsReconciled == false),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                if (File.Exists(tempOut)) File.Delete(tempOut);
            }
        }

        [Fact]
        public async Task ConfirmExportAsync_WhenBillingServiceThrowsException_DoesNotFailExport()
        {
            string tempOut = Path.Combine(Path.GetTempPath(), $"telemetry_fail_{Guid.NewGuid():N}.xml");
            try
            {
                var billingMock = new Mock<IPostXmlBillingService>();
                billingMock.Setup(b => b.ReportStatementConvertedAsync(It.IsAny<StatementConversionTelemetry>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Telemetry endpoint offline"));

                var vm = new HomeViewModel(
                    _parserMock.Object,
                    _tallyXmlExportMock.Object,
                    new Mock<IExcelExportService>().Object,
                    _reconciliationEngineMock.Object,
                    _fileDialogMock.Object,
                    _dispatcherMock.Object,
                    _loggerMock.Object,
                    new Mock<IBankDetectorService>().Object,
                    billingMock.Object
                );

                var doc = new StatementDocument
                {
                    BankName = "SBI",
                    AccountNumber = "1234567890",
                    TotalPages = 2,
                    Transactions = new List<BankTransaction>
                    {
                        new BankTransaction { TransactionDate = DateTime.Today, WithdrawalAmount = 200m, Type = TransactionType.Withdrawal }
                    }
                };

                vm.CurrentDocument = doc;
                vm.Transactions.Add(doc.Transactions[0]);
                vm.BankLedgerName = "SBI";
                vm.IsXmlFormat = true;
                vm.IsExportDialogOpen = true;

                _fileDialogMock.Setup(d => d.SaveTallyXmlFileDialog(It.IsAny<string>())).Returns(tempOut);
                _tallyXmlExportMock.Setup(t => t.GenerateTallyXml(doc, "SBI", "Default Company")).Returns("<XML />");

                // Export should succeed despite telemetry background exception
                await vm.ConfirmExportAsync();

                vm.SuccessMessage.Should().Contain("Tally XML voucher exported successfully");
                File.Exists(tempOut).Should().BeTrue();
            }
            finally
            {
                if (File.Exists(tempOut)) File.Delete(tempOut);
            }
        }

        [Fact]
        public void EnsureLedgerAvailable_WhenNullOrWhitespace_DoesNotAdd()
        {
            int initialCount = _viewModel.AvailableLedgers.Count;

            // Invoking with null, empty, whitespace
            _viewModel.BankLedgerName = "   ";
            _viewModel.AvailableLedgers.Should().HaveCount(initialCount);
        }

        #endregion
    }
}
