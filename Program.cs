using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Latency;

namespace bounded_disturbance
{

    [RPlotExporter, RankColumn]
    public class NominalSystem
    {
        private HttpClient _client;
        private Uri _psapiUrl = new Uri(@"https://psapi-preprod.nrk.no/playback/manifest/channel/nrk1");

        [GlobalSetup]
        public void Setup()
        {
            _client = new HttpClient(); 
        }

        [Benchmark]
        public async Task GetManifestNominal()
        {
            var foo = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 10; i++)
            {
                foo.Add(_client.GetAsync(_psapiUrl));
            }

            await Task.WhenAll(foo);
        }

        [Benchmark]
        public async Task GetManifestWithLatency()
        {
            var chaosPolicy = MonkeyPolicy.InjectLatencyAsync(with =>
                with.Latency(TimeSpan.FromSeconds(4))
                    .InjectionRate(0.1)
                    .Enabled(true)); 

            var foo = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 10; i++)
            {
                foo.Add(chaosPolicy.ExecuteAsync(() => _client.GetAsync(_psapiUrl)));
            }

            await Task.WhenAll(foo);
        }

        [Benchmark]
        public async Task GetManifestWithLatencyAndPolly()
        {
            var chaosPolicy = MonkeyPolicy.InjectLatencyAsync(with =>
                with.Latency(TimeSpan.FromSeconds(2))
                    .InjectionRate(0.05)
                    .Enabled(true));

            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromMilliseconds(150));

            var retry = Policy.Handle<TimeoutException>().RetryAsync(3);
            var policy = Policy.WrapAsync(chaosPolicy, retry, timeoutPolicy);

            var foo = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 10; i++)
            {
                foo.Add(policy.ExecuteAsync(() => _client.GetAsync(_psapiUrl)));
            }

            await Task.WhenAll(foo);
        }
      }

    public class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<NominalSystem>();
            Console.Write(summary);
            Console.ReadKey();
        }
    }
}
