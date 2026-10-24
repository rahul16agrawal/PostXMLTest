using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PostXML.Models;
using PostXML.Services;
using Xunit;

namespace PostXMLTest.Services
{
    public class TallyXmlExportServiceTest
    {
        private readonly Mock<ILogger<TallyXmlExportService>> _loggerMock;
        private readonly TallyXmlExportService _service;

        public TallyXmlExportServiceTest()
        {
            _loggerMock = new Mock<ILogger<TallyXmlExportService>>();
            _service = new TallyXmlExportService(_loggerMock.Object);
        }

        [Fact]
        public void GenerateTallyXml_CreatesStandardVouchers()
        {
            // Arrange
            var statement = new StatementDocument
            {
                BankName = "HDFC Bank",
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 10, 24),
                        DepositAmount = 0m,
                        WithdrawalAmount = 450.00m,
                        TargetLedgerName = "Staff Welfare",
                        RawNarration = "UPI/123/Zomato",
                        Type = TransactionType.Withdrawal
                    },
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 10, 24),
                        DepositAmount = 50000.00m,
                        WithdrawalAmount = 0m,
                        TargetLedgerName = "Sales Account",
                        RawNarration = "NEFT/456/Client Payment",
                        Type = TransactionType.Deposit
                    },
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 10, 24),
                        DepositAmount = 10000.00m,
                        WithdrawalAmount = 0m,
                        TargetLedgerName = "Cash A/c",
                        RawNarration = "ATM Cash Deposit",
                        Type = TransactionType.Contra
                    }
                }
            };

            // Act
            string xml = _service.GenerateTallyXml(statement, "HDFC Bank A/c", "Acme Enterprises & Co");

            // Assert
            xml.Should().Contain("<ENVELOPE>");
            xml.Should().Contain("<TALLYREQUEST>Import Data</TALLYREQUEST>");
            xml.Should().Contain("<SVCURRENTCOMPANY>Acme Enterprises &amp; Co</SVCURRENTCOMPANY>");
            xml.Should().Contain("<VOUCHER VCHTYPE=\"Payment\"");
            xml.Should().Contain("<VOUCHER VCHTYPE=\"Receipt\"");
            xml.Should().Contain("<VOUCHER VCHTYPE=\"Contra\"");
            xml.Should().Contain("<LEDGERNAME>HDFC Bank A/c</LEDGERNAME>");
            xml.Should().Contain("<LEDGERNAME>Staff Welfare</LEDGERNAME>");
            xml.Should().Contain("<LEDGERNAME>Sales Account</LEDGERNAME>");
            xml.Should().Contain("<LEDGERNAME>Cash A/c</LEDGERNAME>");
            xml.Should().Contain("<DATE>20261024</DATE>");
        }

        [Fact]
        public void GenerateTallyXml_WithNullStatement_ThrowsArgumentNullException()
        {
            var act = () => _service.GenerateTallyXml(null!, "HDFC Bank");
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GenerateTallyXml_WithBlankBankLedger_ThrowsArgumentException()
        {
            var statement = new StatementDocument();
            var act = () => _service.GenerateTallyXml(statement, "   ");
            act.Should().Throw<ArgumentException>();
        }
    }
}
