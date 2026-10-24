using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Postaze.Configurations;
using Postaze.Repositories;
using Postaze.Services;
using PostXML.Services;
using PostXML.ViewModels;
using Xunit;

namespace PostXMLTest.Services
{
    public class NavigationServiceTest
    {
        private static IServiceProvider BuildMockServiceProvider(Mock<INavigationService>? navMock = null)
        {
            var services = new ServiceCollection();
            var loggerFactory = LoggerFactory.Create(builder => { });

            services.AddSingleton(loggerFactory.CreateLogger<NavigationService>());
            services.AddSingleton(loggerFactory.CreateLogger<LoginViewModel>());
            services.AddSingleton(loggerFactory.CreateLogger<RegisterViewModel>());
            services.AddSingleton(loggerFactory.CreateLogger<DashboardViewModel>());

            var repoMock = new Mock<ICustomerRepository>();
            var profileMock = new Mock<IProfileService>();
            var sessionMock = new Mock<ICustomerSessionService>();
            var features = new PostazeSoftwareFeatures
            {
                ApplicationName = "PostXML",
                ApplicationTagLine = "Tagline"
            };
            var featuresMock = new Mock<IOptions<PostazeSoftwareFeatures>>();
            featuresMock.Setup(f => f.Value).Returns(features);

            var configMock = new Mock<IOptions<CustomerDefaultConfig>>();
            configMock.Setup(c => c.Value).Returns(new CustomerDefaultConfig());

            services.AddSingleton(repoMock.Object);
            services.AddSingleton(profileMock.Object);
            services.AddSingleton(sessionMock.Object);
            services.AddSingleton(featuresMock.Object);
            services.AddSingleton(configMock.Object);

            if (navMock != null)
            {
                services.AddSingleton<INavigationService>(navMock.Object);
            }
            else
            {
                services.AddSingleton<INavigationService, NavigationService>();
            }

            services.AddTransient<LoginViewModel>();
            services.AddTransient<RegisterViewModel>();
            services.AddTransient<DashboardViewModel>();

            return services.BuildServiceProvider();
        }

        [Fact]
        public void SetNavigationHost_WithNull_ThrowsArgumentNullException()
        {
            // Arrange
            var sp = BuildMockServiceProvider();
            var logger = sp.GetRequiredService<ILogger<NavigationService>>();
            var service = new NavigationService(sp, logger);

            // Act & Assert
            var act = () => service.SetNavigationHost(null!);
            act.Should().Throw<ArgumentNullException>()
                .WithMessage("*Navigation host cannot be null*");
        }

        [Fact]
        public void NavigateTo_UnregisteredViewModel_ThrowsInvalidOperationException()
        {
            // Arrange
            var sp = BuildMockServiceProvider();
            var logger = sp.GetRequiredService<ILogger<NavigationService>>();
            var service = new NavigationService(sp, logger);

            // Act & Assert
            var act = () => service.NavigateTo<string>();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*No view registered*");
        }

        [Fact]
        public void NavigateTo_WithoutHost_ResolvesViewModelAndDoesNotThrow()
        {
            // Arrange
            var sp = BuildMockServiceProvider();
            var logger = sp.GetRequiredService<ILogger<NavigationService>>();
            var service = new NavigationService(sp, logger);

            // Act & Assert: Host is null, so it logs warning and completes gracefully
            var act = () => service.NavigateTo<LoginViewModel>();
            act.Should().NotThrow();
        }

        [Fact]
        public void NavigateTo_WithInitializationAction_InvokesActionOnResolvedViewModel()
        {
            // Arrange
            var sp = BuildMockServiceProvider();
            var service = sp.GetRequiredService<INavigationService>();

            bool actionInvoked = false;

            // Act
            service.NavigateTo<LoginViewModel>(vm =>
            {
                actionInvoked = true;
                vm.Should().NotBeNull();
            });

            // Assert
            actionInvoked.Should().BeTrue();
        }

        [Fact]
        public void NavigateTo_WhenInitializationActionThrows_RethrowsException()
        {
            // Arrange
            var sp = BuildMockServiceProvider();
            var service = sp.GetRequiredService<INavigationService>();

            // Act & Assert
            var act = () => service.NavigateTo<LoginViewModel>(vm =>
            {
                throw new InvalidOperationException("Initialization exploded");
            });

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Initialization exploded");
        }
    }
}
