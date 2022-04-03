namespace NStorage
{
    internal static class Consts
    {
        private const string LibName = "BinaryStorage";

#if NET6_0_OR_GREATER
        internal const string LogPrefix = $"{LibName}::";
#else
        internal const string LogPrefix = LibName + "::";
#endif

    }
}
