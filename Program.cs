using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Unicorn
{
    class Unicorn
    {
        static readonly byte[] AESKey = NollaPrng.Get16Seeded(0);
        static void Main(string[] args)
        {
            if (args.Length < 1) {
                PrintHelp();
                return;
            }
            if (args.Length > 1 && args[0] == "beta") {
                NollaPrng.BETA_SEED = true;
                args = args.Skip(1).ToArray();
            }
            var command = args[0];
            args = args.Skip(1).ToArray();
            switch (command)
            {
                case "help":
                    PrintHelp();
                    break;
                case "wak":
                    WakMain(args);
                    break;
                case "recipe":
                    MaterialPicker.DoMain(args);
                    break;
                case "rng":
                    NollaPrng.DoMain(args);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown command {args[0]}");
                PrintHelp();
                    break;
            }
        }

        static void PrintHelp(bool commandsOnly = false) {
            if (!commandsOnly) {
                Console.WriteLine("Unicorn - An all-in-one utility for Noita files and things");
            }
            Console.WriteLine("Commands (use beta [command] to use beta compatible prng):");
            Console.WriteLine("wak [file] ([name substring] | #[id] | @pos) - unpack given wak archive, with optional filter, recipe (todo: packing)");
            Console.WriteLine("recipe [seed] - show Lively Concotion and Alchemical Precursor recipes for given seed");
        }

        static void WakMain(string[] args) {
            if (args.Length < 1) {
                Console.Error.WriteLine("File path argument required!");
                return;
            }
            var fileInfo = new FileInfo(args[0]);
            if (!fileInfo.Exists) {
                Console.Error.WriteLine($"File {fileInfo.FullName} does not exist");
                return;
            }

            using (var stream = fileInfo.OpenRead()) {
                var header = DecryptStream(stream, 0x10, 1);
                var numFiles = BitConverter.ToInt32(new ArraySegment<byte>(header, 4, 4));
                var tocSize  = BitConverter.ToInt32(new ArraySegment<byte>(header, 8, 4));
                Console.WriteLine($"WizardPak: {numFiles} files");

                var files = new List<FileDescriptor>();
                var tocBytes = DecryptStream(stream, tocSize - 0x10, int.MaxValue - 1);
                using (var tocStream = new MemoryStream(tocBytes))
                using (var reader = new BinaryReader(tocStream))
                {
                    for (int i = 0; i < numFiles; i++)
                    {
                        var offset = reader.ReadInt32();
                        var length = reader.ReadInt32();
                        var pathLength = reader.ReadInt32();
                        files.Add(new FileDescriptor {
                            Path = Encoding.UTF8.GetString(reader.ReadBytes(pathLength)),
                            Offset = offset,
                            Length = length,
                            Id = i
                        });
                    }
                    files.Sort((a, b) => a.Offset.CompareTo(b.Offset));
                    if (args.Length > 1)
                        files = files.FindAll( (f) =>
                                f.Path.Contains(args[1]) ||
                                (args[1].StartsWith("#") && int.TryParse(args[1].Substring(1), out var id) && f.Id == id) ||
                                (args[1].StartsWith("@") && int.TryParse(args[1].Substring(1), out var pos) && f.Offset >= pos && f.Offset + f.Length < pos)
                            );
                    foreach (var file in files) {
                        Directory.CreateDirectory(Path.GetDirectoryName(file.Path));
                        using (var fileStream = File.OpenWrite(Path.Combine(Directory.GetCurrentDirectory(), file.Path))) {
                            // || (int.TryParse(args[1], out var tmp) && tmp == file.Id)
                            Console.Write($"{file} - #{file.Id}, {file.Length} bytes at {file.Offset}");
                            if (stream.Position != file.Offset) {
                                Console.Write($" (seeking {file.Offset - stream.Position})");
                                stream.Seek(file.Offset, SeekOrigin.Begin);
                            }
                            Console.WriteLine();
                            fileStream.Write(DecryptStream(stream, file.Length, file.Id));
                        }
                    }
                }
            }
        }

        static byte[] DecryptStream(Stream stream, int length, int iv) {
            var plaintext = new byte[length];
            using (var aesAlg = new Aes128CounterMode(NollaPrng.Get16Seeded(iv)))
            {
                // Create a decryptor to perform the stream transform.
                var decryptor = aesAlg.CreateDecryptor(AESKey, null);
                var buffer = new byte[length];
                stream.Read(buffer, 0, length);
                using (MemoryStream limitedStream = new MemoryStream(buffer)) {
                    using (CryptoStream csDecrypt = new CryptoStream(limitedStream, decryptor, CryptoStreamMode.Read, true))
                    {
                        csDecrypt.Read(plaintext, 0, length);
                    }
                }
            }
            return plaintext;
        }
    }

    class FileDescriptor {
        public string Path { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }

        public int Id { get; set; }

        override public string ToString() {
            return Path;
        }
    }

    class MaterialPicker
    {
        public static void DoMain(string[] args) {
            if (args.Length < 1) {
                Console.Error.WriteLine("Seed required!");
                return;
            }
            PickForSeed((uint) long.Parse(args[0]));
            return;
        }
        static Dictionary<string, string> Names = new Dictionary<string, string>{
            ["a"] = "b",
            ["a"] = "b",
            ["a"] = "b",
            ["a"] = "b",
        };
        static List<string> LIQUIDS = new List<string>{
            "water",
            "water_ice",
            "water_swamp",
            "oil",
            "alcohol",
            "swamp",
            "mud",
            "blood",
            "blood_fungi",
            "blood_worm",
            "radioactive_liquid", // aka "toxic sludge"
            "cement",
            "acid",
            "lava",
            "urine",
            "poison",
            "magic_liquid_teleportation",
            "magic_liquid_polymorph",
            "magic_liquid_random_polymorph",
            "magic_liquid_berserk",
            "magic_liquid_charm",
            "magic_liquid_invisibility"
        };

        static List<string> ALCHEMY = new List<string>{
            "sand",
            "bone",
            "soil",
            "honey",
            "slime",
            "snow",
            "rotten_meat",
            "wax",
            "gold",
            "silver",
            "copper",
            "brass",
            "diamond",
            "coal",
            "gunpowder",
            "gunpowder_explosive",
            "grass",
            "fungi"
        };
        NollaPrng PRNG;
        List<string> Materials = new List<string>();
        public MaterialPicker(NollaPrng prng, uint worldSeed) {
            PRNG = prng;
            PickMaterials(LIQUIDS, 3);
            PickMaterials(ALCHEMY, 1);
            ShuffleList(worldSeed);
            PRNG.Next();
            PRNG.Next();
        }

        void PickMaterials(List<string> source, int count) {
            int counter = 0;
            int failed = 0;
            while (counter < count && failed < 99999) {
                var picked = source[(int) (PRNG.Next() * source.Count)];
                if (!Materials.Any(v => v == picked)) {
                    Materials = Materials.Append(picked).ToList();
                    counter++;
                } else {
                    failed++;
                }
            }
            return;
        }

        void ShuffleList(uint worldSeed) {
            var prng = new NollaPrng((worldSeed >> 1) + 12534);
            // Toxic sludge, blood, and soil for first
            for (int i = Materials.Count - 1; i >= 0; i--) {
                int rand = (int) (prng.Next() * (i + 1));
                var tmp = Materials[i];
                Materials[i] = Materials[rand];
                Materials[rand] = tmp;
            }
        }

        override public string ToString() {
            return $"{Materials[0]}, {Materials[1]}, {Materials[2]}";
        }

        public static void PickForSeed(uint worldSeed) {
            var prng = new NollaPrng(worldSeed * 0.17127000 + 1323.59030000);
            // Preheat random!
            for (int i = 0; i < 5; i++)
                prng.Next();

            Console.WriteLine($"Seed: {worldSeed}");
            Console.WriteLine($"Lively Concotion: {new MaterialPicker(prng, worldSeed).ToString()}");
            Console.WriteLine($"Alchemical Precursor: {new MaterialPicker(prng, worldSeed).ToString()}");
            Console.WriteLine("");
        }
    }

    class NollaPrng
    {
        public static bool BETA_SEED = false;
        const int SEED_BASE = 23456789 + 1 + 11 * 11;
        public double Seed { get; private set; }

        public NollaPrng(int seed, int seedBase = SEED_BASE) {
            Seed = (double) (uint) seedBase + seed;
            if (BETA_SEED && Seed >= 2147483647.0) {
                Seed *= 0.5;
            }
            Next();
        }

        public NollaPrng(double seed) {
            Seed = seed;
            Next();
        }

        public double Next() {
            Seed = ((int) Seed) * 16807 + ((int) Seed) / 127773 * -int.MaxValue;
            //it's abs+1, because M A G I C, damn it
            if (Seed < 0) Seed += int.MaxValue;
            return Seed / int.MaxValue;
        }

        public byte[] Next16() {
            var value = new byte[16];
            for (int i = 0; i < 4; i++) {
                byte[] bytes = BitConverter.GetBytes((int) (Next() * int.MinValue));
                Buffer.BlockCopy(
                    bytes, 0,
                    value, i * 4,
                    4
                );
            }
            return value;
        }

        public static byte[] Get16Seeded(int seed, int seedBase = SEED_BASE) {
            var prng = new NollaPrng(seed, seedBase);
            return prng.Next16();
        }

        public static void DoMain(string[] args) {
            foreach (var arg in args) {
                if (uint.TryParse(arg, out var value)) {
                    var prng = new NollaPrng(value);
                    Console.WriteLine($"Next state for seed {value}: {prng.Seed}, error on float cast {prng.Seed - ((double) (float) (prng.Seed * ((double)1/int.MaxValue))) * (0x8000_0000)}");
                    Console.WriteLine($"IV bytes for given seed are: {BitConverter.ToString(Get16Seeded((int) value))}");
                }
            }
        }
    }
}
