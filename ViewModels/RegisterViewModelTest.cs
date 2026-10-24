using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Postaze.Configurations;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest.ViewModels
{
    public class RegisterViewModelTest
    {
        [Fact]
        public void NavigateToLoginCommand_Executes_NavigatesToLoginViewModel()
        {
            // Arrange
            var navMock = new Mock<INavigationService>();
            var repoMock = new Mock<ICustomerRepository>();
            var loggerMock = new Mock<ILogger<RegisterViewModel>>();
            var configMock = new Mock<IOptions<CustomerDefaultConfig>>();
            configMock.Setup(c => c.Value).Returns(new CustomerDefaultConfig());

            var featuresMock = new Mock<IOptions<PostazeSoftwareFeatures>>();
            featuresMock.Setup(f => f.Value).Returns(new PostazeSoftwareFeatures
            {
                ApplicationName = "PostXML",
                ApplicationTagLine = "From Bank Statement to Tally XML in Seconds"
            });

            var vm = new RegisterViewModel(
                navMock.Object,
                repoMock.Object,
                loggerMock.Object,
                configMock.Object,
                featuresMock.Object);

            // Act
            vm.NavigateToLoginViewUserControlCommand.Execute(null);

            // Assert
            navMock.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Once);
        }
    }
}
