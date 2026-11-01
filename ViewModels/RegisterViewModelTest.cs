using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Postaze.Configurations;
using Postaze.Models;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest.ViewModels
{
    public class RegisterViewModelTest
    {
        private readonly Mock<INavigationService> _navMock;
        private readonly Mock<ICustomerRepository> _repoMock;
        private readonly Mock<ILogger<RegisterViewModel>> _loggerMock;
        private readonly IOptions<CustomerDefaultConfig> _configOptions;
        private readonly IOptions<PostazeSoftwareFeatures> _featuresOptions;

        public RegisterViewModelTest()
        {
            _navMock = new Mock<INavigationService>();
            _repoMock = new Mock<ICustomerRepository>();
            _loggerMock = new Mock<ILogger<RegisterViewModel>>();
            _configOptions = Options.Create(new CustomerDefaultConfig());
            _featuresOptions = Options.Create(new PostazeSoftwareFeatures
            {
                ApplicationName = "PostXML",
                ApplicationTagLine = "From Bank Statement to Tally XML in Seconds"
            });
        }

        private RegisterViewModel CreateViewModel()
        {
            return new RegisterViewModel(
                _navMock.Object,
                _repoMock.Object,
                _loggerMock.Object,
                _configOptions,
                _featuresOptions);
        }

        [Fact]
        public void NavigateToLoginCommand_Executes_NavigatesToLoginViewModel()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.NavigateToLoginViewUserControlCommand.Execute(null);

            // Assert
            _navMock.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Once);
        }

        [Fact]
        public void CustomerCategories_ContainsAccountantAndCaFirm()
        {
            // Arrange
            var vm = CreateViewModel();

            // Assert
            vm.CustomerCategories.Should().Contain(CustomerCategory.ACCOUNTANT);
            vm.CustomerCategories.Should().Contain(CustomerCategory.CA_FIRM);
            vm.CustomerCategories.Should().Contain(CustomerCategory.RETAILER);
            vm.CustomerCategories.Should().Contain(CustomerCategory.MANUFACTURER);
        }

        [Theory]
        [InlineData(CustomerCategory.ACCOUNTANT, false, "GSTIN (Optional)")]
        [InlineData(CustomerCategory.CA_FIRM, true, "GSTIN *")]
        [InlineData(CustomerCategory.RETAILER, true, "GSTIN *")]
        [InlineData(CustomerCategory.MANUFACTURER, true, "GSTIN *")]
        [InlineData(CustomerCategory.WHOLESALER, true, "GSTIN *")]
        [InlineData(CustomerCategory.AADHATIYA, true, "GSTIN *")]
        [InlineData(CustomerCategory.AGENT, true, "GSTIN *")]
        [InlineData(CustomerCategory.TRANSPORTER, true, "GSTIN *")]
        public void SelectedCustomerCategory_UpdatesGstinMandatoryAndLabel(
            CustomerCategory category,
            bool expectedMandatory,
            string expectedLabel)
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.SelectedCustomerCategory = category;

            // Assert
            vm.IsGstinMandatory.Should().Be(expectedMandatory);
            vm.GstinFieldLabel.Should().Be(expectedLabel);
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_AccountantWithEmptyGstin_RegistersSuccessfullyWithNullGstin()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.ACCOUNTANT;
            vm.Gstin = "";
            vm.MobileNumber = "9876543210";
            vm.Email = "accountant@example.com";

            Customer? capturedCustomer = null;
            _repoMock.Setup(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()))
                     .Callback<Customer, Mobile>((c, m) => capturedCustomer = c)
                     .ReturnsAsync(new RestApiResponse<Customer>(true, null, null, new Customer
                     {
                         CustomerId = 100589,
                         Category = CustomerCategory.ACCOUNTANT,
                         Gstin = null
                     }));

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Once);
            capturedCustomer.Should().NotBeNull();
            capturedCustomer!.Category.Should().Be(CustomerCategory.ACCOUNTANT);
            capturedCustomer.Gstin.Should().BeNull();
            capturedCustomer.CustomerPostazeId.Should().Be("ACCO0"); // CustomerId is 0 prior to server assignment
            vm.RegisterFeedbackMessage.Should().Contain("Check your email for login details");
            vm.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_AccountantWithValidGstin_RegistersSuccessfullyWithGstin()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.ACCOUNTANT;
            vm.Gstin = "10aabcu9355j1z9"; // lowercase to test trimming and upper-casing
            vm.MobileNumber = "9876543210";
            vm.Email = "accountant@example.com";

            Customer? capturedCustomer = null;
            _repoMock.Setup(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()))
                     .Callback<Customer, Mobile>((c, m) => capturedCustomer = c)
                     .ReturnsAsync(new RestApiResponse<Customer>(true, null, null, new Customer()));

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Once);
            capturedCustomer.Should().NotBeNull();
            capturedCustomer!.Gstin.Should().Be("10AABCU9355J1Z9");
            capturedCustomer.Category.Should().Be(CustomerCategory.ACCOUNTANT);
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_AccountantWithInvalidGstinLength_FailsLocalValidationWithoutCallingRepo()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.ACCOUNTANT;
            vm.Gstin = "10AABCU"; // 7 chars, invalid
            vm.MobileNumber = "9876543210";
            vm.Email = "accountant@example.com";

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Never);
            vm.RegisterFeedbackMessage.Should().Contain("15 characters");
            vm.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_CaFirmWithEmptyGstin_FailsLocalValidationWithoutCallingRepo()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.CA_FIRM;
            vm.Gstin = "";
            vm.MobileNumber = "9876543211";
            vm.Email = "cafirm@example.com";

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert: GSTIN is strictly required for CA Firm
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Never);
            vm.RegisterFeedbackMessage.Should().Contain("Gstin is required");
            vm.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_CaFirmWithWhitespaceGstin_FailsLocalValidationWithoutCallingRepo()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.CA_FIRM;
            vm.Gstin = "   ";
            vm.MobileNumber = "9876543211";
            vm.Email = "cafirm@example.com";

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Never);
            vm.RegisterFeedbackMessage.Should().Contain("Gstin is required");
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_CaFirmWithValidGstin_RegistersSuccessfullyWithGstin()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.CA_FIRM;
            vm.Gstin = "10aaacb1534f1zl";
            vm.MobileNumber = "9876543211";
            vm.Email = "cafirm@example.com";

            Customer? capturedCustomer = null;
            _repoMock.Setup(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()))
                     .Callback<Customer, Mobile>((c, m) => capturedCustomer = c)
                     .ReturnsAsync(new RestApiResponse<Customer>(true, null, null, new Customer
                     {
                         CustomerId = 100789,
                         Category = CustomerCategory.CA_FIRM,
                         Gstin = "10AAACB1534F1ZL"
                     }));

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Once);
            capturedCustomer.Should().NotBeNull();
            capturedCustomer!.Category.Should().Be(CustomerCategory.CA_FIRM);
            capturedCustomer.Gstin.Should().Be("10AAACB1534F1ZL");
            capturedCustomer.CustomerPostazeId.Should().Be("CAFI0");
            vm.RegisterFeedbackMessage.Should().Contain("Check your email for login details");
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_CaFirmWithInvalidGstinLength_FailsLocalValidationWithoutCallingRepo()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.CA_FIRM;
            vm.Gstin = "10AAACB"; // 7 chars, invalid
            vm.MobileNumber = "9876543211";
            vm.Email = "cafirm@example.com";

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Never);
            vm.RegisterFeedbackMessage.Should().Contain("15 characters");
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_RetailerWithEmptyGstin_FailsLocalValidationWithoutCallingRepo()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.RETAILER;
            vm.Gstin = "";
            vm.MobileNumber = "9876543210";
            vm.Email = "retailer@example.com";

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Never);
            vm.RegisterFeedbackMessage.Should().Contain("Gstin is required");
            vm.IsProcessing.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteRegisterCommandAsync_RetailerWithInvalidGstinLength_FailsLocalValidationWithoutCallingRepo()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.SelectedCustomerCategory = CustomerCategory.RETAILER;
            vm.Gstin = "10AAACB1534F1"; // 13 chars
            vm.MobileNumber = "9876543210";
            vm.Email = "retailer@example.com";

            // Act
            await vm.RegisterCommand.ExecuteAsync(null);

            // Assert
            _repoMock.Verify(r => r.RegisterCustomerAsync(It.IsAny<Customer>(), It.IsAny<Mobile>()), Times.Never);
            vm.RegisterFeedbackMessage.Should().Contain("15 characters");
        }
    }
}
