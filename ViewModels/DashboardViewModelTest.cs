using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Postaze.Models;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest.ViewModels
{
    public class DashboardViewModelTest
    {
        private readonly Mock<INavigationService> _navMock;
        private readonly Mock<ICustomerRepository> _repoMock;
        private readonly Mock<ILogger<DashboardViewModel>> _loggerMock;
        private readonly DashboardViewModel _viewModel;

        public DashboardViewModelTest()
        {
            _navMock = new Mock<INavigationService>();
            _repoMock = new Mock<ICustomerRepository>();
            _loggerMock = new Mock<ILogger<DashboardViewModel>>();

            _viewModel = new DashboardViewModel(_navMock.Object, _repoMock.Object, _loggerMock.Object);
        }

        [Fact]
        public void Constructor_SetsUserControlTitleToPostXML()
        {
            _viewModel.UserControlTitle.Should().Be("POSTXML");
        }

        [Fact]
        public void Initialize_SetsAuthenticatedCustomer()
        {
            // Arrange
            var customer = new Customer
            {
                CustomerId = 10,
                Category = CustomerCategory.WHOLESALER,
                Gstin = "27AABCU9603R1ZM"
            };

            // Act
            _viewModel.Initialize(customer);

            // Assert
            _viewModel.AuthenticatedCustomer.Should().Be(customer);
        }

        [Fact]
        public void NavigateToHomeCommand_SetsSelectedTabHome()
        {
            // Act
            _viewModel.NavigateToHomeViewUserControlCommand.Execute(null);

            // Assert
            _viewModel.SelectedTab.Should().Be("Home");
        }

        [Fact]
        public void NavigateToProfileCommand_SetsSelectedTabProfile()
        {
            // Act
            _viewModel.NavigateToProfileViewUserControlCommand.Execute(null);

            // Assert
            _viewModel.SelectedTab.Should().Be("Profile");
        }

        [Fact]
        public void CurrentViewProperty_CanBeSetAndRaisesPropertyChanged()
        {
            var dummyView = new object();
            bool eventRaised = false;
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.CurrentView))
                {
                    eventRaised = true;
                }
            };

            _viewModel.CurrentView = dummyView;
            _viewModel.CurrentView.Should().Be(dummyView);
            eventRaised.Should().BeTrue();
        }
    }
}
