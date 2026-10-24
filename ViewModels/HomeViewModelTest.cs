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
        public void ClearStatement_ResetsAllFields()
        {
            _viewModel.CurrentDocument = new StatementDocument();
            _viewModel.Transactions.Add(new BankTransaction());
            _viewModel.IsStatementLoaded = true;
            _viewModel.SelectedStatementFilePath = "statement.pdf";
            _viewModel.SuccessMessage = "Loaded";

            _viewModel.ClearStatement();

            _viewModel.CurrentDocument.Should().BeNull();
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
    }
}
