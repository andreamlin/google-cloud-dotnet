﻿// Copyright 2018 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Api.Gax.Grpc;
using Google.Api.Gax.Testing;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Cloud.Spanner.V1.Tests
{
    public partial class SessionPoolTests
    {
        private sealed class SessionTestingSpannerClient : SpannerClient
        {
            private int _sessionCounter;
            private int _deletionCounter;
            private int _transactionCounter;
            public TimeSpan CreateSessionDelay { get; set; } = TimeSpan.FromSeconds(5);
            public TimeSpan BeginTransactionDelay { get; set; } = TimeSpan.FromSeconds(1);
            public TimeSpan ExecuteSqlDelay { get; set; } = TimeSpan.FromSeconds(1);
            public TimeSpan DeleteSessionDelay { get; set; } = TimeSpan.FromSeconds(1);
            // We do quite a bit of work in our continuations, so allow a bit longer than the default of 10ms.
            // Ditto allow a bit longer than normal for any test assertions to complete; this is particularly
            // relevant in tests that generate a lot of logs.
            // This will increase test times, but reduce flakiness.
            public FakeScheduler Scheduler { get; } = new FakeScheduler
            {
                IdleTimeBeforeAdvancing = TimeSpan.FromMilliseconds(200),
                PostLoopSettleTime = TimeSpan.FromMilliseconds(200),
            };
            public FakeClock Clock => Scheduler.Clock;
            public InMemoryLogger Logger { get; } = new InMemoryLogger();
            public int SessionsCreated => Interlocked.CompareExchange(ref _sessionCounter, 0, 0);
            public int SessionsDeleted => Interlocked.CompareExchange(ref _deletionCounter, 0, 0);
            public bool FailAllRpcs { get; set; } = false;

            public ConcurrentQueue<ExecuteSqlRequest> ExecuteSqlRequests { get; } = new ConcurrentQueue<ExecuteSqlRequest>();

            // TODO: Keep track of created and deleted session names?

            public SessionTestingSpannerClient()
            {
                Settings = SpannerSettings.GetDefault();
                Settings.Scheduler = Scheduler;
                Settings.Clock = Clock;
                Settings.Logger = Logger;
            }

            public override async Task<Transaction> BeginTransactionAsync(BeginTransactionRequest request, CallSettings callSettings = null)
            {
                CheckFailAllRpcs();
                await Scheduler.Delay(BeginTransactionDelay);
                int count = Interlocked.Increment(ref _transactionCounter);
                return new Transaction
                {
                    Id = ByteString.CopyFromUtf8($"transaction-{count}"),
                    ReadTimestamp = Timestamp.FromDateTime(Clock.GetCurrentDateTimeUtc())
                };
            }

            public override async Task<Session> CreateSessionAsync(CreateSessionRequest request, CallSettings callSettings = null)
            {
                CheckFailAllRpcs();
                await Scheduler.Delay(CreateSessionDelay);
                int count = Interlocked.Increment(ref _sessionCounter);
                var database = request.DatabaseAsDatabaseName;
                return new Session { SessionName = new SessionName(database.ProjectId, database.InstanceId, database.DatabaseId, $"session-{count}") };
            }

            public override async Task DeleteSessionAsync(DeleteSessionRequest request, CallSettings callSettings = null)
            {
                CheckFailAllRpcs();
                await Scheduler.Delay(DeleteSessionDelay);
                int count = Interlocked.Increment(ref _deletionCounter);
            }

            public override async Task<ResultSet> ExecuteSqlAsync(ExecuteSqlRequest request, CallSettings callSettings = null)
            {
                CheckFailAllRpcs();
                ExecuteSqlRequests.Enqueue(request.Clone());
                await Scheduler.Delay(ExecuteSqlDelay);
                return new ResultSet();
            }

            private void CheckFailAllRpcs()
            {
                if (FailAllRpcs)
                {
                    throw new RpcException(new Status(StatusCode.Internal, "Bang"));
                }
            }
        }
    }
}
