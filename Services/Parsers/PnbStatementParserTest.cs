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
    public class PnbStatementParserTest
    {
        private readonly PnbStatementParser _parser = new();

        [Fact]
        public void SupportedBankCode_ReturnsPNB()
        {
            Assert.Equal("PNB", _parser.SupportedBankCode);
        }

        [Fact]
        public void Parse_Statement001_ExtractsAll367Transactions()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string pdfPath = Path.Combine(basePath, "TestData", "PNBBank", "PNBBank_Statement_001.pdf");
            Assert.True(File.Exists(pdfPath), $"PDF fixture not found at {pdfPath}");

            var profile = new BankProfile
            {
                BankCode = "PNB",
                DisplayName = "Punjab National Bank",
                DateFormat = "dd/MM/yyyy"
            };

            var options = new ParsingOptions { Password = "9881002100002425" };
            using var stream = File.OpenRead(pdfPath);
            using var pdfDoc = PdfDocument.Open(stream, options);

            var doc = _parser.Parse(pdfDoc, profile, new List<string>());

            Assert.NotNull(doc);
            Assert.Equal("Punjab National Bank", doc.BankName);
            Assert.Equal("9881002100002425", doc.AccountNumber);
            Assert.Equal("ANUSHKA COAL BRIQUETTE", doc.AccountHolderName);
            Assert.Equal("PUNB0988100", doc.IfscCode);
            Assert.Equal(284492.81m, doc.OpeningBalance);
            Assert.Equal(71021.46m, doc.ClosingBalance);
            Assert.Equal(367, doc.Transactions.Count);
            Assert.Equal(23580301.20m, doc.TotalDeposits);
            Assert.Equal(23793772.55m, doc.TotalWithdrawals);
        }
    }
}
