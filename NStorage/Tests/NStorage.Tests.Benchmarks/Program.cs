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
            => DefaultConfig.Instance
            .AddJob(CoreRuntime.Core60)
            // uncomment if you want to check performance on other platforms
            //.AddJob(CoreRuntime.Core50)
            //.AddJob(CoreRuntime.Core31)
            //.AddJob(ClrRuntime.Net461)
            ;
    }

    public static class ConfigExtensions
    {
        public static ManualConfig AddJob(this IConfig config, Runtime runtime)
        {
            return config.AddJob(
                Job.Default
                    .WithWarmupCount(2)
                    .WithIterationCount(20)
                    .WithRuntime(runtime)
                );
        }
    }
}
