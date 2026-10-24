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
    }
}
