using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Unicorn
{
    class Program
    {
        static readonly byte[] AESKey = NollaPRNG.Get16Seeded(0);
        static void Main(string[] args)
        {
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
                Console.WriteLine(BitConverter.ToString(new ArraySegment<byte>(tocBytes, 0, 0x10).ToArray()));
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
                        //Console.WriteLine(files[files.Count - 1]);
                    }
                    files.Sort((a, b) => a.Offset.CompareTo(b.Offset));
                    if (args.Length > 1)
                        files = files.FindAll((f) => f.Path.Contains(args[1]) || (args[1].StartsWith("#") && int.TryParse(args[1].Substring(1), out var tmp) && f.Id == tmp));
                    foreach (var file in files) {
                        Process currentProcess = Process.GetCurrentProcess();
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
            using (var aesAlg = new Aes128CounterMode(NollaPRNG.Get16Seeded(iv)))
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

    class NollaPRNG
    {
        const int SEED_BASE = 23456789 + 1 + 11 * 11;
        private double Seed { get; set; }

        NollaPRNG(int seed, int seedBase = SEED_BASE) {
            Seed = (double) (uint) seedBase + seed;
            Next();
        }

        double Next() {
            // There's definitely a better way to write this, but I can't find it
            // Would love some help
            long lseed = ((int)Seed * -2092037281L);
            var seedbytes = BitConverter.GetBytes(lseed);
            Array.Reverse(seedbytes);
            Console.WriteLine(
                $"{(int) Seed} {lseed} {BitConverter.IsLittleEndian}\n" +
                $"{BitConverter.ToString(seedbytes)}  " +
                $"LO {BitConverter.ToString(BitConverter.GetBytes((uint) lseed))} | " +
                $"HI {BitConverter.ToString(BitConverter.GetBytes((uint) (lseed >> 0x20)))} -> " +
                $"{BitConverter.ToString(BitConverter.GetBytes((int)(lseed >> 0x20) + (int)Seed))}" +
                $" ({(int)(lseed >> 0x20) + (int)Seed})"
            );
            int temp = ((int) ((int)Seed * -2092037281L >> 0x20) + (int) Seed) >> 0x10;
            Console.WriteLine($"{(int) Seed * 0x41a7}, {temp}, {(temp) * Int32.MaxValue}");

            Seed = ((int) Seed * 0x41a7 ) - (temp * int.MaxValue + (temp < 0 ? 1 : 0));
            //it's abs+1, because M A G I C, damn it
            if (Seed < 0) Seed += int.MaxValue;
            //Console.WriteLine($"{Seed}, {Seed / int.MaxValue == Seed * 4.6566128750000002e-10}, {BitConverter.ToString(BitConverter.GetBytes((int) -Seed))}");
            return Seed / int.MaxValue;
        }

        byte[] Next16() {
            var value = new byte[16];
            for (int i = 0; i < 4; i++) {
                double valueee = Next() * int.MinValue;
                Console.WriteLine($"{valueee}\n");
                byte[] bytes = BitConverter.GetBytes((int) valueee);
                Buffer.BlockCopy(
                    bytes, 0,
                    value, i * 4,
                    4
                );
            }
            Console.WriteLine($"{BitConverter.ToString(value)}");
            return value;
        }

        public static byte[] Get16Seeded(int seed, int seedBase = SEED_BASE) {
            var prng = new NollaPRNG(seed, seedBase);
            return prng.Next16();
        }
    }
}
