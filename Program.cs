using System;
using System.IO;
using System.Linq;

namespace noita_unicorn
{
    class Program
    {

        /// <summary>
        /// The WizardPak file reader
        /// </summary>
        /// <param name="fileOption">An option whose argument is parsed as a FileInfo</param>
        /// <param name="seed">seed to gen</param>
        static void Main(int seed = int.MaxValue - 1)
        {
            //Console.WriteLine($"Hello World! {fileOption?.Name}");
            var zeroResult = new byte[] {
                0xC3, 0xD2, 0xBA, 0xE7,
                0xC3, 0xF3, 0x62, 0x9A,
                0x17, 0x53, 0x71, 0xD6,
                0xB1, 0xF5, 0x05, 0xAA
            };
            //
            var zeroSeeded = NollaPRNG.Seeded16(0);
            if (!Enumerable.SequenceEqual(zeroSeeded, zeroResult)) {
            Console.WriteLine($"{BitConverter.ToString(zeroSeeded)}");
                throw new Exception("prng(0) is incorrect!");
            }
            Console.WriteLine($"{BitConverter.ToString(NollaPRNG.Seeded16(seed, true))}");
            return;
            foreach (var i in new int[]{0,1,7,int.MaxValue-1}) {
                Console.WriteLine($"{BitConverter.ToString(NollaPRNG.Seeded16(i))}");
            }
        }


    }

    class NollaPRNG
    {
        const int SEED_BASE = 23456789 + 11 * 11;
        private double Seed { get; set; }
        private bool Debug { get; }

        NollaPRNG(int seed, int seedBase = SEED_BASE, bool debug = false) {
            Debug = debug;
            if (Debug)
                Console.WriteLine($"{seed} {(uint) seed + seedBase}");
            Seed = (double) (uint) seedBase + seed;
            if (Debug)
                Console.WriteLine($"{Seed}");
            Next();
        }

        double Next() {
            long lseed = (int)Seed * -2092037281L;
            var seedbytes = BitConverter.GetBytes(lseed);
            Array.Reverse(seedbytes);
            if (Debug)
            Console.WriteLine(
                $"{(int) Seed} {lseed} {BitConverter.IsLittleEndian}\n" +
                $"{BitConverter.ToString(seedbytes)}  " +
                $"LO {BitConverter.ToString(BitConverter.GetBytes((uint) lseed))} | " +
                $"HI {BitConverter.ToString(BitConverter.GetBytes((uint) (lseed >> 0x20)))} -> " +
                $"{BitConverter.ToString(BitConverter.GetBytes((int)(lseed >> 0x20) + (int)Seed))}" +
                $" ({(int)(lseed >> 0x20) + (int)Seed})"
            );
            uint edx = (uint) ((int) (lseed >> 0x20) + (int) Seed);
            if (Debug)
                Console.WriteLine($"{(int) Seed * 0x41a7}, {(int) edx >> 0x10}, {((int) edx >> 0x10) * Int32.MaxValue}");
            uint iseed = (uint) ( (int) Seed * 0x41a7 ) - (uint) ( ((int) edx >> 0x10) * Int32.MaxValue );

            Seed = + (int) iseed;
            if (Debug)
                Console.WriteLine($"{Seed}, {BitConverter.ToString(BitConverter.GetBytes(Seed * 4.656612875E-10))}");
            return Seed / Int32.MaxValue;//4.656612875E-10;
        }

        byte[] Next16() {
            var value = new byte[16];
            for (int i = 0; i < 4; i++)
            {
                double valueee = Next() * int.MinValue;
                Console.WriteLine($"{valueee}");
                byte[] bytes = BitConverter.GetBytes((int) valueee);
                Buffer.BlockCopy(
                    bytes, 0,
                    value, i * 4,
                    4
                );
            }
            return value;
        }

        public static byte[] Seeded16(int seed, bool debug = false, int seedBase = 0x0165ec8f) {
            var prng = new NollaPRNG(seed, seedBase, debug);
            return prng.Next16();
        }
    }
}
