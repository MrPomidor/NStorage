using System;
using System.Diagnostics.Tracing;
using System.Threading;

namespace NStorage.Tracing
{
    [EventSource(Name = EventSources.Add)]
    internal class AddEventSource : EventSource
    {
        public static readonly AddEventSource Log = new();

        private ulong _totalBytesAdded = 0;

        private EventCounter? _averageAddedCounter;
        private PollingCounter? _totalAddedCounter;
        private AddEventSource() { }

        [Event(eventId: 1, Message = "Added stream with length: {0}")]
        public void AddStream(long length)
        {
            WriteEvent(1, length);
            _averageAddedCounter?.WriteMetric(length);
            Interlocked.Add(ref _totalBytesAdded, Convert.ToUInt64(length));
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                // This is the convention for initializing counters in the RuntimeEventSource (lazily on the first enable command).
                // They aren't disabled afterwards...

                _averageAddedCounter ??= new EventCounter(EventCounters.Add.AddAverageLength, this)
                {
                    DisplayName = "Average Added Bytes",
                    DisplayUnits = "bytes"
                };
                _totalAddedCounter ??= new PollingCounter(EventCounters.Add.AddTotalLength, this, () => Volatile.Read(ref _totalBytesAdded))
                {
                    DisplayName = "Total Added Bytes",
                    DisplayUnits = "bytes"
                };
            }
        }

        protected override void Dispose(bool disposing)
        {
            _averageAddedCounter?.Dispose();
            _averageAddedCounter = null;

            _totalAddedCounter?.Dispose();
            _totalAddedCounter = null;

            base.Dispose(disposing);
        }
    }
}
