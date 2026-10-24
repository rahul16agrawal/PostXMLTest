using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Postaze.Models;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest.ViewModels
{
    public class HomeViewModelTest
    {
        private readonly Mock<ILogger<HomeViewModel>> _loggerMock;
        private readonly HomeViewModel _viewModel;

        public HomeViewModelTest()
        {
            _loggerMock = new Mock<ILogger<HomeViewModel>>();
            _viewModel = new HomeViewModel(_loggerMock.Object);
        }

        [Fact]
        public void Constructor_SetsDefaultValues()
        {
            _viewModel.WelcomeHeadline.Should().Be("Bank Statement to Tally Prime XML Converter");
            _viewModel.WelcomeSubtitle.Should().Contain("Convert bank statements");
            _viewModel.StatusMessage.Should().Be("Ready to upload statement.");
            _viewModel.IsProcessing.Should().BeFalse();
            _viewModel.SelectedStatementFilePath.Should().BeEmpty();
            _viewModel.AuthenticatedCustomer.Should().BeNull();
        }

        [Fact]
        public void Initialize_SetsAuthenticatedCustomer()
        {
            // Arrange
            var customer = new Customer
            {
                CustomerId = 42,
                Category = CustomerCategory.WHOLESALER,
                Gstin = "27AABCU9603R1ZM",
                EmailId = "user@postxml.com"
            };

            // Act
            _viewModel.Initialize(customer);

            // Assert
            _viewModel.AuthenticatedCustomer.Should().Be(customer);
        }
    }
}
