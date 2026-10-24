using System;
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

namespace PostXMLTest.Services
{
    public class SessionTerminationServiceTest
    {
        private readonly Mock<ILogger<SessionTerminationService>> _loggerMock;
        private readonly Mock<IProfileService> _profileMock;
        private readonly Mock<ICustomerSessionService> _sessionMock;
        private readonly Mock<INavigationService> _navMock;
        private readonly Mock<IWinDispatcherService> _dispatcherMock;
        private readonly SessionTerminationService _service;

        public SessionTerminationServiceTest()
        {
            _loggerMock = new Mock<ILogger<SessionTerminationService>>();
            _profileMock = new Mock<IProfileService>();
            _sessionMock = new Mock<ICustomerSessionService>();
            _navMock = new Mock<INavigationService>();
            _dispatcherMock = new Mock<IWinDispatcherService>();

            _dispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());

            _service = new SessionTerminationService(
                _loggerMock.Object,
                _profileMock.Object,
                _sessionMock.Object,
                _navMock.Object,
                _dispatcherMock.Object
            );
        }

        [Fact]
        public async Task TerminateSessionAsync_ClearsTokenAndNavigatesToLogin()
        {
            // Arrange
            var customer = new Customer
            {
                CustomerId = 42,
                Category = CustomerCategory.WHOLESALER
            };
            _sessionMock.SetupGet(s => s.Customer).Returns(customer);

            var profile = new UserAppStateProfile
            {
                AuthToken = "existing-token"
            };
            _profileMock.Setup(p => p.GetProfileAsync(customer.CustomerPostazeId))
                .ReturnsAsync(profile);

            // Act
            await _service.TerminateSessionAsync("Session expired");

            // Assert
            profile.AuthToken.Should().BeNull();
            profile.TokenExpiry.Should().BeNull();
            _profileMock.Verify(p => p.SaveProfileAsync(profile), Times.Once);

            _sessionMock.VerifySet(s => s.Customer = null);
            _sessionMock.VerifySet(s => s.Token = null);

            _navMock.Verify(n => n.NavigateTo<LoginViewModel>(It.IsAny<Action<LoginViewModel>>()), Times.Once);
        }
    }
}
