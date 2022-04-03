using System.Diagnostics.Tracing;
using System.Threading;

namespace NStorage.Tracing
{
    [EventSource(Name = EventSources.Flush)]
    internal class FlushEventSource : EventSource
    {
        public static readonly FlushEventSource Log = new FlushEventSource();

        private long _flushedManual = 0;
        private long _flushedAuto = 0;
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
        private PollingCounter _flushManualCounter;
        private PollingCounter _flushAutoCounter;
#endif
        private FlushEventSource() { }

        [Event(eventId: 1, Message = "Flushed Manual")]
        public void FlushManual()
        {
            WriteEvent(1);
            Interlocked.Increment(ref _flushedManual);
        }

        [Event(eventId: 2, Message = "Flushed Auto")]
        public void FlushAuto()
        {
            WriteEvent(2);
            Interlocked.Increment(ref _flushedAuto);
        }

        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            if (command.Command == EventCommand.Enable)
            {
                // This is the convention for initializing counters in the RuntimeEventSource (lazily on the first enable command).
                // They aren't disabled afterwards...

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                _flushManualCounter = _flushManualCounter ?? new PollingCounter(EventCounters.Flush.FlushManual, this, () => Volatile.Read(ref _flushedManual))
                {
                    DisplayName = "Manual Flush Called"
                };
                _flushAutoCounter = _flushAutoCounter ?? new PollingCounter(EventCounters.Flush.FlushAuto, this, () => Volatile.Read(ref _flushedAuto))
                {
                    DisplayName = "Auto Flush Called"
                };
#endif
            }
        }

        protected override void Dispose(bool disposing)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            _flushManualCounter?.Dispose();
            _flushManualCounter = null;

            _flushAutoCounter?.Dispose();
            _flushAutoCounter = null;
#endif

            base.Dispose(disposing);
        }
    }
}
