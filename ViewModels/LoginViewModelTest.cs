using System;
using System.Security;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Postaze.Configurations;
using Postaze.Models;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.Models;
using PostXML.Services;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest.ViewModels
{
    public class LoginViewModelTest
    {
        private static SecureString ToSecureString(string password)
        {
            var secure = new SecureString();
            foreach (char c in password)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }

        private static (LoginViewModel ViewModel, Mock<INavigationService> NavMock, Mock<ICustomerRepository> RepoMock, Mock<IProfileService> ProfileMock, Mock<ICustomerSessionService> SessionMock) CreateViewModel()
        {
            var navMock = new Mock<INavigationService>();
            var repoMock = new Mock<ICustomerRepository>();
            var loggerMock = new Mock<ILogger<LoginViewModel>>();
            var profileMock = new Mock<IProfileService>();
            var sessionMock = new Mock<ICustomerSessionService>();
            var featuresMock = new Mock<IOptions<PostazeSoftwareFeatures>>();

            featuresMock.Setup(f => f.Value).Returns(new PostazeSoftwareFeatures
            {
                ApplicationName = "PostXML",
                ApplicationTagLine = "From Bank Statement to Tally XML in Seconds"
            });

            sessionMock.SetupProperty(s => s.Token, "test_bearer_token");

            var vm = new LoginViewModel(
                navMock.Object,
                repoMock.Object,
                featuresMock.Object,
                loggerMock.Object,
                profileMock.Object,
                sessionMock.Object);

            return (vm, navMock, repoMock, profileMock, sessionMock);
        }

        [Fact]
        public async Task LoginCommand_WithValidCredentials_NavigatesToDashboardAndSavesProfile()
        {
            // Arrange
            var (vm, navMock, repoMock, profileMock, _) = CreateViewModel();
            var customer = new Customer
            {
                CustomerId = 42,
                Category = CustomerCategory.WHOLESALER,
                Gstin = "27AABCU9603R1ZM",
                EmailId = "ca@firm.com"
            };

            repoMock.Setup(r => r.AuthenticateCustomerAsync("validUser", It.IsAny<SecureString>()))
                .ReturnsAsync(customer);

            vm.Username = "validUser";
            vm.Password = ToSecureString("secret");

            // Act
            await vm.LoginCommand.ExecuteAsync(null);

            // Assert
            navMock.Verify(n => n.NavigateTo<DashboardViewModel>(It.IsAny<Action<DashboardViewModel>>()), Times.Once);
            profileMock.Verify(p => p.SaveProfileAsync(It.Is<UserAppStateProfile>(prof => prof.PostazeId == "WHOL42" && prof.AuthToken == "test_bearer_token")), Times.Once);
            vm.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task LoginCommand_WithInvalidCredentials_ShowsErrorAndDoesNotNavigate()
        {
            // Arrange
            var (vm, navMock, repoMock, profileMock, _) = CreateViewModel();

            repoMock.Setup(r => r.AuthenticateCustomerAsync("badUser", It.IsAny<SecureString>()))
                .ReturnsAsync((Customer?)null);

            vm.Username = "badUser";
            vm.Password = ToSecureString("wrong");

            // Act
            await vm.LoginCommand.ExecuteAsync(null);

            // Assert
            navMock.Verify(n => n.NavigateTo<DashboardViewModel>(It.IsAny<Action<DashboardViewModel>>()), Times.Never);
            profileMock.Verify(p => p.SaveProfileAsync(It.IsAny<UserAppStateProfile>()), Times.Never);
            vm.LoginFeedbackMessage.Should().NotBeNullOrWhiteSpace();
            vm.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task LoginCommand_WhenRepositoryThrowsException_SetsFeedbackMessageAndResetsProcessing()
        {
            // Arrange
            var (vm, navMock, repoMock, _, _) = CreateViewModel();

            repoMock.Setup(r => r.AuthenticateCustomerAsync(It.IsAny<string>(), It.IsAny<SecureString>()))
                .ThrowsAsync(new InvalidOperationException("API connection failure"));

            vm.Username = "anyUser";
            vm.Password = ToSecureString("anyPass");

            // Act
            await vm.LoginCommand.ExecuteAsync(null);

            // Assert
            navMock.Verify(n => n.NavigateTo<DashboardViewModel>(It.IsAny<Action<DashboardViewModel>>()), Times.Never);
            vm.LoginFeedbackMessage.Should().NotBeNullOrWhiteSpace();
            vm.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task LoginCommand_WhenNavigationThrowsException_HandlesGracefullyAndSetsFeedback()
        {
            // Arrange
            var (vm, navMock, repoMock, _, _) = CreateViewModel();
            var customer = new Customer
            {
                CustomerId = 1,
                Category = CustomerCategory.RETAILER,
                Gstin = "27AABCU9603R1ZM",
                EmailId = "test@example.com"
            };

            repoMock.Setup(r => r.AuthenticateCustomerAsync(It.IsAny<string>(), It.IsAny<SecureString>()))
                .ReturnsAsync(customer);

            navMock.Setup(n => n.NavigateTo<DashboardViewModel>(It.IsAny<Action<DashboardViewModel>>()))
                .Throws(new InvalidOperationException("Host view failure"));

            vm.Username = "validUser";
            vm.Password = ToSecureString("pass");

            // Act
            await vm.LoginCommand.ExecuteAsync(null);

            // Assert
            vm.LoginFeedbackMessage.Should().NotBeNullOrWhiteSpace();
            vm.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public void NavigateToRegisterCommand_Executes_NavigatesToRegisterViewModel()
        {
            // Arrange
            var (vm, navMock, _, _, _) = CreateViewModel();

            // Act
            vm.NavigateToRegisterViewUserControlCommand.Execute(null);

            // Assert
            navMock.Verify(n => n.NavigateTo<RegisterViewModel>(), Times.Once);
        }

        [Fact]
        public void Username_And_Password_PropertyChanges_NotifyCorrectly()
        {
            // Arrange
            var (vm, _, _, _, _) = CreateViewModel();
            string? changedProperty = null;
            vm.PropertyChanged += (sender, args) => changedProperty = args.PropertyName;

            // Act
            vm.Username = "NewUsername";

            // Assert
            changedProperty.Should().Be(nameof(vm.Username));
            vm.Username.Should().Be("NewUsername");
        }
    }
}
