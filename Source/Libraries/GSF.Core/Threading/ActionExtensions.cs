﻿//******************************************************************************************************
//  ActionExtensions.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  02/02/2016 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Threading;

namespace GSF.Threading
{
    /// <summary>
    /// Defines extension methods for actions.
    /// </summary>
    public static class ActionExtensions
    {
        // Cancellatin token to cancel delayed operations.
        private class DelayCancellationToken : ICancellationToken
        {
            #region [ Members ]

            // Fields
            private const int Idle = 0;
            private const int Busy = 1;
            private const int Disposed = 2;

            private readonly ManualResetEvent WaitObj;
            private int m_state;

            #endregion

            #region [ Constructors ]

            public DelayCancellationToken(ManualResetEvent waitObj)
            {
                WaitObj = waitObj;
            }

            #endregion

            #region [ Methods ]

            public bool Cancel()
            {
                // If the token is not idle, that means that either a Cancel
                // operation is in progress or the token has been disposed
                if (Interlocked.CompareExchange(ref m_state, Busy, Idle) != Idle)
                    return true;

                try
                {
                    // Determine whether the wait
                    // handle has already been set
                    if (WaitObj.WaitOne(0))
                        return true;

                    // Set the wait handle and return a value indicating
                    // that the token was not previously cancelled
                    WaitObj.Set();
                    return false;
                }
                finally
                {
                    // Set the state back to idle and dispose of the wait
                    // handle if Dispose() was called while the token was busy
                    if (Interlocked.CompareExchange(ref m_state, Idle, Busy) == Disposed)
                        WaitObj.Dispose();
                }
            }

            public void Dispose()
            {
                // Set state to Disposed, but do not dispose
                // of the wait handle if the token is busy
                if (Interlocked.Exchange(ref m_state, Disposed) != Idle)
                    return;

                // Set the wait handle
                // and dispose of it
                WaitObj.Set();
                WaitObj.Dispose();
            }

            #endregion
        }

        /// <summary>
        /// Execute an action on the thread pool after a specified number of milliseconds.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="waitObj">The wait handle to be used to cancel execution.</param>
        /// <param name="delay">The amount of time to wait before execution, in milliseconds.</param>
        public static void DelayAndExecute(this Action action, WaitHandle waitObj, int delay)
        {
            object waitHandleLock = new object();
            RegisteredWaitHandle waitHandle = null;

            WaitOrTimerCallback callback = (state, timeout) =>
            {
                if (Interlocked.Exchange(ref waitHandleLock, null) == null)
                    waitHandle.Unregister(null);

                if (!timeout)
                    return;

                action();
            };

            waitHandle = ThreadPool.RegisterWaitForSingleObject(waitObj, callback, null, delay, true);

            if (Interlocked.Exchange(ref waitHandleLock, null) == null)
                waitHandle.Unregister(null);
        }

        /// <summary>
        /// Execute an action on the thread pool after a specified number of milliseconds.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="delay">The amount of time to wait before execution, in milliseconds.</param>
        /// <returns>A cancellation token that can be used to cancel the operation.</returns>
        public static ICancellationToken DelayAndExecute(this Action action, int delay)
        {
            object waitHandleLock = new object();
            ManualResetEvent waitObj = new ManualResetEvent(false);
            DelayCancellationToken cancellationToken = new DelayCancellationToken(waitObj);
            RegisteredWaitHandle waitHandle = null;

            WaitOrTimerCallback callback = (state, timeout) =>
            {
                if (Interlocked.Exchange(ref waitHandleLock, null) == null)
                {
                    waitHandle.Unregister(null);
                    cancellationToken.Dispose();
                }

                if (!timeout)
                    return;

                action();
            };

            waitHandle = ThreadPool.RegisterWaitForSingleObject(waitObj, callback, null, delay, true);

            if (Interlocked.Exchange(ref waitHandleLock, null) == null)
            {
                waitHandle.Unregister(null);
                cancellationToken.Dispose();
            }

            return cancellationToken;
        }
    }
}