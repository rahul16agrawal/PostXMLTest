using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.ViewModels;
using PostXML.Views;
using Xunit;

namespace PostXMLTest.Views
{
    public class DashboardUserControlTest
    {
        [WpfFact]
        public void Constructor_SetsDataContextAndInitializes()
        {
            // Arrange
            var navMock = new Mock<INavigationService>();
            var repoMock = new Mock<ICustomerRepository>();
            var loggerMock = new Mock<ILogger<DashboardViewModel>>();

            var vm = new DashboardViewModel(navMock.Object, repoMock.Object, loggerMock.Object);

            // Act
            var view = new DashboardUserControl(vm);

            // Assert
            view.Should().NotBeNull();
            view.DataContext.Should().Be(vm);
        }
    }
}
