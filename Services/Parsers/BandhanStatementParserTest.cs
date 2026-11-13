using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PostXML.Models;
using PostXML.Services.Parsers;
using UglyToad.PdfPig;
using Xunit;

namespace PostXMLTest.Services.Parsers
{
    public class BandhanStatementParserTest
    {
        private readonly BandhanStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsBANDHAN()
        {
            Assert.Equal("BANDHAN", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_Statement001_ExtractsAll433Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "BandhanBank", "BandhanBank_Statement_001.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "BANDHAN",
                DisplayName = "Bandhan Bank",
                DateFormat = "dd-MMMM-yyyy"
            };

            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("Bandhan Bank", doc.BankName);
            Assert.Equal("20100083106505", doc.AccountNumber);
            Assert.Equal("M/S BIMLA ENTERPRISES", doc.AccountHolderName);
            Assert.Equal("BDBL0001432", doc.IfscCode);
            Assert.Equal(0.00m, doc.OpeningBalance);
            Assert.Equal(442797.14m, doc.ClosingBalance);
            Assert.Equal(433, doc.Transactions.Count);
            Assert.Equal(7928941.20m, doc.TotalDeposits);
            Assert.Equal(7486144.06m, doc.TotalWithdrawals);

            decimal expectedClosing = doc.OpeningBalance + doc.TotalDeposits - doc.TotalWithdrawals;
            Assert.Equal(doc.ClosingBalance, expectedClosing);

            // Chronological ordering check
            var firstTxn = doc.Transactions.First();
            var lastTxn = doc.Transactions.Last();
            Assert.True(firstTxn.TransactionDate <= lastTxn.TransactionDate);
            Assert.Equal(new DateTime(2026, 6, 15), firstTxn.TransactionDate);
            Assert.Contains("Initial Funding Deposit", firstTxn.RawNarration);
            Assert.Equal(100000.00m, firstTxn.DepositAmount);
            Assert.Equal(100000.00m, firstTxn.RunningBalance);

            Assert.Equal(new DateTime(2026, 11, 13), lastTxn.TransactionDate);
            Assert.Contains("UPI/DR/D389339184174", lastTxn.RawNarration);
            Assert.Equal(16000.00m, lastTxn.WithdrawalAmount);
            Assert.Equal(442797.14m, lastTxn.RunningBalance);
        }
    }
}
