using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using PostXML.Models;
using PostXML.Services;
using Xunit;

namespace PostXMLTest.Services
{
    public class PostXmlBillingServiceTest
    {
        private readonly Mock<IHttpClientFactory> _mockFactory;
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly PostXmlBillingService _billingService;

        public PostXmlBillingServiceTest()
        {
            _mockFactory = new Mock<IHttpClientFactory>();
            _mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _httpClient = new HttpClient(_mockHandler.Object)
            {
                BaseAddress = new Uri("https://postaze.com/api/")
            };

            _mockFactory.Setup(f => f.CreateClient("PostazeApi")).Returns(_httpClient);
            _billingService = new PostXmlBillingService(_mockFactory.Object, NullLogger<PostXmlBillingService>.Instance);
        }

        [Fact]
        public async Task RefreshQuotaAsync_WhenApiReturns200WithDataWrapper_UpdatesQuotaAndReturns()
        {
            // Arrange
            string jsonResponse = """
            {
                "status": "success",
                "data": {
                    "plan_code": "li_postxml_12m_super",
                    "plan_name": "Yearly Super Saver",
                    "subscription_status": "active",
                    "pages_converted_total": 428,
                    "pages_converted_period": 428,
                    "page_quota_limit": 5000,
                    "is_conversion_allowed": true,
                    "days_remaining": 365
                }
            }
            """;

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString().Contains("v1/postxml/quota-status")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var result = await _billingService.RefreshQuotaAsync();

            // Assert
            result.Should().NotBeNull();
            result!.PlanCode.Should().Be("li_postxml_12m_super");
            result.PagesConvertedTotal.Should().Be(428);
            result.PageQuotaLimit.Should().Be(5000);
            _billingService.CurrentQuota.Should().BeSameAs(result);
        }

        [Fact]
        public async Task RefreshQuotaAsync_WhenApiFails_ReturnsCachedQuota()
        {
            // Arrange
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            // Act
            var result = await _billingService.RefreshQuotaAsync();

            // Assert
            result.Should().NotBeNull();
            result!.PlanCode.Should().Be("free_trial"); // default cached
        }

        [Fact]
        public async Task CreateCheckoutSessionAsync_WhenApiSucceeds_ReturnsPortalUrl()
        {
            // Arrange
            string expectedUrl = "https://postaze.com/postxml/upgrade?customer=100021&signature=abc";
            string jsonResponse = $$"""
            {
                "status": "success",
                "data": {
                    "checkout_url": "{{expectedUrl}}"
                }
            }
            """;

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains("v1/postxml/checkout-session")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse)
                });

            // Act
            var url = await _billingService.CreateCheckoutSessionAsync();

            // Assert
            url.Should().Be(expectedUrl);
        }

        [Fact]
        public async Task CreateCheckoutSessionAsync_WhenApiFails_ReturnsFallbackPortalUrl()
        {
            // Arrange
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway));

            // Act
            var url = await _billingService.CreateCheckoutSessionAsync();

            // Assert
            url.Should().Be("https://postaze.com/postxml/upgrade");
        }

        [Fact]
        public async Task ReportStatementConvertedAsync_WhenApiSucceeds_UpdatesCurrentQuota()
        {
            // Arrange
            var telemetry = new StatementConversionTelemetry
            {
                BankCode = "HDFC",
                AccountNumberMasked = "5020XXXX2327",
                PageCount = 14,
                TransactionCount = 162,
                ExportFormat = "XML",
                IsReconciled = true,
                FileHashSha256 = "dummyhash123"
            };

            string jsonResponse = """
            {
                "status": "success",
                "data": {
                    "pages_converted_total": 442,
                    "pages_converted_period": 442,
                    "page_quota_remaining": 4558
                }
            }
            """;

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains("v1/postxml/telemetry/statement-converted")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse)
                });

            // Act
            bool success = await _billingService.ReportStatementConvertedAsync(telemetry);

            // Assert
            success.Should().BeTrue();
            _billingService.CurrentQuota!.PagesConvertedTotal.Should().Be(442);
            _billingService.CurrentQuota!.PagesConvertedPeriod.Should().Be(442);
        }

        [Fact]
        public async Task ReportStatementConvertedAsync_WhenNullTelemetry_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _billingService.ReportStatementConvertedAsync(null!));
        }

        [Fact]
        public async Task ReportStatementConvertedAsync_WhenApiFails_ReturnsFalse()
        {
            var telemetry = new StatementConversionTelemetry { BankCode = "SBI", PageCount = 5 };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            bool success = await _billingService.ReportStatementConvertedAsync(telemetry);

            success.Should().BeFalse();
        }

        #region Edge Cases, Nulls & Exception Handling

        [Fact]
        public void Constructor_NullHttpClientFactory_ThrowsArgumentNullException()
        {
            Action act = () => new PostXmlBillingService(null!, NullLogger<PostXmlBillingService>.Instance);
            act.Should().Throw<ArgumentNullException>().WithParameterName("httpClientFactory");
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            Action act = () => new PostXmlBillingService(_mockFactory.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public async Task RefreshQuotaAsync_WhenNetworkThrowsHttpRequestException_ReturnsCachedQuotaGracefully()
        {
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("DNS resolution failed"));

            var result = await _billingService.RefreshQuotaAsync();

            result.Should().NotBeNull();
            result!.PlanCode.Should().Be("free_trial");
        }

        [Fact]
        public async Task RefreshQuotaAsync_WhenRequestTimesOut_ReturnsCachedQuotaGracefully()
        {
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timed out"));

            var result = await _billingService.RefreshQuotaAsync();

            result.Should().NotBeNull();
            result!.PlanCode.Should().Be("free_trial");
        }

        [Fact]
        public async Task RefreshQuotaAsync_WhenResponseContainsHtmlErrorPage_ReturnsCachedQuotaWithoutThrowing()
        {
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway)
                {
                    Content = new StringContent("<html><body>502 Bad Gateway</body></html>")
                });

            var result = await _billingService.RefreshQuotaAsync();

            result.Should().NotBeNull();
            result!.PlanCode.Should().Be("free_trial");
        }

        [Fact]
        public async Task RefreshQuotaAsync_WhenRootObjectDirectlyContainsQuotaWithoutDataWrapper_ParsesCorrectly()
        {
            string directJson = """
            {
                "plan_code": "li_postxml_6m_value",
                "plan_name": "Half Year Value",
                "status": "active",
                "pages_converted_total": 210,
                "pages_converted_period": 210,
                "page_quota_limit": 2500
            }
            """;

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(directJson)
                });

            var result = await _billingService.RefreshQuotaAsync();

            result.Should().NotBeNull();
            result!.PlanCode.Should().Be("li_postxml_6m_value");
            result.DisplayStatus.Should().Be("ACTIVE");
            result.PagesConvertedTotal.Should().Be(210);
            result.PageQuotaLimit.Should().Be(2500);
        }

        [Fact]
        public async Task CreateCheckoutSessionAsync_WhenDirectCheckoutUrlAtRoot_ReturnsUrl()
        {
            string json = """
            {
                "checkout_url": "https://postaze.com/direct/checkout"
            }
            """;

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });

            var url = await _billingService.CreateCheckoutSessionAsync();

            url.Should().Be("https://postaze.com/direct/checkout");
        }

        [Fact]
        public async Task CreateCheckoutSessionAsync_WhenNetworkThrowsException_ReturnsFallbackUrl()
        {
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            var url = await _billingService.CreateCheckoutSessionAsync();

            url.Should().Be("https://postaze.com/postxml/upgrade");
        }

        [Fact]
        public async Task ReportStatementConvertedAsync_WhenNetworkThrowsException_ReturnsFalseGracefully()
        {
            var telemetry = new StatementConversionTelemetry { BankCode = "AXIS", PageCount = 2 };

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Server disconnected"));

            bool success = await _billingService.ReportStatementConvertedAsync(telemetry);

            success.Should().BeFalse();
        }

        [Fact]
        public async Task ReportStatementConvertedAsync_WhenResponseDataHasNonIntegerValues_DoesNotCrash()
        {
            var telemetry = new StatementConversionTelemetry { BankCode = "KOTAK", PageCount = 1 };

            string json = """
            {
                "status": "success",
                "data": {
                    "pages_converted_total": "invalid_string",
                    "pages_converted_period": null
                }
            }
            """;

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });

            bool success = await _billingService.ReportStatementConvertedAsync(telemetry);

            // Should succeed without throwing unhandled deserialization or type cast exception
            success.Should().BeTrue();
        }

        #endregion
    }
}
