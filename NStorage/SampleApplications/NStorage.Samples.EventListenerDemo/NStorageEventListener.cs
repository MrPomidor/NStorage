using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

namespace NStorage.Samples.EventListenerDemo
{
    internal class NStorageEventListener : EventListener
    {
        private readonly ConcurrentDictionary<string, string> _counters = new ConcurrentDictionary<string, string>();

        public IReadOnlyDictionary<string, string> GetCountersSnapshot() => _counters.ToList().ToDictionary(kv => kv.Key, kv => kv.Value);

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
                    var (counterName, counterValue) = GetRelevantMetric(eventPayload);
                    _counters[counterName] = counterValue;
                }
            }
        }

        private static (string counterName, string counterValue) GetRelevantMetric(
            IDictionary<string, object> eventPayload)
        {
            var counterName = "";
            var counterValue = "";

            if (eventPayload.TryGetValue("DisplayName", out object displayValue))
            {
                counterName = displayValue.ToString();
            }
            if (eventPayload.TryGetValue("Mean", out object value) ||
                eventPayload.TryGetValue("Increment", out value))
            {
                counterValue = value.ToString();
            }

            return (counterName, counterValue);
        }
    }
}
