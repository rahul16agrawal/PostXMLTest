using FluentAssertions;
using Postaze.ViewModels;
using PostXML.Views;
using Xunit;

namespace PostXMLTest.Views
{
    public class MainViewWindowTest
    {
        [WpfFact]
        public void Constructor_SetsDataContextAndInitializes()
        {
            // Arrange
            var vm = new MainViewModel();

            // Act
            var window = new MainViewWindow(vm);

            // Assert
            window.Should().NotBeNull();
            window.DataContext.Should().Be(vm);
            window.Title.Should().Be("PostXML");
        }
    }
}
