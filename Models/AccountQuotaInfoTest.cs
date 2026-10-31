using System;
using FluentAssertions;
using PostXML.Models;
using Xunit;

namespace PostXMLTest.Models
{
    public class AccountQuotaInfoTest
    {
        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var quota = new AccountQuotaInfo();

            quota.PlanCode.Should().Be("free_trial");
            quota.DisplayPlanName.Should().Be("Free Trial");
            quota.DisplayStatus.Should().Be("FREE TRIAL");
            quota.StatusColorHex.Should().Be("#38BDF8");
            quota.IsUnlimited.Should().BeTrue();
            quota.CanUpload.Should().BeTrue();
            quota.CanRenew.Should().BeTrue();
        }

        [Fact]
        public void ExpiredStatus_ComputesCorrectly()
        {
            var quota = new AccountQuotaInfo
            {
                SubscriptionStatus = "expired",
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            };

            quota.IsExpired.Should().BeTrue();
            quota.DisplayStatus.Should().Be("EXPIRED");
            quota.StatusColorHex.Should().Be("#EF4444");
            quota.IsQuotaExceeded.Should().BeTrue();
            quota.CanUpload.Should().BeFalse();
        }

        [Fact]
        public void ExpiringSoon_ComputesCorrectly()
        {
            var quota = new AccountQuotaInfo
            {
                SubscriptionStatus = "active",
                DaysRemaining = 3
            };

            quota.IsExpiringSoon.Should().BeTrue();
            quota.StatusColorHex.Should().Be("#F59E0B");
        }

        [Fact]
        public void UsagePercentage_CalculatesProperly()
        {
            var quota = new AccountQuotaInfo
            {
                StorageQuotaBytes = 1000,
                StorageUsedBytes = 500
            };

            quota.UsagePercentage.Should().Be(50.0);
            quota.IsApproachingQuota.Should().BeFalse();

            quota.StorageUsedBytes = 950;
            quota.IsApproachingQuota.Should().BeTrue();
        }

        [Fact]
        public void FormatBytes_FormatsCorrectly()
        {
            AccountQuotaInfo.FormatBytes(0).Should().Be("0 B");
            AccountQuotaInfo.FormatBytes(1024).Should().Be("1 KB");
            AccountQuotaInfo.FormatBytes(1048576).Should().Be("1 MB");
            AccountQuotaInfo.FormatBytes(1073741824).Should().Be("1 GB");
        }

        [Fact]
        public void PlanDisplayName_MapsStandardCodes()
        {
            var q1 = new AccountQuotaInfo { PlanCode = "yearly_saver" };
            q1.DisplayPlanName.Should().Be("Yearly Super Saver (12 months)");

            var q2 = new AccountQuotaInfo { PlanCode = "half_year" };
            q2.DisplayPlanName.Should().Be("Half Year Value (6 months)");

            var q3 = new AccountQuotaInfo { PlanCode = "quarterly" };
            q3.DisplayPlanName.Should().Be("Quarterly Saver (3 months)");
        }

        [Fact]
        public void AvailablePlanInfo_HoldsProperties()
        {
            var plan = new AvailablePlanInfo
            {
                Code = "pro",
                Name = "Professional",
                PriceMonthlyInr = 499,
                StorageQuotaDisplay = "50 GB"
            };

            plan.Code.Should().Be("pro");
            plan.Name.Should().Be("Professional");
            plan.PriceMonthlyInr.Should().Be(499);
            plan.StorageQuotaDisplay.Should().Be("50 GB");
        }

        #region PostXML Page Conversion Quota Tests

        [Fact]
        public void PageConversionDefaults_AreCorrect()
        {
            var quota = new AccountQuotaInfo();

            quota.PagesConvertedTotal.Should().Be(0);
            quota.PagesConvertedPeriod.Should().Be(0);
            quota.PageQuotaLimit.Should().Be(100);
            quota.IsConversionAllowed.Should().BeTrue();
            quota.PageUsagePercentage.Should().Be(0.0);
            quota.PageUsageDisplay.Should().Be("0 / 100 pages");
            quota.PagesConvertedDisplay.Should().Be("0 pages");
            quota.PageAllowanceDisplay.Should().Be("0 / 100 pages (Fair Use)");
            quota.CanConvert.Should().BeTrue();
        }

        [Fact]
        public void PageUsagePercentage_CalculatesAndCapsAt100()
        {
            var quota = new AccountQuotaInfo
            {
                PageQuotaLimit = 5000,
                PagesConvertedPeriod = 2500
            };

            quota.PageUsagePercentage.Should().Be(50.0);

            // Exceeded limit capped at 100%
            quota.PagesConvertedPeriod = 6000;
            quota.PageUsagePercentage.Should().Be(100.0);

            // Zero limit handles division gracefully
            quota.PageQuotaLimit = 0;
            quota.PageUsagePercentage.Should().Be(0.0);
        }

        [Fact]
        public void CanConvert_ReflectsQuotaLimitAndExpiration()
        {
            var quota = new AccountQuotaInfo
            {
                PageQuotaLimit = 100,
                PagesConvertedPeriod = 99,
                IsConversionAllowed = true,
                SubscriptionStatus = "active"
            };

            quota.CanConvert.Should().BeTrue();

            // When period reaches or exceeds limit
            quota.PagesConvertedPeriod = 100;
            quota.CanConvert.Should().BeFalse();

            quota.PagesConvertedPeriod = 105;
            quota.CanConvert.Should().BeFalse();

            // When explicitly disallowed
            quota.PagesConvertedPeriod = 50;
            quota.IsConversionAllowed = false;
            quota.CanConvert.Should().BeFalse();

            // When subscription expired
            quota.IsConversionAllowed = true;
            quota.SubscriptionStatus = "expired";
            quota.CanConvert.Should().BeFalse();
        }

        [Fact]
        public void Deserialization_FromJson_PopulatesPageConversionFields()
        {
            string json = """
            {
                "plan_code": "li_postxml_12m_super",
                "plan_name": "Yearly Super Saver",
                "subscription_status": "active",
                "pages_converted_total": 428,
                "pages_converted_period": 428,
                "page_quota_limit": 5000,
                "is_conversion_allowed": true,
                "days_remaining": 365
            }
            """;

            var quota = System.Text.Json.JsonSerializer.Deserialize<AccountQuotaInfo>(json);

            quota.Should().NotBeNull();
            quota!.PlanCode.Should().Be("li_postxml_12m_super");
            quota.PlanName.Should().Be("Yearly Super Saver");
            quota.PagesConvertedTotal.Should().Be(428);
            quota.PagesConvertedPeriod.Should().Be(428);
            quota.PageQuotaLimit.Should().Be(5000);
            quota.IsConversionAllowed.Should().BeTrue();
            quota.PagesConvertedDisplay.Should().Be("428 pages");
            quota.PageAllowanceDisplay.Should().Be("428 / 5,000 pages (Fair Use)");
            quota.PageUsagePercentage.Should().BeApproximately(8.56, 0.01);
            quota.CanConvert.Should().BeTrue();
        }

        #endregion
    }
}
