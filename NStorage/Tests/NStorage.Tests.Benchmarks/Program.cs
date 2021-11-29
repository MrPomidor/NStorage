using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace NStorage.Tests.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .Run(args, GetGlobalConfig());
        }

        static IConfig GetGlobalConfig()
            => DefaultConfig.Instance.AddJob(
                Job.Default
                    .WithWarmupCount(2)
                    .WithIterationCount(20)
                    .WithRuntime(CoreRuntime.Core60)
                );
    }
}
