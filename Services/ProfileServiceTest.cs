using System;
using System.Collections.Generic;
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
        public async Task SaveProfileAsync_PersistsAllUserAppStateProfileProperties()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var profile = new UserAppStateProfile
            {
                PostazeId = "CA9999",
                EmailId = "auditor@firm.com",
                AuthToken = "bearer_full_token_xyz",
                MachineFingerprint = "HW-GUID-8888",
                MachineName = "AUDIT-WORKSTATION",
                TokenExpiry = now.AddDays(30),
                LastLoginAt = now
            };

            // Act
            await _service.SaveProfileAsync(profile);
            var loaded = await _service.GetProfileAsync("CA9999");

            // Assert
            loaded.Should().NotBeNull();
            loaded!.PostazeId.Should().Be("CA9999");
            loaded.EmailId.Should().Be("auditor@firm.com");
            loaded.AuthToken.Should().Be("bearer_full_token_xyz");
            loaded.MachineFingerprint.Should().Be("HW-GUID-8888");
            loaded.MachineName.Should().Be("AUDIT-WORKSTATION");
            loaded.TokenExpiry.Should().BeCloseTo(now.AddDays(30), TimeSpan.FromSeconds(2));
            loaded.LastLoginAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task SaveProfileAsync_WhenTargetDirectoryDoesNotExist_CreatesDirectoryAndSavesProfile()
        {
            // Arrange
            string nestedPath = Path.Combine(_tempFolder, "NonExistentDir", "Profiles");
            if (Directory.Exists(nestedPath)) Directory.Delete(nestedPath, true);

            var loggerMock = new Mock<ILogger<ProfileService>>();
            var customService = new ProfileService(loggerMock.Object, nestedPath);

            var profile = new UserAppStateProfile
            {
                PostazeId = "DEEP01",
                EmailId = "deep@example.com",
                AuthToken = "token_deep"
            };

            // Act
            await customService.SaveProfileAsync(profile);
            var loaded = await customService.GetProfileAsync("DEEP01");

            // Assert
            Directory.Exists(nestedPath).Should().BeTrue();
            loaded.Should().NotBeNull();
            loaded!.PostazeId.Should().Be("DEEP01");
            loaded.AuthToken.Should().Be("token_deep");
        }

        [Fact]
        public async Task SaveProfileAsync_WhenProfileAlreadyExists_OverwritesWithUpdatedData()
        {
            // Arrange
            var profileV1 = new UserAppStateProfile
            {
                PostazeId = "MANU100021",
                EmailId = "ca@example.com",
                AuthToken = "initial_token_1"
            };
            await _service.SaveProfileAsync(profileV1);

            var profileV2 = new UserAppStateProfile
            {
                PostazeId = "MANU100021",
                EmailId = "new_ca@example.com",
                AuthToken = "updated_token_2"
            };

            // Act
            await _service.SaveProfileAsync(profileV2);
            var loaded = await _service.GetProfileAsync("MANU100021");

            // Assert
            loaded.Should().NotBeNull();
            loaded!.EmailId.Should().Be("new_ca@example.com");
            loaded.AuthToken.Should().Be("updated_token_2");
        }

        [Fact]
        public async Task GetProfileAsync_CaseInsensitivity_ReturnsSavedProfileRegardlessOfQueryCasing()
        {
            // Arrange
            var profile = new UserAppStateProfile
            {
                PostazeId = "Postaze_CA_01",
                EmailId = "case@example.com",
                AuthToken = "case_token_123"
            };
            await _service.SaveProfileAsync(profile);

            // Act
            var loadedLower = await _service.GetProfileAsync("postaze_ca_01");
            var loadedUpper = await _service.GetProfileAsync("POSTAZE_CA_01");
            var loadedMixed = await _service.GetProfileAsync("PoStAzE_cA_01");

            // Assert
            loadedLower.Should().NotBeNull();
            loadedUpper.Should().NotBeNull();
            loadedMixed.Should().NotBeNull();
            loadedLower!.EmailId.Should().Be("case@example.com");
            loadedUpper!.EmailId.Should().Be("case@example.com");
            loadedMixed!.EmailId.Should().Be("case@example.com");
        }

        [Fact]
        public async Task GetProfileAsync_WhenProfileDoesNotExist_ReturnsNull()
        {
            var result = await _service.GetProfileAsync("NON_EXISTENT_USER");
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetProfileAsync_WhenFileContainsCorruptedJson_ReturnsNullGracefullyWithoutThrowing()
        {
            // Arrange
            string filePath = Path.Combine(_tempFolder, "userprofile_corrupt.json");
            await File.WriteAllTextAsync(filePath, "{ this is not valid JSON !!!");

            // Act
            var profile = await _service.GetProfileAsync("corrupt");

            // Assert
            profile.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData(null)]
        public async Task GetProfileAsync_WithNullOrWhitespace_ReturnsNull(string? invalidId)
        {
            var result = await _service.GetProfileAsync(invalidId!);
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData(null)]
        public async Task SaveProfileAsync_WithInvalidPostazeId_ThrowsArgumentException(string? invalidId)
        {
            var profile = new UserAppStateProfile { PostazeId = invalidId! };
            var act = async () => await _service.SaveProfileAsync(profile);
            await act.Should().ThrowAsync<ArgumentException>();
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
        public async Task GetLastLoggedUserId_WhenMultipleProfilesExist_ReturnsMostRecentlyModifiedProfile()
        {
            // Arrange
            var p1 = new UserAppStateProfile { PostazeId = "USER_ALPHA", AuthToken = "token_a" };
            var p2 = new UserAppStateProfile { PostazeId = "USER_BETA", AuthToken = "token_b" };
            var p3 = new UserAppStateProfile { PostazeId = "USER_GAMMA", AuthToken = "token_c" };

            await _service.SaveProfileAsync(p1);
            await _service.SaveProfileAsync(p2);
            await _service.SaveProfileAsync(p3);

            string file1 = Path.Combine(_tempFolder, "userprofile_user_alpha.json");
            string file2 = Path.Combine(_tempFolder, "userprofile_user_beta.json");
            string file3 = Path.Combine(_tempFolder, "userprofile_user_gamma.json");

            // Explicitly set write times to verify ordering independent of file creation sequence
            File.SetLastWriteTimeUtc(file1, DateTime.UtcNow.AddMinutes(-30));
            File.SetLastWriteTimeUtc(file3, DateTime.UtcNow.AddMinutes(-15));
            File.SetLastWriteTimeUtc(file2, DateTime.UtcNow); // Most recent

            // Act
            var lastUser = _service.GetLastLoggedUserId();

            // Assert
            lastUser.Should().Be("user_beta");
        }

        [Fact]
        public void GetLastLoggedUserId_WhenDirectoryDoesNotExist_ReturnsNullWithoutException()
        {
            // Arrange
            string nonExistent = Path.Combine(_tempFolder, "GhostFolder_" + Guid.NewGuid().ToString("N"));
            var loggerMock = new Mock<ILogger<ProfileService>>();
            var service = new ProfileService(loggerMock.Object, nonExistent);

            // Act
            var result = service.GetLastLoggedUserId();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetLastLoggedUserId_WhenDirectoryIsEmpty_ReturnsNull()
        {
            // Arrange
            string emptyDir = Path.Combine(_tempFolder, "EmptyFolder_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(emptyDir);
            var loggerMock = new Mock<ILogger<ProfileService>>();
            var service = new ProfileService(loggerMock.Object, emptyDir);

            // Act
            var result = service.GetLastLoggedUserId();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetLastLoggedUserId_WhenDirectoryContainsNonProfileFiles_IgnoresStrayFilesAndReturnsNull()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_tempFolder, "readme.txt"), "some notes");
            File.WriteAllText(Path.Combine(_tempFolder, "appsettings.json"), "{}");
            File.WriteAllText(Path.Combine(_tempFolder, "userprofile_.bak"), "backup");
            File.WriteAllText(Path.Combine(_tempFolder, "Log_20261031.txt"), "log data");

            // Act
            var result = _service.GetLastLoggedUserId();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ConcurrentAccess_MultipleParallelSavesAndReads_ExecutesSafelyWithoutConcurrencyExceptions()
        {
            // Arrange: Simulate heavy concurrent load from parallel UI/Background tasks
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var p = new UserAppStateProfile
                    {
                        PostazeId = $"CONCURRENT_{index % 5}",
                        EmailId = $"user{index}@test.com",
                        AuthToken = $"token_{index}"
                    };
                    await _service.SaveProfileAsync(p);
                    var loaded = await _service.GetProfileAsync($"CONCURRENT_{index % 5}");
                    loaded.Should().NotBeNull();
                }));
            }

            // Act & Assert: Should complete without throwing any IOException or concurrency violation
            await Task.WhenAll(tasks);
        }

        [Fact]
        public void Constructor_Default_ResolvesToCommonApplicationDataDirectory()
        {
            var loggerMock = new Mock<ILogger<ProfileService>>();
            var defaultService = new ProfileService(loggerMock.Object);
            var folderField = typeof(ProfileService).GetField("_folderPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            string folderPath = (string)folderField!.GetValue(defaultService)!;

            string expectedCommonData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Postaze", "PostXML");

            folderPath.Should().Be(expectedCommonData, "ProfileService must store state in machine-wide CommonApplicationData.");
        }
    }
}
