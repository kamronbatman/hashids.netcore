using System;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace HashidsNetCore.Benchmarks
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
            private HashidsNetCore.Hashids _hashids = new();
            private int[] _ints = { 12345, 1234567890, int.MaxValue };
            private long[] _longs = { 12345, 1234567890123456789, long.MaxValue };
            private const string _hex = "507f1f77bcf86cd799439011";

            [GlobalSetup]
            public void Setup()
            {
                _longs.AsSpan().ToHexString(); // Primes the hex lookup table
                // Prime the array pool
                for (var i = 0; i < 12; i++)
                {
                    var arr1 = ArrayPool<char>.Shared.Rent(1 << i);
                    ArrayPool<char>.Shared.Return(arr1);
                }
            }

            [Benchmark]
            public void RoundtripSingleInt()
            {
                var encodedValue = _hashids.EncodeLong(1234567890);
                var decodedValue = _hashids.DecodeSingleLong(encodedValue);
            }

            [Benchmark]
            public void RoundtripInts()
            {
                var encodedValue = _hashids.Encode(_ints);
                var decodedValue = _hashids.Decode(encodedValue);
            }

            [Benchmark]
            public void RoundtripSingleLong()
            {
                var encodedValue = _hashids.EncodeLong(1234567890123456789);
                var decodedValue = _hashids.DecodeSingleLong(encodedValue);
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
