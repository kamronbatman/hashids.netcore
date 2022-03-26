using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace Hashids.net.benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<HashBenchmark>();
        }

        [SimpleJob(RuntimeMoniker.Net60)]
        [MemoryDiagnoser]
        public class HashBenchmark
        {
            private HashidsNet.Hashids _hashids;
            private int[] _ints;
            private long[] _longs;
            private const string _hex = "507f1f77bcf86cd799439011";

            [GlobalSetup]
            public void Setup()
            {
                _ints = new[] { 12345, 1234567890, int.MaxValue };
                _longs = new[] { 12345, 1234567890123456789, long.MaxValue };
                _hashids = new HashidsNet.Hashids();
                _longs.AsSpan().ToHexString(); // Primes the hex lookup table
            }

            [Benchmark]
            public void RoundtripInts()
            {
                var encodedValue = _hashids.Encode(_ints);
                var decodedValue = _hashids.Decode(encodedValue);
            }

            [Benchmark]
            public void RoundtripLongs()
            {
                var encodedValue = _hashids.EncodeLong(_longs);
                var decodedValue = _hashids.DecodeLong(encodedValue);
            }

            [Benchmark]
            public void RoundtripHex()
            {
                var encodedValue = _hashids.EncodeHex(_hex);
                var decodedValue = _hashids.DecodeHex(encodedValue);
            }
        }
    }
}
