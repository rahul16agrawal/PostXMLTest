using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Postaze.Configurations;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.ViewModels;
using PostXML.Views;
using Xunit;

namespace PostXMLTest.Views
{
    public class RegisterViewUserControlTest
    {
        [WpfFact]
        public void Constructor_SetsDataContextAndInitializes()
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
                ApplicationTagLine = "Tagline"
            });

            var vm = new RegisterViewModel(navMock.Object, repoMock.Object, loggerMock.Object, configMock.Object, featuresMock.Object);

            // Act
            var view = new RegisterViewUserControl(vm);

            // Assert
            view.Should().NotBeNull();
            view.DataContext.Should().Be(vm);
        }
    }
}
