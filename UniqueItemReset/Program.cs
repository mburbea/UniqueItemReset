using Force.Crc32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace UniqueItemReset
{
    static class Program
    {
        static T Read<T>(this Span<byte> span, int offset) where T:struct => MemoryMarshal.Read<T>(span.Slice(offset));

        static void Write<T>(this Span<byte> span, int offset, T value) where T : struct => MemoryMarshal.Write<T>(span.Slice(offset), ref value);

        static int Main(string[] args)
        {
            var types = File.ReadLines("types.txt").Skip(1).Select(x => int.Parse(x, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToHashSet();
            var saveData = File.ReadAllBytes(args[0]);
            var data = saveData.AsSpan();
            if(BitConverter.ToInt32(saveData, 8) != 0)
            {
                Console.WriteLine("ERROR: this program is only for the remaster");
                return -1;
            }
            if(!Path.GetFileNameWithoutExtension(args[0]).StartsWith( "svd_fmt_5"))
            {
                Console.WriteLine("ERROR: this program only supports user saves");
                return -1;
            }
            if(Encoding.UTF8.GetString(data.Slice(6148, 4).ToArray()) == "zlib")
            {
                Console.WriteLine("ERROR: save file is compressed.");
                return -1;
            }
            var typeOffset = data.IndexOf(new byte[] { 0x1D, 0x0D, 0x00, 0x00, 0x94, 0x76, 0x03, 00 });
            var count = data.Read<int>(typeOffset);
            var flagOffset = typeOffset + (2 + count) * 4;
            typeOffset += 4;
            var flips = 0;
            for(int i = 0; i < count; i++, flagOffset+=4, typeOffset+=4)
            {
                var flag = data.Read<int>(flagOffset);
                var simtype = data.Read<int>(typeOffset);
                if(flag == 0 && types.Contains(simtype))
                {
                    data.Write(flagOffset, 1);
                    flips++;
                }
            }
            var backup = $"{args[0]}.bak";
            Console.WriteLine($"Created a backup:{backup}");
            File.Copy(args[0], backup, true);
            Console.WriteLine($"Restored {flips} items back to droppable.");
            var fileCrc = Crc32Algorithm.Compute(saveData, 8, saveData.Length - 8);
            var headerCrc = Crc32Algorithm.Compute(saveData, 8, 6144 - 8);
            data.Write(0, fileCrc);
            data.Write(4, headerCrc);
            File.WriteAllBytes(args[0], saveData);
            return 0;
        }
    }
}
