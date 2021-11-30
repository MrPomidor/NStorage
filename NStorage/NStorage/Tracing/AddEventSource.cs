using System;
using System.Diagnostics.Tracing;
using System.Threading;

namespace NStorage.Tracing
{
    [EventSource(Name = EventSources.Add)]
    internal class AddEventSource : EventSource
    {
        public static readonly AddEventSource Log = new AddEventSource();

        private long _totalBytesAdded = 0;

        private EventCounter _averageAddedCounter;
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
        private PollingCounter _totalAddedCounter;
#endif
        private AddEventSource() { }

        [Event(eventId: 1, Message = "Added stream with length: {0}")]
        public void AddStream(long length)
        {
            WriteEvent(1, length);
            _averageAddedCounter?.WriteMetric(length);
            Interlocked.Add(ref _totalBytesAdded, Convert.ToInt64(length));
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                // This is the convention for initializing counters in the RuntimeEventSource (lazily on the first enable command).
                // They aren't disabled afterwards...

                _averageAddedCounter = _averageAddedCounter ?? new EventCounter(EventCounters.Add.AddAverageLength, this)
                {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                    DisplayName = "Average Added Bytes",
                    DisplayUnits = "bytes"
#endif
                };
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                _totalAddedCounter = _totalAddedCounter ?? new PollingCounter(EventCounters.Add.AddTotalLength, this, () => Volatile.Read(ref _totalBytesAdded))
                {
                    DisplayName = "Total Added Bytes",
                    DisplayUnits = "bytes"
                };
#endif
            }
        }

        protected override void Dispose(bool disposing)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            _averageAddedCounter?.Dispose();
            _averageAddedCounter = null;

            _totalAddedCounter?.Dispose();
            _totalAddedCounter = null;
#endif
            base.Dispose(disposing);
        }
    }
}
