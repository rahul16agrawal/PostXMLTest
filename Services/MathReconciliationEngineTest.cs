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
    public class MathReconciliationEngineTest
    {
        private readonly Mock<ILogger<MathReconciliationEngine>> _loggerMock;
        private readonly MathReconciliationEngine _engine;

        public MathReconciliationEngineTest()
        {
            _loggerMock = new Mock<ILogger<MathReconciliationEngine>>();
            _engine = new MathReconciliationEngine(_loggerMock.Object);
        }

        [Fact]
        public void Reconcile_WhenBalancedStatement_ReturnsFullyReconciled()
        {
            // Arrange: Opening 1000 + 500 (Deposit) - 200 (Withdrawal) = 1300
            var statement = new StatementDocument
            {
                OpeningBalance = 1000.00m,
                ClosingBalance = 1300.00m,
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        TransactionDate = DateTime.Today.AddDays(-1),
                        DepositAmount = 500.00m,
                        WithdrawalAmount = 0m,
                        RunningBalance = 1500.00m
                    },
                    new BankTransaction
                    {
                        TransactionDate = DateTime.Today,
                        DepositAmount = 0m,
                        WithdrawalAmount = 200.00m,
                        RunningBalance = 1300.00m
                    }
                }
            };

            // Act
            var result = _engine.Reconcile(statement);

            // Assert
            result.IsFullyReconciled.Should().BeTrue();
            result.ExpectedClosingBalance.Should().Be(1300.00m);
            result.ActualClosingBalance.Should().Be(1300.00m);
            result.DiscrepancyAmount.Should().Be(0m);
            result.MismatchedRowIndices.Should().BeEmpty();
            result.StatusBadgeText.Should().Be("● 100% Reconciled");
            result.StatusBadgeColor.Should().Be("#10B981");

            statement.TotalDeposits.Should().Be(500.00m);
            statement.TotalWithdrawals.Should().Be(200.00m);
        }

        [Fact]
        public void Reconcile_WhenRowBalanceMismatched_FlagsRowIndex()
        {
            // Arrange: Bank statement row has an internal typo
            var statement = new StatementDocument
            {
                OpeningBalance = 1000.00m,
                ClosingBalance = 1300.00m,
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        DepositAmount = 500.00m,
                        WithdrawalAmount = 0m,
                        RunningBalance = 1450.00m // Error: should be 1500
                    },
                    new BankTransaction
                    {
                        DepositAmount = 0m,
                        WithdrawalAmount = 200.00m,
                        RunningBalance = 1300.00m
                    }
                }
            };

            // Act
            var result = _engine.Reconcile(statement);

            // Assert
            result.IsFullyReconciled.Should().BeFalse();
            result.MismatchedRowIndices.Should().Contain(1);
            result.StatusBadgeText.Should().Be("● Math Mismatch Detected");
            result.StatusBadgeColor.Should().Be("#EF4444");
        }

        [Fact]
        public void Reconcile_WhenClosingBalanceMismatched_FlagsDiscrepancy()
        {
            // Arrange: Stated closing balance in header is 9999 (wrong)
            var statement = new StatementDocument
            {
                OpeningBalance = 1000.00m,
                ClosingBalance = 9999.00m,
                Transactions = new List<BankTransaction>
                {
                    new BankTransaction
                    {
                        DepositAmount = 500.00m,
                        WithdrawalAmount = 0m,
                        RunningBalance = 1500.00m
                    }
                }
            };

            // Act
            var result = _engine.Reconcile(statement);

            // Assert
            result.IsFullyReconciled.Should().BeFalse();
            result.DiscrepancyAmount.Should().Be(8499.00m);
        }

        [Fact]
        public void Reconcile_WhenOpeningBalanceIsZero_InfersOpeningBalanceFromFirstTransaction()
        {
            // Arrange: Negative balance (e.g. OD/CC account)
            // First transaction deposit 25,000, running balance -3,029,604.43
            // Inferred opening: -3,029,604.43 - 25,000 = -3,054,604.43
            var statement = new StatementDocument
            {
                OpeningBalance = 0m,
                ClosingBalance = 0m,
                Transactions = new List<BankTransaction>
                {
                    new()
                    {
                        DepositAmount = 25000.00m,
                        WithdrawalAmount = 0m,
                        RunningBalance = -3029604.43m
                    },
                    new()
                    {
                        DepositAmount = 4620.00m,
                        WithdrawalAmount = 0m,
                        RunningBalance = -3024984.43m
                    }
                }
            };

            // Act
            var result = _engine.Reconcile(statement);

            // Assert
            result.IsFullyReconciled.Should().BeTrue();
            statement.OpeningBalance.Should().Be(-3054604.43m);
            statement.ClosingBalance.Should().Be(-3024984.43m);
            result.MismatchedRowIndices.Should().BeEmpty();
            result.DiscrepancyAmount.Should().Be(0m);
        }

        [Fact]
        public void Reconcile_WithNullDocument_ThrowsArgumentNullException()
        {
            var act = () => _engine.Reconcile(null!);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}
