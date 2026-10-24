using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Postaze.Models;
using Postaze.Repositories;
using Postaze.Services;
using PostXML;
using PostXML.Models;
using PostXML.Services;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest
{
    public class AutoLoginTest
    {
        private readonly Mock<IProfileService> _profileMock = new();
        private readonly Mock<ICustomerRepository> _repoMock = new();
        private readonly Mock<ICustomerSessionService> _sessionMock = new();
        private readonly Mock<INavigationService> _navMock = new();
        private readonly IServiceProvider _serviceProvider;

        public AutoLoginTest()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_profileMock.Object);
            services.AddSingleton(_repoMock.Object);
            services.AddSingleton(_sessionMock.Object);
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task PerformAutoLoginAsync_WhenNoLastLoggedUser_NavigatesToLogin()
        {
            // Arrange
            _profileMock.Setup(p => p.GetLastLoggedUserId()).Returns((string?)null);

            // Act
            await App.PerformAutoLoginAsync(_serviceProvider, _navMock.Object);

            // Assert
            _navMock.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Once);
            _navMock.Verify(n => n.NavigateTo<DashboardViewModel>(It.IsAny<Action<DashboardViewModel>>()), Times.Never);
        }

        [Fact]
        public async Task PerformAutoLoginAsync_WhenProfileHasNoToken_NavigatesToLogin()
        {
            // Arrange
            _profileMock.Setup(p => p.GetLastLoggedUserId()).Returns("MANU100021");
            _profileMock.Setup(p => p.GetProfileAsync("MANU100021"))
                .ReturnsAsync(new UserAppStateProfile { PostazeId = "MANU100021", AuthToken = null });

            // Act
            await App.PerformAutoLoginAsync(_serviceProvider, _navMock.Object);

            // Assert
            _navMock.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Once);
            _navMock.Verify(n => n.NavigateTo<DashboardViewModel>(It.IsAny<Action<DashboardViewModel>>()), Times.Never);
        }

        [Fact]
        public async Task PerformAutoLoginAsync_WhenTokenIsValid_RestoresSessionAndNavigatesToDashboard()
        {
            // Arrange
            var customer = new Customer
            {
                CustomerId = 100021,
                Category = CustomerCategory.MANUFACTURER,
                EmailId = "owner@factory.com"
            };

            _profileMock.Setup(p => p.GetLastLoggedUserId()).Returns("MANU100021");
            _profileMock.Setup(p => p.GetProfileAsync("MANU100021"))
                .ReturnsAsync(new UserAppStateProfile
                {
                    PostazeId = "MANU100021",
                    EmailId = "owner@factory.com",
                    AuthToken = "valid_live_bearer_token"
                });

            _repoMock.Setup(r => r.AuthenticateWithTokenAsync("valid_live_bearer_token"))
                .ReturnsAsync(customer);

            // Act
            await App.PerformAutoLoginAsync(_serviceProvider, _navMock.Object);

            // Assert
            _navMock.Verify(n => n.NavigateTo<DashboardViewModel>(It.IsAny<Action<DashboardViewModel>>()), Times.Once);
            _navMock.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Never);
            _sessionMock.VerifySet(s => s.Customer = customer, Times.Once);
            _sessionMock.VerifySet(s => s.Token = "valid_live_bearer_token", Times.Once);
        }

        [Fact]
        public async Task PerformAutoLoginAsync_WhenTokenIsRejected_NavigatesToLogin()
        {
            // Arrange
            _profileMock.Setup(p => p.GetLastLoggedUserId()).Returns("MANU100021");
            _profileMock.Setup(p => p.GetProfileAsync("MANU100021"))
                .ReturnsAsync(new UserAppStateProfile
                {
                    PostazeId = "MANU100021",
                    AuthToken = "expired_or_invalid_token"
                });

            _repoMock.Setup(r => r.AuthenticateWithTokenAsync("expired_or_invalid_token"))
                .ReturnsAsync((Customer?)null);

            // Act
            await App.PerformAutoLoginAsync(_serviceProvider, _navMock.Object);

            // Assert
            _navMock.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Once);
            _navMock.Verify(n => n.NavigateTo<DashboardViewModel>(It.IsAny<Action<DashboardViewModel>>()), Times.Never);
        }

        [Fact]
        public async Task PerformAutoLoginAsync_WhenExceptionOccurs_GracefullyFallsBackToLogin()
        {
            // Arrange
            _profileMock.Setup(p => p.GetLastLoggedUserId()).Throws(new InvalidOperationException("Disk failure"));

            // Act
            await App.PerformAutoLoginAsync(_serviceProvider, _navMock.Object);

            // Assert
            _navMock.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Once);
        }
    }
}
