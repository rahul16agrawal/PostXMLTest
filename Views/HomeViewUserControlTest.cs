using System;
using System.Collections.Generic;
using System.Windows;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PostXML.Models;
using PostXML.Services;
using PostXML.ViewModels;
using PostXML.Views;
using Xunit;

namespace PostXMLTest.Views
{
    public class HomeViewUserControlTest
    {
        [WpfFact]
        public void ParameterlessConstructor_InitializesComponent()
        {
            var view = new HomeViewUserControl();
            view.Should().NotBeNull();
        }

        [WpfFact]
        public void ParameterizedConstructor_SetsDataContext()
        {
            var parserMock = new Mock<IBankStatementParser>();
            var tallyXmlMock = new Mock<ITallyXmlExportService>();
            var reconciliationMock = new Mock<IMathReconciliationEngine>();
            var fileDialogMock = new Mock<IFileDialogService>();
            var dispatcherMock = new Mock<IWinDispatcherService>();
            var loggerMock = new Mock<ILogger<HomeViewModel>>();

            var vm = new HomeViewModel(
                parserMock.Object,
                tallyXmlMock.Object,
                reconciliationMock.Object,
                fileDialogMock.Object,
                dispatcherMock.Object,
                loggerMock.Object);

            var view = new HomeViewUserControl(vm);

            view.Should().NotBeNull();
            view.DataContext.Should().Be(vm);
        }

        [WpfFact]
        public void HomeViewUserControl_Language_IsSetToIndianEnglish()
        {
            var view = new HomeViewUserControl();
            view.Language.IetfLanguageTag.Should().BeEquivalentTo("en-IN");
        }

        [WpfFact]
        public void HomeViewUserControl_WithLoadedTransactions_MeasuresAndRendersWithoutCultureNotFoundException()
        {
            // Verifies that neither summary cards nor DataGrid columns throw CultureNotFoundException
            // due to quotes in ConverterCulture bindings
            var parserMock = new Mock<IBankStatementParser>();
            var tallyXmlMock = new Mock<ITallyXmlExportService>();
            var reconciliationMock = new Mock<IMathReconciliationEngine>();
            var fileDialogMock = new Mock<IFileDialogService>();
            var dispatcherMock = new Mock<IWinDispatcherService>();
            var loggerMock = new Mock<ILogger<HomeViewModel>>();

            var vm = new HomeViewModel(
                parserMock.Object,
                tallyXmlMock.Object,
                reconciliationMock.Object,
                fileDialogMock.Object,
                dispatcherMock.Object,
                loggerMock.Object);

            var doc = new StatementDocument
            {
                BankName = "Canara Bank",
                AccountNumber = "1234567890",
                OpeningBalance = -3054604.43m,
                TotalDeposits = 12871997.78m,
                TotalWithdrawals = 13532139.16m,
                ClosingBalance = -3714745.81m,
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        RowIndex = 1,
                        TransactionDate = new DateTime(2026, 8, 1),
                        RawNarration = "UPI/CR/123/Payment",
                        PaymentMode = "UPI",
                        DepositAmount = 5000m,
                        WithdrawalAmount = 0m,
                        RunningBalance = -3049604.43m,
                        TargetLedgerName = "Suspense",
                        Type = TransactionType.Deposit
                    }
                }
            };

            vm.CurrentDocument = doc;
            vm.Transactions.Add(doc.Transactions[0]);
            vm.IsStatementLoaded = true;

            var view = new HomeViewUserControl(vm);
            var act = () =>
            {
                view.Measure(new Size(1024, 768));
                view.Arrange(new Rect(0, 0, 1024, 768));
                view.UpdateLayout();
            };

            act.Should().NotThrow();
        }
    }
}
