using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PostXML.ViewModels;
using PostXML.Views;
using Xunit;

namespace PostXMLTest.Views
{
    public class HomeViewUserControlTest
    {
        [WpfFact]
        public void ParameterlessConstructor_InitializesComponent()
        {
            var view = new HomeViewUserControl();
            view.Should().NotBeNull();
        }

        [WpfFact]
        public void ParameterizedConstructor_SetsDataContext()
        {
            var loggerMock = new Mock<ILogger<HomeViewModel>>();
            var vm = new HomeViewModel(loggerMock.Object);

            var view = new HomeViewUserControl(vm);

            view.Should().NotBeNull();
            view.DataContext.Should().Be(vm);
        }
    }
}
