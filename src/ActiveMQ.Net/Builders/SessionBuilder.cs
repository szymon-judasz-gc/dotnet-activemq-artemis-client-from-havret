﻿using System.Threading;
using System.Threading.Tasks;
using ActiveMQ.Net.Exceptions;
using Amqp;
using Amqp.Framing;
using Microsoft.Extensions.Logging;

namespace ActiveMQ.Net.Builders
{
    internal class SessionBuilder
    {
        private const uint DefaultWindowSize = 2048;
        private const int DefaultMaxLinksPerSession = 63;
        
        private readonly ILoggerFactory _loggerFactory;
        private readonly Amqp.Connection _connection;
        private readonly TaskCompletionSource<bool> _tcs;

        public SessionBuilder(ILoggerFactory loggerFactory, Amqp.Connection connection)
        {
            _loggerFactory = loggerFactory;
            _connection = connection;
            _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public async Task<Session> CreateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            cancellationToken.Register(() => _tcs.TrySetCanceled());

            var begin = new Begin
            {
                IncomingWindow = DefaultWindowSize,
                OutgoingWindow = DefaultWindowSize,
                HandleMax = DefaultMaxLinksPerSession,
                NextOutgoingId = uint.MaxValue - 2u
            };

            var session = new Session(_connection, begin, OnBegin);
            session.AddClosedCallback(OnClosed);
            await _tcs.Task.ConfigureAwait(false);
            session.Closed -= OnClosed;
            return session;
        }

        private void OnBegin(ISession session, Begin begin)
        {
            _tcs.TrySetResult(true);
        }
        
        private void OnClosed(IAmqpObject sender, Error error)
        {
            if (error != null)
            {
                _tcs.TrySetException(CreateSessionException.FromError(error));
            }
        }
    }
}