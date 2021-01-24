#region License

/* 
 * All content copyright Marko Lahma, unless otherwise indicated. All rights reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not 
 * use this file except in compliance with the License. You may obtain a copy 
 * of the License at 
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0 
 *   
 * Unless required by applicable law or agreed to in writing, software 
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations 
 * under the License.
 * 
 */

#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Quartz.Listener
{
    /// <summary>
    /// Holds a List of references to JobListener instances and broadcasts all
    /// events to them (in order).
    /// </summary>
    /// <remarks>
    /// <para>The broadcasting behavior of this listener to delegate listeners may be
    /// more convenient than registering all of the listeners directly with the
    /// Scheduler, and provides the flexibility of easily changing which listeners
    /// get notified.</para>
    /// </remarks>
    /// <seealso cref="AddListener(IJobListener)" />
    /// <seealso cref="RemoveListener(IJobListener)" />
    /// <seealso cref="RemoveListener(string)" />
    /// <author>James House (jhouse AT revolition DOT net)</author>
    public class BroadcastJobListener : IJobListener
    {
        private readonly List<IJobListener> listeners;
        private readonly ILogger<BroadcastJobListener> logger;

        /// <summary>
        /// Construct an instance with the given name.
        /// </summary>
        /// <remarks>
        /// (Remember to add some delegate listeners!)
        /// </remarks>
        public BroadcastJobListener(
            ILogger<BroadcastJobListener> logger,
            string name)
        {
            this.logger = logger;
            Name = name ?? throw new ArgumentNullException(nameof(name), "Listener name cannot be null!");
            listeners = new List<IJobListener>();
        }

        /// <summary>
        /// Construct an instance with the given name, and List of listeners.
        /// </summary>
        public BroadcastJobListener(ILogger<BroadcastJobListener> logger, string name, List<IJobListener> listeners) : this(logger, name)
        {
            this.listeners.AddRange(listeners);
        }

        public string Name { get; }

        public void AddListener(IJobListener listener)
        {
            listeners.Add(listener);
        }

        public bool RemoveListener(IJobListener listener)
        {
            return listeners.Remove(listener);
        }

        public bool RemoveListener(string listenerName)
        {
            var listener = listeners.Find(x => x.Name == listenerName);
            if (listener != null)
            {
                listeners.Remove(listener);
                return true;
            }
            return false;
        }

        public IReadOnlyList<IJobListener> Listeners => listeners;

        public Task JobToBeExecuted(
            IJobExecutionContext context, 
            CancellationToken cancellationToken = default)
        {
            return IterateListenersInGuard(l => l.JobToBeExecuted(context, cancellationToken), nameof(JobToBeExecuted));
        }

        public Task JobExecutionVetoed(
            IJobExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            return IterateListenersInGuard(l => l.JobExecutionVetoed(context, cancellationToken), nameof(JobExecutionVetoed));
        }

        public Task JobWasExecuted(IJobExecutionContext context,
            JobExecutionException? jobException,
            CancellationToken cancellationToken = default)
        {
            return IterateListenersInGuard(l => l.JobWasExecuted(context, jobException, cancellationToken), nameof(JobWasExecuted));
        }

        private async Task IterateListenersInGuard(Func<IJobListener, Task> action, string methodName)
        {
            foreach (var listener in listeners)
            {
                try
                {
                    await action(listener).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Listener {ListenerName} - method {MethodName} raised an exception", listener.Name, methodName);
                }
            }
        }
    }
}
