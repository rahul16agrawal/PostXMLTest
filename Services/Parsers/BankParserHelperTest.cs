using System;
using System.Collections.Generic;
using PostXML.Models;
using PostXML.Services.Parsers;
using Xunit;

namespace PostXMLTest.Services.Parsers
{
    public class BankParserHelperTest
    {
        [Theory]
        [InlineData("UPI/12345/ZOMATO/ORDER", "UPI")]
        [InlineData("NEFT-N12345678-VENDOR PAYMENT", "NEFT")]
        [InlineData("IMPS/P2A/5678/TRANS", "IMPS")]
        [InlineData("RTGS/R12345678/TRANSFER", "RTGS")]
        [InlineData("POS 409823 RELIANCE FRESH", "POS")]
        [InlineData("ATM CASH WDL SOUTH EX", "ATM")]
        [InlineData("SMS ALERT CHG Q3", "CHARGES")]
        [InlineData("ANNUAL CARD FEE", "CHARGES")]
        [InlineData("CLEARING CHEQUE INWARD", "TRANSFER")]
        public void ExtractPaymentMode_IdentifiesCorrectMode(string narration, string expectedMode)
        {
            string mode = BankParserHelper.ExtractPaymentMode(narration);
            Assert.Equal(expectedMode, mode);
        }

        [Theory]
        [InlineData("UPI/4098234123/ZOMATO/ORDER123", "UPI", "ZOMATO")]
        [InlineData("NEFT/567890/INFOSYS/SALARY", "NEFT", "INFOSYS")]
        [InlineData("IMPS/112233/RELIANCE/PAYMENT", "IMPS", "RELIANCE")]
        [InlineData("", "TRANSFER", "General Transaction")]
        public void ExtractCleanEntity_ExtractsMeaningfulPartyName(string narration, string mode, string expectedEntity)
        {
            string entity = BankParserHelper.ExtractCleanEntity(narration, mode);
            Assert.Equal(expectedEntity, entity);
        }

        [Theory]
        [InlineData("UPI/ZOMATO INDIA PVT LTD", "UPI", "Staff Welfare Expense")]
        [InlineData("SWIGGY FOOD ORDER", "UPI", "Staff Welfare Expense")]
        [InlineData("AMAZON RETAIL INDIA", "UPI", "Office Supplies")]
        [InlineData("FLIPKART INTERNET", "UPI", "Office Supplies")]
        [InlineData("INDIAN OIL PETROL PUMP", "POS", "Fuel Expense")]
        [InlineData("BSES ELECTRICITY BILL", "NEFT", "Electricity Expense")]
        [InlineData("STAFF SALARY FOR OCT", "NEFT", "Salary Expense")]
        [InlineData("QUARTERLY SMS CHG", "CHARGES", "Bank Charges")]
        [InlineData("MISCELLANEOUS TRANSFER", "TRANSFER", "Suspense A/c")]
        public void SuggestLedger_MapsCommonVendorsToExpenseLedgers(string entity, string mode, string expectedLedger)
        {
            string ledger = BankParserHelper.SuggestLedger(entity, mode);
            Assert.Equal(expectedLedger, ledger);
        }

        [Theory]
        [InlineData("01/04/2024", 2024, 4, 1)]
        [InlineData("15-08-2023", 2023, 8, 15)]
        [InlineData("05 Jan 2025", 2025, 1, 5)]
        [InlineData("26-Jan-2024", 2024, 1, 26)]
        [InlineData("31/12/24", 2024, 12, 31)]
        public void TryParseTransactionDate_ParsesSupportedFormats(string text, int expectedYear, int expectedMonth, int expectedDay)
        {
            bool success = BankParserHelper.TryParseTransactionDate(text, out DateTime dt);
            Assert.True(success);
            Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), dt);
        }

        [Fact]
        public void TryParseTransactionDate_InvalidString_ReturnsFalse()
        {
            bool success = BankParserHelper.TryParseTransactionDate("not-a-date", out _);
            Assert.False(success);
        }

        [Theory]
        [InlineData("1,234.56", 1234.56)]
        [InlineData("-500.00", -500.00)]
        [InlineData("12345.67", 12345.67)]
        public void TryParseAmount_ParsesValidAmounts(string text, decimal expected)
        {
            bool success = BankParserHelper.TryParseAmount(text, out decimal amt);
            Assert.True(success);
            Assert.Equal(expected, amt);
        }

        [Fact]
        public void CleanNarration_StripsHeadersAndExtraSpaces()
        {
            string raw = "Date Particulars Withdrawals Deposits Balance   UPI/12345/TEST   ";
            string cleaned = BankParserHelper.CleanNarration(raw);
            Assert.Equal("UPI/12345/TEST", cleaned);
        }

        [Theory]
        [InlineData("STATEMENT OF ACCOUNT FOR PERIOD", true)]
        [InlineData("PAGE 1 OF 5", true)]
        [InlineData("TRANSACTION DATE VALUE DATE PARTICULARS", true)]
        [InlineData("UPI/9876543210/PAYMENT TO MERCHANT", false)]
        public void IsIgnorableHeaderFooter_DetectsNoiseLines(string line, bool expected)
        {
            bool isIgnorable = BankParserHelper.IsIgnorableHeaderFooter(line);
            Assert.Equal(expected, isIgnorable);
        }

        [Fact]
        public void ExtractMetadataIfPresent_ExtractsAccountNumberAndBalances()
        {
            var doc = new StatementDocument();

            BankParserHelper.ExtractMetadataIfPresent("Account No : 12345678901234", doc);
            BankParserHelper.ExtractMetadataIfPresent("Opening Balance: 10,500.00", doc);
            BankParserHelper.ExtractMetadataIfPresent("Closing Balance: 25,750.50", doc);

            Assert.Equal("12345678901234", doc.AccountNumber);
            Assert.Equal(10500.00m, doc.OpeningBalance);
            Assert.Equal(25750.50m, doc.ClosingBalance);
        }

        [Fact]
        public void ExtractMetadataIfPresent_DebitBalance_AssignsNegative()
        {
            var doc = new StatementDocument();

            BankParserHelper.ExtractMetadataIfPresent("Opening Balance: 5,000.00 DR", doc);
            Assert.Equal(-5000.00m, doc.OpeningBalance);
        }

        [Fact]
        public void FinalizeTransaction_ContraTransaction_SetsContraType()
        {
            var txn = new BankTransaction
            {
                RawNarration = "CASH WDL ATM SELF",
                WithdrawalAmount = 5000
            };
            var def = new BankProfileDefinition();

            BankParserHelper.FinalizeTransaction(txn, def);

            Assert.Equal(TransactionType.Contra, txn.Type);
        }

        [Fact]
        public void FinalizeTransaction_DepositTransaction_SetsDepositType()
        {
            var txn = new BankTransaction
            {
                RawNarration = "UPI/CUSTOMER/PAYMENT",
                DepositAmount = 1500
            };
            var def = new BankProfileDefinition();

            BankParserHelper.FinalizeTransaction(txn, def);

            Assert.Equal(TransactionType.Deposit, txn.Type);
            Assert.Equal("UPI", txn.PaymentMode);
        }
    }
}
