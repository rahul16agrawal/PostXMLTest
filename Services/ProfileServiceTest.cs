using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PostXML.Models;
using PostXML.Services;
using Xunit;

namespace PostXMLTest.Services
{
    public class ProfileServiceTest : IDisposable
    {
        private readonly string _tempFolder;
        private readonly ProfileService _service;

        public ProfileServiceTest()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "PostXMLTest_Profile_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            var loggerMock = new Mock<ILogger<ProfileService>>();
            _service = new ProfileService(loggerMock.Object, _tempFolder);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempFolder))
                {
                    Directory.Delete(_tempFolder, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in temp folder
            }
        }

        [Fact]
        public async Task SaveProfileAsync_And_GetProfileAsync_PersistsAndLoadsCorrectly()
        {
            // Arrange
            var profile = new UserAppStateProfile
            {
                PostazeId = "MANU100021",
                EmailId = "ca@example.com",
                AuthToken = "token_abc123"
            };

            // Act
            await _service.SaveProfileAsync(profile);
            var loaded = await _service.GetProfileAsync("MANU100021");

            // Assert
            loaded.Should().NotBeNull();
            loaded!.PostazeId.Should().Be("MANU100021");
            loaded.EmailId.Should().Be("ca@example.com");
            loaded.AuthToken.Should().Be("token_abc123");
        }

        [Fact]
        public async Task GetLastLoggedUserId_ReturnsMostRecentProfile()
        {
            // Arrange
            var profile1 = new UserAppStateProfile { PostazeId = "USER001", AuthToken = "token1" };
            var profile2 = new UserAppStateProfile { PostazeId = "USER002", AuthToken = "token2" };

            await _service.SaveProfileAsync(profile1);
            await Task.Delay(50); // Ensure timestamp difference
            await _service.SaveProfileAsync(profile2);

            // Act
            var lastUser = _service.GetLastLoggedUserId();

            // Assert
            lastUser.Should().BeEquivalentTo("USER002");
        }

        [Fact]
        public async Task SaveProfileAsync_WithEmptyPostazeId_ThrowsArgumentException()
        {
            var profile = new UserAppStateProfile { PostazeId = "" };

            var act = async () => await _service.SaveProfileAsync(profile);
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task GetProfileAsync_WithEmptyId_ReturnsNull()
        {
            var result = await _service.GetProfileAsync("");
            result.Should().BeNull();
        }
    }
}
