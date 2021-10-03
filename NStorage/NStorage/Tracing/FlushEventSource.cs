using System.Diagnostics.Tracing;
using System.Threading;

namespace NStorage.Tracing
{
    [EventSource(Name = EventSources.Flush)]
    internal class FlushEventSource : EventSource
    {
        public static readonly FlushEventSource Log = new();

        private ulong _flushedManual = 0;
        private ulong _flushedAuto = 0;

        private PollingCounter? _flushManualCounter;
        private PollingCounter? _flushAutoCounter;
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

                _flushManualCounter ??= new PollingCounter(EventCounters.Flush.FlushManual, this, () => Volatile.Read(ref _flushedManual))
                {
                    DisplayName = "Manual Flush Called"
                };
                _flushAutoCounter ??= new PollingCounter(EventCounters.Flush.FlushAuto, this, () => Volatile.Read(ref _flushedAuto))
                {
                    DisplayName = "Auto Flush Called"
                };
            }
        }

        protected override void Dispose(bool disposing)
        {
            _flushManualCounter?.Dispose();
            _flushManualCounter = null;

            _flushAutoCounter?.Dispose();
            _flushAutoCounter = null;

            base.Dispose(disposing);
        }
    }
}
