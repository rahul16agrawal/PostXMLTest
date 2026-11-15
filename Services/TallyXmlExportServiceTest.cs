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

        [Fact]
        public void GenerateTallyXml_BankLedger_OmitsOpeningBalanceNode()
        {
            var statement = new StatementDocument
            {
                BankName = "State Bank of India",
                OpeningBalance = 45514.24m,
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 4, 1),
                        DepositAmount = 1000m,
                        TargetLedgerName = "Sales Account",
                        Type = TransactionType.Deposit
                    }
                }
            };

            string xml = _service.GenerateTallyXml(statement, "SBI");

            var xdoc = System.Xml.Linq.XDocument.Parse(xml);
            var bankLedger = xdoc.Descendants("LEDGER").FirstOrDefault(l => (string?)l.Attribute("NAME") == "SBI");
            bankLedger.Should().NotBeNull();
            bankLedger!.Element("OPENINGBALANCE").Should().BeNull();
        }

        [Fact]
        public void GenerateTallyXml_WithCustomBankLedgerName_UsesCustomNameAcrossMasterAndVouchers()
        {
            var statement = new StatementDocument
            {
                BankName = "Canara Bank",
                OpeningBalance = -3054604.43m,
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 8, 1),
                        WithdrawalAmount = 500m,
                        TargetLedgerName = "Office Expenses",
                        Type = TransactionType.Withdrawal
                    },
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 8, 2),
                        DepositAmount = 1500m,
                        TargetLedgerName = "Client Inflow",
                        Type = TransactionType.Deposit
                    }
                }
            };

            string customLedgerName = "Canara Bank OD A/c";
            string xml = _service.GenerateTallyXml(statement, customLedgerName);

            var xdoc = System.Xml.Linq.XDocument.Parse(xml);
            
            // Verify Bank Ledger Master uses custom name and has no opening balance
            var bankLedger = xdoc.Descendants("LEDGER").FirstOrDefault(l => (string?)l.Attribute("NAME") == customLedgerName);
            bankLedger.Should().NotBeNull();
            bankLedger!.Element("PARENT")!.Value.Should().Be("Bank Accounts");
            bankLedger!.Element("OPENINGBALANCE").Should().BeNull();

            var vouchers = xdoc.Descendants("VOUCHER").ToList();
            
            // Payment voucher: Bank ledger is in entries, party is target ledger
            var pmtVoucher = vouchers.First(v => (string?)v.Element("VOUCHERTYPENAME") == "Payment");
            pmtVoucher.Element("PARTYLEDGERNAME")!.Value.Should().Be("Office Expenses");
            var pmtBankEntry = pmtVoucher.Descendants("ALLLEDGERENTRIES.LIST").FirstOrDefault(e => (string?)e.Element("LEDGERNAME") == customLedgerName);
            pmtBankEntry.Should().NotBeNull();

            // Receipt voucher: Party is the bank ledger
            var rcptVoucher = vouchers.First(v => (string?)v.Element("VOUCHERTYPENAME") == "Receipt");
            rcptVoucher.Element("PARTYLEDGERNAME")!.Value.Should().Be(customLedgerName);
            var rcptBankEntry = rcptVoucher.Descendants("ALLLEDGERENTRIES.LIST").FirstOrDefault(e => (string?)e.Element("LEDGERNAME") == customLedgerName);
            rcptBankEntry.Should().NotBeNull();
        }

        [Fact]
        public void GenerateTallyXml_LedgerMasters_IncludeActionNameAndLanguageNameList()
        {
            var statement = new StatementDocument
            {
                BankName = "HDFC Bank",
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 4, 2),
                        WithdrawalAmount = 130130m,
                        TargetLedgerName = "Suspense",
                        Type = TransactionType.Withdrawal
                    }
                }
            };

            string xml = _service.GenerateTallyXml(statement, "HDFC Bank A/c");
            var xdoc = System.Xml.Linq.XDocument.Parse(xml);

            var ledgers = xdoc.Descendants("LEDGER").ToList();
            ledgers.Should().HaveCount(2); // Target ledger + Bank ledger

            foreach (var ledger in ledgers)
            {
                string expectedName = (string)ledger.Attribute("NAME")!;
                ledger.Attribute("ACTION")!.Value.Should().Be("Create");
                ledger.Element("NAME")!.Value.Should().Be(expectedName);

                var langList = ledger.Element("LANGUAGENAME.LIST");
                langList.Should().NotBeNull();
                langList!.Element("LANGUAGEID")!.Value.Trim().Should().Be("1033");

                var nameList = langList.Element("NAME.LIST");
                nameList.Should().NotBeNull();
                nameList!.Attribute("TYPE")!.Value.Should().Be("String");
                nameList.Element("NAME")!.Value.Should().Be(expectedName);
            }
        }

        [Fact]
        public void GenerateTallyXml_LedgerMasters_XmlEscapesSpecialCharactersInNames()
        {
            var statement = new StatementDocument
            {
                BankName = "HDFC Bank",
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 9, 1),
                        DepositAmount = 1000m,
                        TargetLedgerName = "Sharma & Sons <Pvt> \"Ltd\"",
                        RawNarration = "Invoice & Settlement",
                        Type = TransactionType.Deposit
                    }
                }
            };

            string bankLedgerName = "HDFC & Standard Bank (Joint)";
            string xml = _service.GenerateTallyXml(statement, bankLedgerName);

            // Verify XML is well-formed and parses cleanly
            var act = () => System.Xml.Linq.XDocument.Parse(xml);
            act.Should().NotThrow();

            var xdoc = act();
            var ledgers = xdoc.Descendants("LEDGER").ToList();
            ledgers.Should().HaveCount(2);

            var partyLedger = ledgers.FirstOrDefault(l => (string?)l.Attribute("NAME") == "Sharma & Sons <Pvt> \"Ltd\"");
            partyLedger.Should().NotBeNull();
            partyLedger!.Element("NAME")!.Value.Should().Be("Sharma & Sons <Pvt> \"Ltd\"");
            partyLedger.Element("LANGUAGENAME.LIST")!.Element("NAME.LIST")!.Element("NAME")!.Value.Should().Be("Sharma & Sons <Pvt> \"Ltd\"");

            var bankLedger = ledgers.FirstOrDefault(l => (string?)l.Attribute("NAME") == bankLedgerName);
            bankLedger.Should().NotBeNull();
            bankLedger!.Element("NAME")!.Value.Should().Be(bankLedgerName);
        }

        [Fact]
        public void GenerateTallyXml_LedgerMasters_DeduplicatesMultipleTransactionsWithIdenticalLedger()
        {
            var statement = new StatementDocument
            {
                BankName = "ICICI Bank",
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction { TransactionDate = new DateTime(2026, 9, 1), WithdrawalAmount = 100m, TargetLedgerName = "Suspense", Type = TransactionType.Withdrawal },
                    new BankTransaction { TransactionDate = new DateTime(2026, 9, 2), WithdrawalAmount = 200m, TargetLedgerName = "Suspense", Type = TransactionType.Withdrawal },
                    new BankTransaction { TransactionDate = new DateTime(2026, 9, 3), WithdrawalAmount = 300m, TargetLedgerName = "Suspense", Type = TransactionType.Withdrawal },
                    new BankTransaction { TransactionDate = new DateTime(2026, 9, 4), DepositAmount = 400m, TargetLedgerName = "Sales A/c", Type = TransactionType.Deposit },
                    new BankTransaction { TransactionDate = new DateTime(2026, 9, 5), DepositAmount = 500m, TargetLedgerName = "Sales A/c", Type = TransactionType.Deposit }
                }
            };

            string xml = _service.GenerateTallyXml(statement, "ICICI Bank A/c");
            var xdoc = System.Xml.Linq.XDocument.Parse(xml);

            var ledgers = xdoc.Descendants("LEDGER").ToList();
            // Exactly 3 unique ledgers: "Suspense", "Sales A/c", and "ICICI Bank A/c"
            ledgers.Should().HaveCount(3);
            ledgers.Select(l => (string?)l.Attribute("NAME")).Should().BeEquivalentTo(new[] { "Suspense", "Sales A/c", "ICICI Bank A/c" });
        }

        [Fact]
        public void GenerateTallyXml_ContraTransaction_EmitsProperContraVoucherEnvelope()
        {
            var statement = new StatementDocument
            {
                BankName = "State Bank of India",
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        TransactionDate = new DateTime(2026, 9, 15),
                        WithdrawalAmount = 25000m,
                        DepositAmount = 0m,
                        TargetLedgerName = "Cash",
                        RawNarration = "ATM Cash Withdrawal Self",
                        Type = TransactionType.Contra
                    }
                }
            };

            string xml = _service.GenerateTallyXml(statement, "SBI Current A/c");
            var xdoc = System.Xml.Linq.XDocument.Parse(xml);

            var voucher = xdoc.Descendants("VOUCHER").FirstOrDefault();
            voucher.Should().NotBeNull();
            voucher!.Attribute("VCHTYPE")!.Value.Should().Be("Contra");
            voucher.Element("VOUCHERTYPENAME")!.Value.Should().Be("Contra");

            var entries = voucher.Descendants("ALLLEDGERENTRIES.LIST").ToList();
            entries.Should().HaveCount(2);

            // Debited Cash (+25,000 as positive amount) and Credited Bank (-25,000 as negative amount)
            entries.Should().Contain(e => (string?)e.Element("LEDGERNAME") == "Cash" && (decimal)e.Element("AMOUNT")! == -25000m);
            entries.Should().Contain(e => (string?)e.Element("LEDGERNAME") == "SBI Current A/c" && (decimal)e.Element("AMOUNT")! == 25000m);
        }

        [Fact]
        public void GenerateTallyXml_EmptyTransactions_ProducesValidXmlWithBankMaster()
        {
            var statement = new StatementDocument
            {
                BankName = "Punjab National Bank",
                Transactions = new List<BankTransaction>()
            };

            string xml = _service.GenerateTallyXml(statement, "PNB A/c");

            var act = () => System.Xml.Linq.XDocument.Parse(xml);
            act.Should().NotThrow();

            var xdoc = act();
            xdoc.Descendants("LEDGER").Should().HaveCount(1); // Only bank master
            xdoc.Descendants("VOUCHER").Should().BeEmpty();
        }
    }
}
