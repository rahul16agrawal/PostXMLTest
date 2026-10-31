using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Postaze.Models;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.Models;
using PostXML.Services;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest.ViewModels
{
    public class ProfileViewModelTest
    {
        private readonly Mock<ICustomerRepository> _mockRepo;
        private readonly Mock<ILogger<ProfileViewModel>> _mockLogger;
        private readonly Mock<IWinDispatcherService> _mockDispatcher;
        private readonly Mock<IProfileService> _mockProfileService;
        private readonly Mock<ICustomerSessionService> _mockSessionService;
        private readonly Mock<INavigationService> _mockNavService;
        private readonly Mock<ISessionTerminationService> _mockTerminationService;
        private readonly Mock<IGstinLookupService> _mockGstinService;
        private readonly ProfileViewModel _viewModel;

        public ProfileViewModelTest()
        {
            _mockRepo = new Mock<ICustomerRepository>();
            _mockLogger = new Mock<ILogger<ProfileViewModel>>();
            _mockDispatcher = new Mock<IWinDispatcherService>();
            _mockProfileService = new Mock<IProfileService>();
            _mockSessionService = new Mock<ICustomerSessionService>();
            _mockNavService = new Mock<INavigationService>();
            _mockTerminationService = new Mock<ISessionTerminationService>();
            _mockGstinService = new Mock<IGstinLookupService>();

            _mockDispatcher.Setup(d => d.InvokeAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());

            _viewModel = new ProfileViewModel(
                _mockRepo.Object,
                _mockLogger.Object,
                _mockDispatcher.Object,
                _mockProfileService.Object,
                _mockSessionService.Object,
                _mockNavService.Object,
                _mockTerminationService.Object,
                _mockGstinService.Object
            );
        }

        #region Initialization & Commands CanExecute

        [Fact]
        public void Initialize_ShouldSetCustomerAndNotifyCommands()
        {
            // Arrange
            var customer = new Customer
            {
                CustomerId = 100,
                Gstin = "09ABCFM6333M1ZX",
                Category = CustomerCategory.RETAILER,
                Mobiles = new List<Mobile>
                {
                    new Mobile { MobileNumber = "9876543210", CountryCallingCode = 91, WhatsappEnabled = true }
                }
            };

            // Act
            _viewModel.Initialize(customer);

            // Assert
            _viewModel.AuthenticatedCustomer.Should().Be(customer);
            _viewModel.RechargeCommand.CanExecute(null).Should().BeTrue();
            _viewModel.ProfileSaveCommand.CanExecute(null).Should().BeTrue();
            _viewModel.RegisteredMobiles.Should().HaveCount(1);
            _viewModel.HasRegisteredMobiles.Should().BeTrue();
            _viewModel.HasNoRegisteredMobiles.Should().BeFalse();
        }

        [Fact]
        public void CanExecuteRechargeCommand_WhenCustomerNull_ShouldReturnFalse()
        {
            _viewModel.RechargeCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void CanExecuteRechargeCommand_WhenGstinNull_ShouldReturnFalse()
        {
            var customer = new Customer { CustomerId = 100, Gstin = null!, Category = CustomerCategory.RETAILER };
            _viewModel.Initialize(customer);
            _viewModel.RechargeCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void CustomerInitials_ShouldReflectCustomerCategoryOrId()
        {
            var customer = new Customer { CustomerId = 100, Category = CustomerCategory.WHOLESALER };
            _viewModel.Initialize(customer);
            _viewModel.CustomerInitials.Should().Be("WH");

            var c2 = new Customer { CustomerId = 42, Category = CustomerCategory.MANUFACTURER };
            _viewModel.Initialize(c2);
            _viewModel.CustomerInitials.Should().Be("MA");
        }

        #endregion

        #region Dynamic Mobiles Management

        [Fact]
        public void StartAddMobileCommand_AddsEmptyMobileRow()
        {
            var customer = new Customer { CustomerId = 10 };
            _viewModel.Initialize(customer);

            _viewModel.StartAddMobileCommand.Execute(null);

            _viewModel.RegisteredMobiles.Should().HaveCount(1);
            _viewModel.RegisteredMobiles[0].MobileNumber.Should().BeEmpty();
            _viewModel.RegisteredMobiles[0].WhatsappEnabled.Should().BeTrue();
            _viewModel.SaveFeedbackMessage.Should().Contain("New mobile row added");
        }

        [Fact]
        public void ConfirmAddMobile_WithInvalidLength_SetsValidationError()
        {
            _viewModel.NewMobileNumber = "12345";

            _viewModel.ConfirmAddMobileCommand.Execute(null);

            _viewModel.MobileValidationMessage.Should().Contain("must be exactly 10 digits");
            _viewModel.HasMobileValidationMessage.Should().BeTrue();
        }

        [Fact]
        public void ConfirmAddMobile_WithDuplicateNumber_SetsValidationError()
        {
            var customer = new Customer
            {
                CustomerId = 10,
                Mobiles = new List<Mobile>
                {
                    new Mobile { MobileNumber = "9876543210" }
                }
            };
            _viewModel.Initialize(customer);

            _viewModel.NewMobileNumber = "9876543210";
            _viewModel.ConfirmAddMobileCommand.Execute(null);

            _viewModel.MobileValidationMessage.Should().Contain("already added");
        }

        [Fact]
        public void ConfirmAddMobile_WithValidNumber_AddsMobileSuccessfully()
        {
            var customer = new Customer { CustomerId = 10 };
            _viewModel.Initialize(customer);

            _viewModel.NewMobileNumber = "9876543210";
            _viewModel.NewMobileWhatsappEnabled = true;

            _viewModel.ConfirmAddMobileCommand.Execute(null);

            _viewModel.RegisteredMobiles.Should().HaveCount(1);
            _viewModel.RegisteredMobiles[0].MobileNumber.Should().Be("9876543210");
            _viewModel.MobileValidationMessage.Should().BeEmpty();
        }

        [Fact]
        public void CancelAddMobileCommand_ResetsInputs()
        {
            _viewModel.NewMobileNumber = "9876543210";
            _viewModel.MobileValidationMessage = "Some error";

            _viewModel.CancelAddMobileCommand.Execute(null);

            _viewModel.NewMobileNumber.Should().BeEmpty();
            _viewModel.MobileValidationMessage.Should().BeEmpty();
        }

        [Fact]
        public void RemoveMobileCommand_RemovesMobile()
        {
            var mob = new Mobile { MobileNumber = "9876543210" };
            _viewModel.RegisteredMobiles.Add(mob);

            _viewModel.RemoveMobileCommand.Execute(mob);

            _viewModel.RegisteredMobiles.Should().BeEmpty();
            _viewModel.SaveFeedbackMessage.Should().Contain("Mobile removed");
        }

        #endregion

        #region Password Change Modal

        [Fact]
        public void OpenAndClosePasswordModal_TogglesModalState()
        {
            var customer = new Customer { CustomerId = 10 };
            _viewModel.Initialize(customer);

            _viewModel.OpenChangePasswordModalCommand.Execute(null);
            _viewModel.IsPasswordChangeModalOpen.Should().BeTrue();

            _viewModel.CloseChangePasswordModalCommand.Execute(null);
            _viewModel.IsPasswordChangeModalOpen.Should().BeFalse();
        }

        [Fact]
        public async Task SubmitPasswordChange_WhenFieldsIncomplete_SetsErrorMessage()
        {
            var customer = new Customer { CustomerId = 10, CurrentPassword = "OldPassword123" };
            _viewModel.Initialize(customer);
            _viewModel.OpenChangePasswordModalCommand.Execute(null);

            await _viewModel.SubmitPasswordChangeCommand.ExecuteAsync(null);

            _viewModel.PasswordModalErrorMessage.Should().Contain("All password fields are required");
            _viewModel.HasPasswordModalErrorMessage.Should().BeTrue();
        }

        [Fact]
        public async Task SubmitPasswordChange_WhenNewPasswordShort_SetsErrorMessage()
        {
            var customer = new Customer
            {
                CustomerId = 10,
                CurrentPassword = "OldPassword123",
                NewPassword = "123",
                ConfirmPassword = "123"
            };
            _viewModel.Initialize(customer);

            await _viewModel.SubmitPasswordChangeCommand.ExecuteAsync(null);

            _viewModel.PasswordModalErrorMessage.Should().Contain("at least 6 characters");
        }

        [Fact]
        public async Task SubmitPasswordChange_WhenNewPasswordMatchesCurrent_SetsErrorMessage()
        {
            var customer = new Customer
            {
                CustomerId = 10,
                CurrentPassword = "Password123",
                NewPassword = "Password123",
                ConfirmPassword = "Password123"
            };
            _viewModel.Initialize(customer);

            await _viewModel.SubmitPasswordChangeCommand.ExecuteAsync(null);

            _viewModel.PasswordModalErrorMessage.Should().Contain("cannot be the same as the current password");
        }

        [Fact]
        public async Task SubmitPasswordChange_WhenPasswordsDoNotMatch_SetsErrorMessage()
        {
            var customer = new Customer
            {
                CustomerId = 10,
                CurrentPassword = "OldPassword123",
                NewPassword = "NewPassword123",
                ConfirmPassword = "MismatchPassword123"
            };
            _viewModel.Initialize(customer);

            await _viewModel.SubmitPasswordChangeCommand.ExecuteAsync(null);

            _viewModel.PasswordModalErrorMessage.Should().Contain("do not match");
        }

        [Fact]
        public async Task SubmitPasswordChange_WhenApiSucceeds_ClosesModalAndShowsSuccess()
        {
            var customer = new Customer
            {
                CustomerId = 10
            };
            _viewModel.Initialize(customer);
            _viewModel.OpenChangePasswordModalCommand.Execute(null);

            // User enters passwords after opening modal
            customer.CurrentPassword = "OldPassword123";
            customer.NewPassword = "NewPassword123";
            customer.ConfirmPassword = "NewPassword123";

            _mockRepo.Setup(r => r.UpdateCustomerAsync(It.IsAny<Customer>()))
                .ReturnsAsync(new RestApiResponse<Customer> { Success = true, Data = customer });

            await _viewModel.SubmitPasswordChangeCommand.ExecuteAsync(null);

            _viewModel.IsPasswordChangeModalOpen.Should().BeFalse();
            _viewModel.SaveFeedbackMessage.Should().Contain("Password updated successfully");
        }

        [Fact]
        public async Task SubmitPasswordChange_WhenApiFails_SetsErrorMessage()
        {
            var customer = new Customer
            {
                CustomerId = 10,
                CurrentPassword = "OldPassword123",
                NewPassword = "NewPassword123",
                ConfirmPassword = "NewPassword123"
            };
            _viewModel.Initialize(customer);

            _mockRepo.Setup(r => r.UpdateCustomerAsync(It.IsAny<Customer>()))
                .ReturnsAsync(new RestApiResponse<Customer> { Success = false, Message = "Invalid current password" });

            await _viewModel.SubmitPasswordChangeCommand.ExecuteAsync(null);

            _viewModel.PasswordModalErrorMessage.Should().Be("Invalid current password");
        }

        #endregion

        #region Profile Save & Validation

        [Fact]
        public async Task ProfileSaveCommand_WithInvalidMobileDigits_SetsError()
        {
            var customer = new Customer { CustomerId = 10 };
            _viewModel.Initialize(customer);
            _viewModel.RegisteredMobiles.Add(new Mobile { MobileNumber = "12345" });

            await _viewModel.ProfileSaveCommand.ExecuteAsync(null);

            _viewModel.SaveFeedbackMessage.Should().Contain("must be exactly 10 digits");
        }

        [Fact]
        public async Task ProfileSaveCommand_WithDuplicateMobiles_SetsError()
        {
            var customer = new Customer { CustomerId = 10 };
            _viewModel.Initialize(customer);
            _viewModel.RegisteredMobiles.Add(new Mobile { MobileNumber = "9876543210" });
            _viewModel.RegisteredMobiles.Add(new Mobile { MobileNumber = "9876543210" });

            await _viewModel.ProfileSaveCommand.ExecuteAsync(null);

            _viewModel.SaveFeedbackMessage.Should().Contain("Duplicate mobile number");
        }

        [Fact]
        public async Task ProfileSaveCommand_WhenApiSucceeds_UpdatesCustomer()
        {
            var customer = new Customer
            {
                CustomerId = 10,
                Category = CustomerCategory.WHOLESALER,
                Gstin = "27AABCU9603R1ZM",
                EmailId = "valid@postxml.com"
            };
            _viewModel.Initialize(customer);

            _mockRepo.Setup(r => r.UpdateCustomerAsync(It.IsAny<Customer>()))
                .ReturnsAsync(new RestApiResponse<Customer> { Success = true, Data = customer });

            await _viewModel.ProfileSaveCommand.ExecuteAsync(null);

            _viewModel.SaveFeedbackMessage.Should().Contain("Profile updated successfully");
        }

        #endregion

        #region GSTIN Details & Computed Properties

        [Fact]
        public async Task RefreshGstinDetailsAsync_WhenLookupSucceeds_SetsGstinDetails()
        {
            var customer = new Customer { CustomerId = 10, Gstin = "27AABCU9603R1ZM" };
            _viewModel.Initialize(customer);

            var gstinData = new GstinDataDto
            {
                TradeName = "Acme Traders",
                LegalName = "Acme Corp Ltd",
                Status = "Active",
                TaxpayerType = "Regular",
                BusinessConstitution = "Private Limited",
                Address = new GstinAddressDto
                {
                    FullAddress = "123 Business Park",
                    City = "Mumbai",
                    StateCode = "27",
                    Pincode = "400001"
                }
            };

            _mockGstinService.Setup(g => g.LookupGstinAsync("27AABCU9603R1ZM", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new GstinLookupApiResponse { Success = true, Data = gstinData });

            await _viewModel.RefreshGstinDetailsAsync();

            _viewModel.GstinDetails.Should().Be(gstinData);
            _viewModel.TradeNameDisplay.Should().Be("Acme Traders");
            _viewModel.LegalNameDisplay.Should().Be("Acme Corp Ltd");
            _viewModel.GstinStatusBadgeText.Should().Be("● Active");
            _viewModel.GstinStatusBadgeBackground.Should().Be("#DCFCE7");
            _viewModel.FormattedRegisteredAddress.Should().Contain("123 Business Park");
        }

        [Fact]
        public async Task RefreshGstinDetailsAsync_WhenLookupFails_SetsErrorMessage()
        {
            var customer = new Customer { CustomerId = 10, Gstin = "27AABCU9603R1ZM" };
            _viewModel.Initialize(customer);

            _mockGstinService.Setup(g => g.LookupGstinAsync("27AABCU9603R1ZM", It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new GstinLookupApiResponse { Success = false, Message = "GSTIN not found" });

            await _viewModel.RefreshGstinDetailsAsync();

            _viewModel.GstinDetails.Should().BeNull();
            _viewModel.GstinErrorMessage.Should().Be("GSTIN not found");
            _viewModel.HasGstinError.Should().BeTrue();
        }

        #endregion

        #region Logout & Telemetry

        [Fact]
        public async Task LogoutCommand_InvokesSessionTerminationService()
        {
            await _viewModel.LogoutCommand.ExecuteAsync(null);

            _mockTerminationService.Verify(t => t.TerminateSessionAsync(null), Times.Once);
        }

        [Fact]
        public async Task LogoutCommand_WithoutTerminationService_NavigatesToLoginAndClearsState()
        {
            var vm = new ProfileViewModel(
                _mockRepo.Object,
                _mockLogger.Object,
                _mockDispatcher.Object,
                _mockProfileService.Object,
                _mockSessionService.Object,
                _mockNavService.Object,
                null,
                _mockGstinService.Object
            );

            var customer = new Customer { CustomerId = 10, Category = CustomerCategory.WHOLESALER };
            vm.Initialize(customer);

            var profile = new UserAppStateProfile { AuthToken = "abc" };
            _mockProfileService.Setup(p => p.GetProfileAsync(customer.CustomerPostazeId)).ReturnsAsync(profile);

            await vm.ExecuteLogoutAsync();

            profile.AuthToken.Should().BeNull();
            _mockSessionService.VerifySet(s => s.Customer = null);
            _mockNavService.Verify(n => n.NavigateTo<LoginViewModel>(), Times.Once);
        }

        [Fact]
        public async Task RefreshSubscriptionCommand_UpdatesFeedbackMessage()
        {
            await _viewModel.RefreshSubscriptionCommand.ExecuteAsync(null);

            _viewModel.SubscriptionFeedbackMessage.Should().Be("Plan details up to date.");
        }

        [Fact]
        public async Task RechargeCommand_WhenApiFails_SetsSaveFeedbackMessage()
        {
            var customer = new Customer { CustomerId = 10, Gstin = "27AABCU9603R1ZM", Category = CustomerCategory.WHOLESALER };
            _viewModel.Initialize(customer);

            _mockRepo.Setup(r => r.GeneratePaymentLinkAsync(customer))
                .ReturnsAsync(new RestApiResponse<HttpPaymentLinkResponse> { Success = false, Message = "Gateway offline" });

            await _viewModel.RechargeCommand.ExecuteAsync(null);

            _viewModel.SaveFeedbackMessage.Should().Be("Gateway offline");
        }

        [Fact]
        public void MachineTelemetryDisplay_ContainsMachineName()
        {
            _viewModel.MachineTelemetryDisplay.Should().Contain(Environment.MachineName);
        }

        #endregion

        #region PostXML Page Quota & Upgrade Telemetry

        [Fact]
        public void PageConversionBindings_ReflectQuotaInfoCorrectly()
        {
            var quota = new AccountQuotaInfo
            {
                PagesConvertedTotal = 428,
                PagesConvertedPeriod = 428,
                PageQuotaLimit = 5000,
                SubscriptionStatus = "active"
            };

            _viewModel.UpdateQuotaState(quota);

            _viewModel.PagesConvertedDisplay.Should().Be("428 pages");
            _viewModel.PageAllowanceDisplay.Should().Be("428 / 5,000 pages (Fair Use)");
            _viewModel.PageUsagePercent.Should().BeApproximately(8.56, 0.01);
            _viewModel.PageProgressBrushHex.Should().Be("#38BDF8"); // Normal
        }

        [Fact]
        public void PageProgressBrushHex_ReflectsThresholds()
        {
            // Warning at >= 80%
            var warningQuota = new AccountQuotaInfo
            {
                PagesConvertedPeriod = 4200,
                PageQuotaLimit = 5000,
                SubscriptionStatus = "active"
            };
            _viewModel.UpdateQuotaState(warningQuota);
            _viewModel.PageProgressBrushHex.Should().Be("#F59E0B");

            // Exceeded at >= 100%
            var exceededQuota = new AccountQuotaInfo
            {
                PagesConvertedPeriod = 5000,
                PageQuotaLimit = 5000,
                SubscriptionStatus = "active"
            };
            _viewModel.UpdateQuotaState(exceededQuota);
            _viewModel.PageProgressBrushHex.Should().Be("#EF4444");

            // Expired
            var expiredQuota = new AccountQuotaInfo
            {
                PagesConvertedPeriod = 100,
                PageQuotaLimit = 5000,
                SubscriptionStatus = "expired"
            };
            _viewModel.UpdateQuotaState(expiredQuota);
            _viewModel.PageProgressBrushHex.Should().Be("#EF4444");
        }

        [Fact]
        public async Task RefreshSubscriptionCommand_WithBillingService_UpdatesQuota()
        {
            var mockBilling = new Mock<IPostXmlBillingService>();
            var freshQuota = new AccountQuotaInfo
            {
                PlanCode = "li_postxml_12m_super",
                PlanName = "Yearly Super Saver",
                PagesConvertedTotal = 500,
                SubscriptionStatus = "active"
            };
            mockBilling.Setup(b => b.RefreshQuotaAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(freshQuota);

            var vm = new ProfileViewModel(
                _mockRepo.Object,
                _mockLogger.Object,
                _mockDispatcher.Object,
                _mockProfileService.Object,
                _mockSessionService.Object,
                _mockNavService.Object,
                _mockTerminationService.Object,
                _mockGstinService.Object,
                mockBilling.Object
            );

            await vm.RefreshSubscriptionCommand.ExecuteAsync(null);

            vm.QuotaInfo.Should().BeSameAs(freshQuota);
            vm.PlanDisplayName.Should().Be("Yearly Super Saver");
            vm.PagesConvertedDisplay.Should().Be("500 pages");
            vm.SubscriptionFeedbackMessage.Should().Be("Plan details up to date.");
        }

        [Fact]
        public void CanUpgrade_And_UpgradeButtonText_ReflectsSubscriptionState()
        {
            // Free Trial
            var trialQuota = new AccountQuotaInfo { SubscriptionStatus = "trialing", DaysRemaining = 12 };
            _viewModel.UpdateQuotaState(trialQuota);
            _viewModel.CanUpgrade.Should().BeTrue();
            _viewModel.UpgradeButtonText.Should().Be("Upgrade Plan");

            // Active, > 30 days remaining (not eligible for renewal yet)
            var activeQuota = new AccountQuotaInfo { SubscriptionStatus = "active", DaysRemaining = 120 };
            _viewModel.UpdateQuotaState(activeQuota);
            _viewModel.CanUpgrade.Should().BeFalse();

            // Active, <= 30 days remaining (eligible for renewal)
            var renewingQuota = new AccountQuotaInfo { SubscriptionStatus = "active", DaysRemaining = 25 };
            _viewModel.UpdateQuotaState(renewingQuota);
            _viewModel.CanUpgrade.Should().BeTrue();
            _viewModel.UpgradeButtonText.Should().Be("Renew Plan");

            // Expired
            var expiredQuota = new AccountQuotaInfo { SubscriptionStatus = "expired", DaysRemaining = 0 };
            _viewModel.UpdateQuotaState(expiredQuota);
            _viewModel.CanUpgrade.Should().BeTrue();
            _viewModel.UpgradeButtonText.Should().Be("Renew Plan");
        }

        [Fact]
        public void QuotaInfo_WhenNull_PropertiesReturnDefaultsWithoutException()
        {
            _viewModel.UpdateQuotaState(null!);

            _viewModel.PagesConvertedDisplay.Should().Be("0 pages");
            _viewModel.PageAllowanceDisplay.Should().Be("0 / 100 pages (Fair Use)");
            _viewModel.PageUsagePercent.Should().Be(0.0);
            _viewModel.PageProgressBrushHex.Should().Be("#38BDF8");
            _viewModel.CanUpgrade.Should().BeTrue();
            _viewModel.UpgradeButtonText.Should().Be("Upgrade Plan");
        }

        [Fact]
        public void PageProgressBrushHex_WhenConversionNotAllowed_ReturnsDangerRed()
        {
            var quota = new AccountQuotaInfo
            {
                IsConversionAllowed = false,
                PagesConvertedPeriod = 10,
                PageQuotaLimit = 5000,
                SubscriptionStatus = "active"
            };

            _viewModel.UpdateQuotaState(quota);

            _viewModel.PageProgressBrushHex.Should().Be("#EF4444");
        }

        [Fact]
        public async Task RefreshSubscriptionCommand_WhenBillingServiceThrowsException_SetsErrorFeedback()
        {
            var mockBilling = new Mock<IPostXmlBillingService>();
            mockBilling.Setup(b => b.RefreshQuotaAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("API unreachable"));

            var vm = new ProfileViewModel(
                _mockRepo.Object,
                _mockLogger.Object,
                _mockDispatcher.Object,
                _mockProfileService.Object,
                _mockSessionService.Object,
                _mockNavService.Object,
                _mockTerminationService.Object,
                _mockGstinService.Object,
                mockBilling.Object
            );

            await vm.RefreshSubscriptionCommand.ExecuteAsync(null);

            vm.SubscriptionFeedbackMessage.Should().Be("Could not refresh subscription status.");
            vm.IsSubscriptionBusy.Should().BeFalse();
        }

        [Fact]
        public async Task RefreshSubscriptionCommand_WhenBillingServiceReturnsNull_SetsUpToDateFeedback()
        {
            var mockBilling = new Mock<IPostXmlBillingService>();
            mockBilling.Setup(b => b.RefreshQuotaAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync((AccountQuotaInfo?)null);

            var vm = new ProfileViewModel(
                _mockRepo.Object,
                _mockLogger.Object,
                _mockDispatcher.Object,
                _mockProfileService.Object,
                _mockSessionService.Object,
                _mockNavService.Object,
                _mockTerminationService.Object,
                _mockGstinService.Object,
                mockBilling.Object
            );

            await vm.RefreshSubscriptionCommand.ExecuteAsync(null);

            vm.SubscriptionFeedbackMessage.Should().Be("Plan details up to date.");
        }

        [Fact]
        public void Initialize_WhenCustomerMobilesAndGstinAreNull_DoesNotThrow()
        {
            var customer = new Customer
            {
                CustomerId = 101,
                Gstin = null!,
                Mobiles = null!,
                GstinDetails = null
            };

            Action act = () => _viewModel.Initialize(customer);

            act.Should().NotThrow();
            _viewModel.RegisteredMobiles.Should().BeEmpty();
            _viewModel.HasRegisteredMobiles.Should().BeFalse();
        }

        #endregion
    }
}
