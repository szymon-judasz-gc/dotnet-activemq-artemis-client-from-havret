﻿using System;
using System.Threading;
using System.Threading.Tasks;
using ActiveMQ.Net.Transactions;
using Xunit;
using Xunit.Abstractions;

namespace ActiveMQ.Net.IntegrationTests
{
    public class TransactionsSpec : ActiveMQNetIntegrationSpec
    {
        public TransactionsSpec(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task Should_deliver_messages_when_transaction_committed()
        {
            await using var connection = await CreateConnection();
            var address = nameof(Should_deliver_messages_when_transaction_committed);
            await using var producer = await connection.CreateProducerAsync(address, AddressRoutingType.Anycast);
            await using var consumer = await connection.CreateConsumerAsync(address, QueueRoutingType.Anycast);

            var transaction = new Transaction();
            await producer.SendAsync(new Message("foo1"), transaction);
            await producer.SendAsync(new Message("foo2"), transaction);

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await consumer.ReceiveAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token));

            await transaction.CommitAsync();

            var msg1 = await consumer.ReceiveAsync(CancellationToken);
            var msg2 = await consumer.ReceiveAsync(CancellationToken);
            Assert.Equal("foo1", msg1.GetBody<string>());
            Assert.Equal("foo2", msg2.GetBody<string>());
            
            consumer.Accept(msg1);
            consumer.Accept(msg2);
        }

        [Fact]
        public async Task Should_not_deliver_any_messages_when_transaction_rolled_back()
        {
            await using var connection = await CreateConnection();
            var address = nameof(Should_deliver_messages_when_transaction_committed);
            await using var producer = await connection.CreateProducerAsync(address, AddressRoutingType.Anycast);
            await using var consumer = await connection.CreateConsumerAsync(address, QueueRoutingType.Anycast);

            var transaction = new Transaction();
            await producer.SendAsync(new Message("foo1"), transaction);
            await producer.SendAsync(new Message("foo2"), transaction);

            await transaction.RollbackAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await consumer.ReceiveAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token));
        }
        
        [Fact]
        public async Task Should_handle_two_transactions_independently_using_one_producer()
        {
            await using var connection = await CreateConnection();
            var address = nameof(Should_handle_two_transactions_independently_using_one_producer);
            await using var producer = await connection.CreateProducerAsync(address, AddressRoutingType.Anycast);
            await using var consumer = await connection.CreateConsumerAsync(address, QueueRoutingType.Anycast);

            var transaction1 = new Transaction();
            var transaction2 = new Transaction();
            await producer.SendAsync(new Message(1), transaction1);
            await producer.SendAsync(new Message(2), transaction1);
            await producer.SendAsync(new Message(3), transaction2);
            await producer.SendAsync(new Message(4), transaction2);

            await transaction1.CommitAsync();
            await transaction2.CommitAsync();

            for (int i = 1; i <= 4; i++)
            {
                var msg = await consumer.ReceiveAsync(CancellationToken);
                Assert.Equal(i, msg.GetBody<int>());
                consumer.Accept(msg);
            }
        }
        
        [Fact]
        public async Task Should_handle_two_transactions_independently_using_one_producer_when_first_committed_and_second_rolled_back()
        {
            await using var connection = await CreateConnection();
            var address = nameof(Should_handle_two_transactions_independently_using_one_producer);
            await using var producer = await connection.CreateProducerAsync(address, AddressRoutingType.Anycast);
            await using var consumer = await connection.CreateConsumerAsync(address, QueueRoutingType.Anycast);

            var transaction1 = new Transaction();
            var transaction2 = new Transaction();
            await producer.SendAsync(new Message(1), transaction1);
            await producer.SendAsync(new Message(2), transaction1);
            await producer.SendAsync(new Message(3), transaction2);
            await producer.SendAsync(new Message(4), transaction2);

            await transaction1.CommitAsync();
            await transaction2.RollbackAsync();

            for (int i = 1; i <= 2; i++)
            {
                var msg = await consumer.ReceiveAsync(CancellationToken);
                Assert.Equal(i, msg.GetBody<int>());
                consumer.Accept(msg);
            }
            
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await consumer.ReceiveAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token));
        }
    }
}