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
            xml.Should().Contain("<REPORTNAME>All Masters</REPORTNAME>");
            xml.Should().Contain("<SVOverwriteImpVch>Yes</SVOverwriteImpVch>");
            xml.Should().Contain("<SVCURRENTCOMPANY>Acme Enterprises &amp; Co</SVCURRENTCOMPANY>");
            xml.Should().Contain("<BANKALLOCATIONS.LIST>");
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
        public void SanitizeXmlString_StripsInvalidControlCharacters_PreservesValidCharacters()
        {
            // \u0004 is EOT (caused previous ArgumentException), \u0000 is NUL, \u001F is Unit Separator
            string dirty = "PAYMENT\u0004TO\u0000VENDOR\u001B\tWITH\nNEWLINE\rAND DETAILS";
            string sanitized = TallyXmlExportService.SanitizeXmlString(dirty);

            sanitized.Should().Be("PAYMENTTOVENDOR\tWITH\nNEWLINE\rAND DETAILS");
            sanitized.Should().NotContain("\u0004");
            sanitized.Should().NotContain("\u0000");
            sanitized.Should().NotContain("\u001B");
        }

        [Fact]
        public void GenerateTallyXml_WithControlCharactersInNarrationAndReference_SerializesSuccessfullyWithoutThrowing()
        {
            // Arrange: statement containing 0x04 (ASCII EOT) character commonly found in raw PDF font streams
            var statement = new StatementDocument
            {
                BankName = "Canara\u0004Bank",
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 8, 4),
                        DepositAmount = 150000.00m,
                        WithdrawalAmount = 0m,
                        TargetLedgerName = "Suspense\u0000A/c",
                        RawNarration = "UPI/CR/12345/Vendor\u0004Payment/Ref\u001F456",
                        ReferenceNumber = "REF\u0004999",
                        Type = TransactionType.Deposit
                    }
                }
            };

            // Act: previously threw ArgumentException: ' ', hexadecimal value 0x04, is an invalid character
            var act = () => _service.GenerateTallyXml(statement, "Canara Bank\u0004Account", "Test Company");

            // Assert
            act.Should().NotThrow();
            string xml = act();
            xml.Should().NotContain("\u0004");
            xml.Should().NotContain("\u0000");
            xml.Should().NotContain("\u001F");
            xml.Should().Contain("UPI/CR/12345/VendorPayment/Ref456");
            xml.Should().Contain("REF999");
            xml.Should().Contain("<VOUCHER VCHTYPE=\"Receipt\"");
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
