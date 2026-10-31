using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            var vm = CreateTestViewModel();
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
            var vm = CreateTestViewModel();
            var doc = CreateSampleDocument();

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

        [WpfFact]
        public void HomeViewUserControl_WhenStatementLoaded_RendersSplitTwoSectionSummaryHeader()
        {
            var vm = CreateTestViewModel();
            var doc = CreateSampleDocument();

            vm.CurrentDocument = doc;
            vm.Transactions.Add(doc.Transactions[0]);
            vm.IsStatementLoaded = true;

            var view = new HomeViewUserControl(vm);
            view.Measure(new Size(1024, 768));
            view.Arrange(new Rect(0, 0, 1024, 768));
            view.UpdateLayout();

            var textBlocks = FindVisualChildren<TextBlock>(view).ToList();
            var texts = textBlocks.Select(t => t.Text).ToList();

            // Right section 2x2 grid headers must all exist
            texts.Should().Contain("Opening Balance");
            texts.Should().Contain("Closing Balance");
            texts.Should().Contain("Total Inflow (Cr)");
            texts.Should().Contain("Total Outflow (Dr)");

            // Left section bank info must exist
            texts.Should().Contain("Canara Bank");
            texts.Should().Contain("A/c: 1234567890");
        }

        [WpfFact]
        public void HomeViewUserControl_AccountNumberAndBankSelector_HaveMatchingFontSizeAndWeight()
        {
            var vm = CreateTestViewModel();
            var doc = CreateSampleDocument();

            vm.CurrentDocument = doc;
            vm.IsStatementLoaded = true;

            var view = new HomeViewUserControl(vm);
            view.Measure(new Size(1024, 768));
            view.Arrange(new Rect(0, 0, 1024, 768));
            view.UpdateLayout();

            var textBlocks = FindVisualChildren<TextBlock>(view).ToList();
            var accBlock = textBlocks.FirstOrDefault(t => t.Text == "A/c: 1234567890");
            accBlock.Should().NotBeNull();
            accBlock!.FontSize.Should().Be(13.0);
            accBlock.FontWeight.Should().Be(FontWeights.SemiBold);

            var comboBoxes = FindVisualChildren<ComboBox>(view).ToList();
            var bankCombo = comboBoxes.FirstOrDefault(c => c.ItemsSource == vm.AvailableBanks);
            bankCombo.Should().NotBeNull("Bank selector ComboBox must be present in the summary card");
            bankCombo!.FontSize.Should().Be(13.0);
            bankCombo.FontWeight.Should().Be(FontWeights.SemiBold);
        }

        [WpfFact]
        public void Render_HomeViewUserControl_Preview_Image()
        {
            var vm = CreateTestViewModel();
            var doc = CreateSampleDocument();
            doc.BankName = "HDFC Bank";
            doc.AccountNumber = "50200075012327";

            vm.CurrentDocument = doc;
            vm.IsStatementLoaded = true;

            var view = new HomeViewUserControl(vm);
            view.Width = 1000;
            view.Height = 650;
            view.Measure(new Size(1000, 650));
            view.Arrange(new Rect(0, 0, 1000, 650));
            view.UpdateLayout();

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1000, 650, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(view);

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

            string outDir = @"C:\Users\rahul\.gemini\antigravity\brain\cd3d9657-643a-46bf-97d5-87cee5c83cde";
            string outFile = System.IO.Path.Combine(outDir, "preview_option_a.png");

            using var fs = System.IO.File.Create(outFile);
            encoder.Save(fs);
        }

        [WpfFact]
        public void HomeViewUserControl_ReconciliationNotes_HasWrappingConfigured()
        {
            var vm = CreateTestViewModel();
            var doc = CreateSampleDocument();

            vm.CurrentDocument = doc;
            vm.IsStatementLoaded = true;

            var view = new HomeViewUserControl(vm);
            view.Measure(new Size(1024, 768));
            view.Arrange(new Rect(0, 0, 1024, 768));
            view.UpdateLayout();

            var textBlocks = FindVisualChildren<TextBlock>(view).ToList();
            var notesBlock = textBlocks.FirstOrDefault(t => t.TextWrapping == TextWrapping.Wrap && t.LineHeight == 16);

            notesBlock.Should().NotBeNull("Reconciliation notes must have wrapping and line-height configured for breathing room");
        }

        [WpfFact]
        public void HomeViewUserControl_FeedbackMessages_HaveToolTipsConfigured()
        {
            var vm = CreateTestViewModel();
            vm.IsStatementLoaded = true;
            vm.SuccessMessage = "Tally XML voucher exported successfully to Canara_Bank_Tally.xml";
            vm.ErrorMessage = "Sample validation error";

            var view = new HomeViewUserControl(vm);
            view.Measure(new Size(1024, 768));
            view.Arrange(new Rect(0, 0, 1024, 768));
            view.UpdateLayout();

            var textBlocks = FindVisualChildren<TextBlock>(view).ToList();
            var successBlock = textBlocks.FirstOrDefault(t => t.Text == vm.SuccessMessage);
            var actionErrorBlock = textBlocks.FirstOrDefault(t => t.Text == vm.ErrorMessage && t.TextTrimming == TextTrimming.CharacterEllipsis);

            successBlock.Should().NotBeNull();
            successBlock!.ToolTip.Should().NotBeNull();

            actionErrorBlock.Should().NotBeNull();
            actionErrorBlock!.ToolTip.Should().NotBeNull();
        }

        [WpfFact]
        public void HomeViewUserControl_ActionBar_HasUnifiedColorCodedButtons_AndNoAutoMapButton()
        {
            var vm = CreateTestViewModel();
            var doc = CreateSampleDocument();

            vm.CurrentDocument = doc;
            vm.IsStatementLoaded = true;

            var view = new HomeViewUserControl(vm);
            view.Measure(new Size(1024, 768));
            view.Arrange(new Rect(0, 0, 1024, 768));
            view.UpdateLayout();

            var buttons = FindVisualChildren<Button>(view).ToList();
            var buttonTexts = buttons.Select(b => b.Content?.ToString() ?? string.Empty).ToList();

            // Auto-Map Ledgers button must be removed
            buttonTexts.Should().NotContain(t => t.Contains("Auto-Map Ledgers", StringComparison.OrdinalIgnoreCase));

            // Upload New File and Export buttons must be present
            buttonTexts.Should().Contain(t => t.Contains("Upload New File", StringComparison.OrdinalIgnoreCase));
            buttonTexts.Should().Contain(t => t.Contains("Export", StringComparison.OrdinalIgnoreCase));
        }

        [WpfTheory]
        [InlineData(800, 600)]
        [InlineData(980, 700)]
        [InlineData(1280, 800)]
        [InlineData(1920, 1080)]
        public void HomeViewUserControl_DifferentWindowResolutions_MeasuresAndArrangesWithoutThrowing(double width, double height)
        {
            var vm = CreateTestViewModel();
            var doc = CreateSampleDocument();

            vm.CurrentDocument = doc;
            vm.Transactions.Add(doc.Transactions[0]);
            vm.IsStatementLoaded = true;

            var view = new HomeViewUserControl(vm);
            var act = () =>
            {
                view.Measure(new Size(width, height));
                view.Arrange(new Rect(0, 0, width, height));
                view.UpdateLayout();
            };

            act.Should().NotThrow();
        }

        private static HomeViewModel CreateTestViewModel()
        {
            var parserMock = new Mock<IBankStatementParser>();
            var tallyXmlMock = new Mock<ITallyXmlExportService>();
            var reconciliationMock = new Mock<IMathReconciliationEngine>();
            var fileDialogMock = new Mock<IFileDialogService>();
            var dispatcherMock = new Mock<IWinDispatcherService>();
            var loggerMock = new Mock<ILogger<HomeViewModel>>();

            return new HomeViewModel(
                parserMock.Object,
                tallyXmlMock.Object,
                reconciliationMock.Object,
                fileDialogMock.Object,
                dispatcherMock.Object,
                loggerMock.Object);
        }

        private static StatementDocument CreateSampleDocument()
        {
            return new StatementDocument
            {
                BankName = "Canara Bank",
                AccountNumber = "1234567890",
                OpeningBalance = -3054604.43m,
                TotalDeposits = 12871997.78m,
                TotalWithdrawals = 13532139.16m,
                ClosingBalance = -3714745.81m,
                Reconciliation = new ReconciliationSummary
                {
                    IsFullyReconciled = true,
                    ExpectedClosingBalance = -3714745.81m,
                    ActualClosingBalance = -3714745.81m
                },
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
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            int count = VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                {
                    yield return t;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }
}
