using System;
using System.Threading.Tasks;
using FluentAssertions;
using PostXML.Services;
using Xunit;

namespace PostXMLTest.Services
{
    public class WinDispatcherServiceTest
    {
        [Fact]
        public async Task InvokeAsync_ExecutesAction()
        {
            var service = new WinDispatcherService();
            bool executed = false;

            await service.InvokeAsync(() =>
            {
                executed = true;
                return Task.CompletedTask;
            });

            executed.Should().BeTrue();
        }
    }
}
