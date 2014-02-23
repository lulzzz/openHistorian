﻿//******************************************************************************************************
//  FirstStageWriter`2.cs - Gbtc
//
//  Copyright © 2014, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  2/16/2014 - Steven E. Chisholm
//       Generated original version of source code. 
//       
//
//******************************************************************************************************

using System;
using System.Threading;
using GSF.Threading;
using GSF.SortedTreeStore.Tree;

namespace GSF.SortedTreeStore.Engine.Writer
{
    /// <summary>
    /// A collection of settings for <see cref="FirstStageWriter{TKey,TValue}"/>.
    /// </summary>
    public struct FirstStageWriterSettings<TKey, TValue>
        where TKey : class, ISortedTreeValue<TKey>, new()
        where TValue : class, ISortedTreeValue<TValue>, new()
    {
        /// <summary>
        /// The time interval in milliseconds after which automatic data commits occur.
        /// </summary>
        public int RolloverInterval;

        /// <summary>
        /// The maximum desired number of points in the prebuffer before a commit now is requested.
        /// </summary>
        public long RolloverSize;

        /// <summary>
        /// The size that a file is permitted to get before entering a wait state to wait for a pending rollover
        /// to complete.
        /// </summary>
        public long MaximumAllowedSize;

        /// <summary>
        /// The helping functions associated with writing a stage file.
        /// </summary>
        public TempFile<TKey, TValue> TempFile;

    }

    /// <summary>
    /// Handles how data is initially taken from prestage chunks and serialized to the disk.
    /// </summary>
    public class FirstStageWriter<TKey, TValue> : IDisposable
        where TKey : class, ISortedTreeValue<TKey>, new()
        where TValue : class, ISortedTreeValue<TValue>, new()
    {
        /// <summary>
        /// Event that notifies that a certain sequence number has been committed.
        /// </summary>
        public event Action<long> SequenceNumberCommitted;

        private bool m_stopped;
        private bool m_disposed;
        private readonly int m_rolloverInterval;
        private readonly long m_rolloverSize;
        private readonly long m_maximumAllowedSize;
        private long m_lastCommitedSequenceNumber;
        private long m_lastRolledOverSequenceNumber;
        private ScheduledTask m_rolloverTask;
        private readonly object m_syncRoot;
        private readonly TempFile<TKey, TValue> m_stagingFile;
        private readonly ManualResetEvent m_rolloverComplete;

        /// <summary>
        /// Creates a stage writer.
        /// </summary>
        /// <param name="settings">the settings for this stage</param>
        /// <param name="onRollover">delegate to call when a file is done with this stage.</param>
        public FirstStageWriter(FirstStageWriterSettings<TKey, TValue> settings)
        {
            if (settings.RolloverSize > settings.MaximumAllowedSize)
                throw new ArgumentOutOfRangeException("settings.MaximumAllowedSize", "must be greater than or equal to settings.RolloverSize");
            m_rolloverComplete = new ManualResetEvent(false);
            m_stagingFile = settings.TempFile;
            m_rolloverInterval = settings.RolloverInterval;
            m_rolloverSize = settings.RolloverSize;
            m_maximumAllowedSize = settings.MaximumAllowedSize;
            m_syncRoot = new object();
            m_rolloverTask = new ScheduledTask(ThreadingMode.DedicatedForeground);
            m_rolloverTask.OnEvent += ProcessRollover;
            m_rolloverTask.Start(m_rolloverInterval);
        }

        /// <summary>
        /// Appends this data to this stage. Also queues up for deletion if necessary.
        /// </summary>
        /// <param name="args">arguments handed to this class from either the 
        /// PrestageWriter or another StageWriter of a previous generation</param>
        public void AppendData(PrestageArgs<TKey, TValue> args)
        {
            if (m_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            long currentSize;

            //If there is data to write then write it to the current archive.
            lock (m_syncRoot)
            {
                if (m_stopped)
                    throw new Exception("No new points can be added. Point queue has been stopped.");

                m_stagingFile.Append(args.Stream);
                m_lastCommitedSequenceNumber = args.SequenceNumber;

                currentSize = m_stagingFile.Size;

                if (currentSize > m_rolloverSize)
                    m_rolloverTask.Start();

                if (currentSize > m_maximumAllowedSize)
                    m_rolloverComplete.Reset();
            }

            if (SequenceNumberCommitted != null)
                SequenceNumberCommitted(args.SequenceNumber);

            if (currentSize > m_maximumAllowedSize)
                m_rolloverComplete.WaitOne();
        }

        private void ProcessRollover(object sender, ScheduledTaskEventArgs e)
        {
            //The worker can be disposed either via the Stop() method or 
            //the Dispose() method.  If via the dispose method, then
            //don't do any cleanup.
            if (m_disposed && e.IsDisposing)
                return;

            //go ahead and schedule the next rollover since nothing
            //will happen until this function exits anyway.
            //if the task is disposing, the following line does nothing.
            m_rolloverTask.Start(m_rolloverInterval);

            long sequenceNumber;
            lock (m_syncRoot)
            {
                m_stagingFile.DumpToDisk();
                sequenceNumber = m_lastCommitedSequenceNumber;
                m_rolloverComplete.Set();
            }
            m_lastRolledOverSequenceNumber = sequenceNumber;
        }

        /// <summary>
        /// Stop all writing to this class.
        /// Once stopped, it cannot be resumed.
        /// All data is then immediately flushed to the output.
        /// This method calls Dispose()
        /// </summary>
        /// <returns></returns>
        public long Stop()
        {
            lock (m_syncRoot)
            {
                m_stopped = true;
            }
            m_rolloverTask.Dispose();
            m_rolloverTask = null;
            Dispose();
            return m_lastCommitedSequenceNumber;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (!m_disposed)
            {
                m_disposed = true;
                if (m_rolloverTask != null)
                    m_rolloverTask.Dispose();
                m_rolloverTask = null;
            }
        }
    }
}