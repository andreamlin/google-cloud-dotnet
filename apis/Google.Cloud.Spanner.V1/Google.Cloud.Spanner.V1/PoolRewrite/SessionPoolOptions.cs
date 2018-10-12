﻿// Copyright 2017 Google Inc. All Rights Reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using System;

namespace Google.Cloud.Spanner.V1.PoolRewrite
{
    /// <summary>
    /// Options for session pools.
    /// </summary>
    public sealed class SessionPoolOptions
    {
        // Note: if any of these defaults are changed, update the XML documentation comments on the properties accordingly.
        private int _maximumActiveSessions = 100;
        private int _minimumPooledSessions = 10;
        private TimeSpan _idleSessionRefreshDelay = TimeSpan.FromMinutes(15);
        private TimeSpan _poolEvictionDelay = TimeSpan.FromDays(7);
        private ResourcesExhaustedBehavior _waitOnResourcesExhausted = ResourcesExhaustedBehavior.Block;
        private double _writeSessionsFraction = 0.2;
        private int _timeout = 60;
        private TimeSpan _maintenanceLoopDelay = TimeSpan.FromSeconds(30);
        private int _maximumConcurrentSessionCreates = 10;
        private RetrySettings.IJitter _sessionRefreshJitter = new ProportionalRandomJitter(0.1);
        private RetrySettings.IJitter _sessionEvictionJitter = new ProportionalRandomJitter(0.1);
        private IClock _clock = SystemClock.Instance;
        private IScheduler _scheduler= SystemScheduler.Instance;

        /// <summary>
        /// Constructs a new <see cref="SessionPoolOptions"/> with default values.
        /// </summary>
        public SessionPoolOptions()
        {
        }

        /// <summary>
        /// Maximum number of sessions that can be active per database. An active session is one that has been
        /// acquired but not yet released back to the pool.
        /// </summary>
        /// <remarks>
        /// This property has a minimum value of 1, and a default value of 100.
        /// </remarks>
        public int MaximumActiveSessions
        {
            get => _maximumActiveSessions;
            set => _maximumActiveSessions = GaxPreconditions.CheckArgumentRange(value, nameof(value), 1, int.MaxValue);
        }

        /// <summary>
        /// The minimum number of sessions to maintain in the pool of available sessions.
        /// If the number of pooled sessions falls below this number, more sessions are added automatically.
        /// </summary>
        /// <remarks>
        /// This property has a minimum value of 0, and a default value of 10.
        /// </remarks>
        public int MinimumPooledSessions
        {
            get => _minimumPooledSessions;
            set => _minimumPooledSessions = GaxPreconditions.CheckNonNegative(value, nameof(value));
        }

        /// <summary>
        /// The amount of time a session must be idle before it is refreshed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property must always be positive. The default value is 15 minutes.
        /// </para>
        /// <para>
        /// The exact value used is subject to "jitter" to avoid a lot of sessions being refreshed at the exact same time.
        /// </para>
        /// <para>
        /// A lower value will cause sessions to be refreshed more often, slightly reducing the risk of sessions expiring
        /// while being used, at the cost of performing more refreshes.
        /// </para>
        /// <para>
        /// This value must be less than the expire timer on the Spanner server which is currently set at 60 minutes.
        /// </para>
        /// </remarks>
        public TimeSpan IdleSessionRefreshDelay
        {
            get => _idleSessionRefreshDelay;
            set => _idleSessionRefreshDelay = CheckPositiveTimeSpan(value);
        }

        /// <summary>
        /// The amount of time before sessions are forcibly evicted from the pool. This is usually in the order of days,
        /// as sessions can be reused for a long time if they're suitably refreshed. Deleting long-lasting sessions
        /// can help with server-side resource management.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property must always be positive. The default value is 7 days.
        /// </para>
        /// <para>
        /// The exact value used is subject to "jitter" to avoid a lot of sessions being evicted at the exact same time.
        /// </para>
        /// <para>
        /// A lower value will cause sessions to be evicted more often, delaying operations if no sessions are available
        /// when requested.
        /// </para>
        /// </remarks>
        public TimeSpan PoolEvictionDelay
        {
            get => _poolEvictionDelay;
            set => _poolEvictionDelay = CheckPositiveTimeSpan(value);
        }

        /// <summary>
        /// Determines the behavior of a request for a session when <see cref="MaximumActiveSessions"/> has been reached.
        /// </summary>
        /// <remarks>
        /// The default value is <see cref="ResourcesExhaustedBehavior.Block"/>.
        /// </remarks>
        public ResourcesExhaustedBehavior WaitOnResourcesExhausted
        {
            get => _waitOnResourcesExhausted;
            set => _waitOnResourcesExhausted = GaxPreconditions.CheckEnumValue(value, nameof(value));
        }

        /// <summary>
        /// Fraction of sessions to be kept prepared for write transactions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is an optimisation to avoid the cost of sending a BeginTransaction() rpc. If all such sessions are in use and a
        /// write request comes, we will make the BeginTransaction() rpc inline.
        /// </para>
        /// <para>This property must always be in the range 0-1 (inclusive). The default value is 0.2.</para>
        /// </remarks>
        public double WriteSessionsFraction
        {
            get => _writeSessionsFraction;
            set => _writeSessionsFraction = GaxPreconditions.CheckArgumentRange(value, nameof(value), 0.0, 1.0);
        }

        /// <summary>
        /// The total time in seconds allowed for a network call to the Cloud Spanner server, including retries. This setting
        /// is applied to calls to create, refresh and delete sessions, as well as beginning transactions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value cannot be negative, but it can be 0. The behavior of a 0 value depends on <see cref="SpannerSettings.AllowImmediateTimeouts"/>.
        /// The default value is 60.
        /// </para>
        /// </remarks>
        public int Timeout
        {
            get => _timeout;
            set => _timeout = GaxPreconditions.CheckNonNegative(value, nameof(value));
        }

        /// <summary>
        /// The maximum number of session create operations allowed to occur simultaneously per session pool.
        /// Spanner has limits on the number of sessions that can be created concurrently without affecting performance.
        /// This value is not typically changed.
        /// </summary>
        /// <para>
        /// This value must be positive. The default value is 10.
        /// </para>
        public int MaximumConcurrentSessionCreates
        {
            get => _maximumConcurrentSessionCreates;
            set => _maximumConcurrentSessionCreates = GaxPreconditions.CheckArgumentRange(value, nameof(value), 1, int.MaxValue);
        }

        /// <summary>
        /// The delay between maintenance loop iterations. Defaults to 30 seconds.
        /// </summary>
        internal TimeSpan MaintenanceLoopDelay
        {
            get => _maintenanceLoopDelay;
            set => _maintenanceLoopDelay = CheckPositiveTimeSpan(value);
        }

        /// <summary>
        /// Jitter to apply to session refresh times. Defaults to a 10% proportionally random jitter.
        /// </summary>
        internal RetrySettings.IJitter SessionRefreshJitter
        {
            get => _sessionRefreshJitter;
            set => _sessionRefreshJitter = GaxPreconditions.CheckNotNull(value, nameof(value));
        }

        /// <summary>
        /// Jitter to apply to session eviction times. Defaults to a 10% proportionally random jitter.
        /// </summary>
        internal RetrySettings.IJitter SessionEvictionJitter
        {
            get => _sessionEvictionJitter;
            set => _sessionEvictionJitter = GaxPreconditions.CheckNotNull(value, nameof(value));
        }

        /// <summary>
        /// Clock used for timings; can be replaced for testing.
        /// </summary>
        internal IClock Clock
        {
            get => _clock;
            set => _clock = GaxPreconditions.CheckNotNull(value, nameof(value));
        }

        /// <summary>
        /// Scheduler used for delays (e.g. the pool maintenance loop); can be replaced for testing.
        /// </summary>
        internal IScheduler Scheduler
        {
            get => _scheduler;
            set => _scheduler = GaxPreconditions.CheckNotNull(value, nameof(value));
        }

        // TODO: Move to GAX if we find we need it in other libraries. (We have CheckNonNegative already.)
        private static TimeSpan CheckPositiveTimeSpan(TimeSpan value)
        {
            if (value.Ticks <= 0)
            {
                throw new ArgumentOutOfRangeException("value", "Value must be a positive TimeSpan");
            }
            return value;
        }
    }
}
