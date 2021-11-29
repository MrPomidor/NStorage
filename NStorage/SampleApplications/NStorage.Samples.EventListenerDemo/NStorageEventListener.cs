using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

namespace NStorage.Samples.EventListenerDemo
{
    internal class NStorageEventListener : EventListener
    {
        private readonly ConcurrentDictionary<string, decimal> _counters = new ConcurrentDictionary<string, decimal>();

        public IReadOnlyDictionary<string, decimal> GetCountersSnapshot() => _counters.ToList().ToDictionary(kv => kv.Key, kv => kv.Value);

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (!source.Name.StartsWith("NStorage"))
            {
                return;
            }

            EnableEvents(source, EventLevel.Verbose, EventKeywords.All, new Dictionary<string, string>()
            {
                ["EventCounterIntervalSec"] = "1"
            });
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (!eventData.EventName.Equals("EventCounters"))
            {
                return;
            }

            for (int i = 0; i < eventData.Payload.Count; ++i)
            {
                if (eventData.Payload[i] is IDictionary<string, object> eventPayload)
                {
                    UpdateMetricMax(eventPayload);
                }
            }
        }

        private void UpdateMetricMax(IDictionary<string, object> eventPayload)
        {
            var counterName = "";
            decimal counterValue = 0;

            if (eventPayload.TryGetValue("DisplayName", out object displayValue))
            {
                counterName = displayValue.ToString();
            }
            if (eventPayload.TryGetValue("Mean", out object value) ||
                eventPayload.TryGetValue("Increment", out value))
            {
                counterValue = decimal.Parse(value.ToString());
                _counters.AddOrUpdate(counterName, (_) => counterValue, (_, oldValue) => Math.Max(oldValue, counterValue));
            }
        }
    }
}
