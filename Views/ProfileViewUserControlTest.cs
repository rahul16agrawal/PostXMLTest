using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.Services;
using PostXML.ViewModels;
using PostXML.Views;
using Xunit;

namespace PostXMLTest.Views
{
    public class ProfileViewUserControlTest
    {
        [WpfFact]
        public void ParameterlessConstructor_InitializesComponent()
        {
            var view = new ProfileViewUserControl();
            view.Should().NotBeNull();
        }

        [WpfFact]
        public void ParameterizedConstructor_SetsDataContext()
        {
            var repoMock = new Mock<ICustomerRepository>();
            var loggerMock = new Mock<ILogger<ProfileViewModel>>();
            var dispatcherMock = new Mock<IWinDispatcherService>();
            var profileServiceMock = new Mock<IProfileService>();
            var sessionMock = new Mock<ICustomerSessionService>();
            var navMock = new Mock<INavigationService>();
            var terminationMock = new Mock<ISessionTerminationService>();
            var gstinMock = new Mock<IGstinLookupService>();

            dispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());

            var vm = new ProfileViewModel(
                repoMock.Object,
                loggerMock.Object,
                dispatcherMock.Object,
                profileServiceMock.Object,
                sessionMock.Object,
                navMock.Object,
                terminationMock.Object,
                gstinMock.Object
            );

            var view = new ProfileViewUserControl(vm);

            view.Should().NotBeNull();
            view.DataContext.Should().Be(vm);
        }

        [WpfFact]
        public void ProfileView_Card2_DisplaysPageConversionElements()
        {
            var repoMock = new Mock<ICustomerRepository>();
            var loggerMock = new Mock<ILogger<ProfileViewModel>>();
            var dispatcherMock = new Mock<IWinDispatcherService>();
            dispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());

            var vm = new ProfileViewModel(
                repoMock.Object,
                loggerMock.Object,
                dispatcherMock.Object
            );

            vm.UpdateQuotaState(new PostXML.Models.AccountQuotaInfo
            {
                PagesConvertedTotal = 150,
                PagesConvertedPeriod = 150,
                PageQuotaLimit = 5000,
                SubscriptionStatus = "active"
            });

            var view = new ProfileViewUserControl(vm);
            view.Measure(new System.Windows.Size(1200, 800));
            view.Arrange(new System.Windows.Rect(0, 0, 1200, 800));
            view.UpdateLayout();

            var textBlocks = FindVisualChildren<System.Windows.Controls.TextBlock>(view);
            var pagesConvertedText = System.Linq.Enumerable.FirstOrDefault(textBlocks, tb =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(tb) == "Profile_TxtPagesConverted");
            pagesConvertedText.Should().NotBeNull();
            pagesConvertedText!.Text.Should().Be("150 pages");

            var allowanceText = System.Linq.Enumerable.FirstOrDefault(textBlocks, tb =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(tb) == "Profile_TxtPageAllowance");
            allowanceText.Should().NotBeNull();
            allowanceText!.Text.Should().Be("150 / 5,000 pages (Fair Use)");
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject? depObj) where T : System.Windows.DependencyObject
        {
            if (depObj == null) yield break;

            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
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
