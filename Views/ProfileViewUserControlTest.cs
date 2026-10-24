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
    }
}
