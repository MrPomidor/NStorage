namespace NStorage.Tracing
{
    public static class EventSources
    {
        public const string Add = "NStorage.Tracing.Add";
        public const string Flush = "NStorage.Tracing.Flush";
    }

    public static class EventCounters
    {
        public static class Add
        {
            public const string AddAverageLength = "add-length-avg";
            public const string AddTotalLength = "add-length-total";
        }

        public static class Flush
        {
            public const string FlushManual = "flush-manual-count";
            public const string FlushAuto = "flush-auto-count";
        }
    }
}
