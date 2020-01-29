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
                with.Latency(TimeSpan.FromSeconds(10))
                    .InjectionRate(0.02)
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
                with.Latency(TimeSpan.FromSeconds(10))
                    .InjectionRate(0.02)
                    .Enabled(true));

            var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromMilliseconds(150));

            var retry = Policy.Handle<Exception>().RetryAsync(3);
            var policy = Policy.WrapAsync(  retry, timeoutPolicy, chaosPolicy);

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
