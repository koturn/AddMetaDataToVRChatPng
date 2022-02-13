using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;


namespace AddMetaDataToVRChatPng
{
    static class Program
    {
        /// <summary>
        /// Chunk type string of IDAT chunk.
        /// </summary>
        private const string ChunkTypeIdat = "IDAT";
        /// <summary>
        /// Chunk type string of IEND chunk.
        /// </summary>
        private const string ChunkTypeIend = "IEND";
        /// <summary>
        /// Chunk type string of tEXt chunk.
        /// </summary>
        private const string ChunkNameText = "tEXt";
        /// <summary>
        /// Chunk type string of tIME chunk.
        /// </summary>
        private const string ChunkNameTime = "tIME";
        /// <summary>
        /// Predefined keyword of tEXt chunk for time of original image creation.
        /// </summary>
        private const string TextChunkKeyCreationTime = "Creation Time";

        /// <summary>
        /// Signature of PNG file.
        /// </summary>
        private static readonly byte[] PngSignature;


        /// <summary>
        /// Setup DLL search path.
        /// </summary>
        static Program()
        {
            PngSignature = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0d, 0x0a, 0x1a, 0x0a };
        }


        static void Main(string[] args)
        {
            var regex = new Regex(@"^VRChat_\d+x\d+_(\d+)-(\d+)-(\d+)_(\d+)-(\d+)-(\d+)\.(\d+)\.png$", RegexOptions.Compiled);
            var pmo = new PngModifyOptions("yyyy:MM:dd HH:mm:ss.fff", true);
            foreach (var srcFilePath in Directory.EnumerateFiles(args[0]))
            {
                try
                {
                    var dstFilePath = Path.Combine(
                        Path.GetDirectoryName(srcFilePath) ?? "",
                        Path.GetFileNameWithoutExtension(srcFilePath) + ".tmp" + Path.GetExtension(srcFilePath));

                    var fileName = Path.GetFileName(srcFilePath);
                    var match = regex.Match(fileName);
                    var groups = match.Groups;
                    if (!match.Success || groups.Count < 8)
                    {
                        continue;
                    }

                    Console.WriteLine("Modify {0} ...", srcFilePath);

                    var dt = new DateTime(
                        int.Parse(groups[1].Value),
                        int.Parse(groups[2].Value),
                        int.Parse(groups[3].Value),
                        int.Parse(groups[4].Value),
                        int.Parse(groups[5].Value),
                        int.Parse(groups[6].Value),
                        int.Parse(groups[7].Value));

                    using (var ifs = new FileStream(srcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var ofs = new FileStream(dstFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        AddAdditionalChunks(ifs, ofs, pmo, dt);
                    }
                    new FileInfo(dstFilePath).LastWriteTime = dt;

                    File.Move(dstFilePath, srcFilePath, true);

                    Console.WriteLine("Modify {0} done", srcFilePath);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// Split IDAT chunk or add tEXt chunk whose key is "Creation Time" or add tIME chunk.
        /// </summary>
        /// <param name="pngData">Source PNG data.</param>
        /// <param name="execOptions">Options for execution.</param>
        /// <param name="createTime">Ceation time used for "Creation Time" of tEXt chunk and value of tIME chunk.</param>
        /// <returns>Modified PNG data.</returns>
        private static Span<byte> AddAdditionalChunks(Span<byte> pngData, PngModifyOptions execOptions, in DateTime createTime)
        {
            using var oms = new MemoryStream(pngData.Length
                + (string.IsNullOrEmpty(execOptions.TextCreationTimeFormat) ? 0 : 256)
                + (execOptions.IsAddTimeChunk ? 19 : 0));
            unsafe
            {
                fixed (byte* p = pngData)
                {
                    using var ims = new UnmanagedMemoryStream(p, pngData.Length);
                    AddAdditionalChunks(ims, oms, execOptions, createTime);
                }
            }
            return oms.GetBuffer().AsSpan(0, (int)oms.Length);
        }

        /// <summary>
        /// Split IDAT chunk into the specified size.
        /// </summary>
        /// <param name="srcPngStream">Source PNG data stream.</param>
        /// <param name="dstPngStream">Destination data stream.</param>
        /// <param name="execOptions">Options for execution.</param>
        /// <param name="createTime">Ceation time used for "Creation Time" of tEXt chunk and value of tIME chunk.</param>
        private static void AddAdditionalChunks(Stream srcPngStream, Stream dstPngStream, PngModifyOptions execOptions, in DateTime createTime)
        {
            Span<byte> pngSignature = stackalloc byte[PngSignature.Length];
            if (srcPngStream.Read(pngSignature) < pngSignature.Length)
            {
                throw new Exception("Source PNG file data is too small.");
            }

            if (!HasPngSignature(pngSignature))
            {
                var sb = new StringBuilder();
                foreach (var b in pngSignature)
                {
                    sb.Append($" {b:2X}");
                }
                throw new InvalidDataException($"Invalid PNG signature:{sb}");
            }

            // Write PNG Signature
            dstPngStream.Write(PngSignature, 0, PngSignature.Length);

            using var bw = new BinaryWriter(dstPngStream, Encoding.ASCII, true);
            PngChunk pngChunk;
            var hasTimeChunk = false;
            var hasTextCreationTime = false;
            do
            {
                pngChunk = PngChunk.ReadOneChunk(srcPngStream);

                if (pngChunk.Type == ChunkNameText) {
                    // May be thrown ArgumentOutOfRangeException if null character is not found.
                    var key = Encoding.ASCII.GetString(
                        pngChunk.Data,
                        0,
                        Array.IndexOf(pngChunk.Data, (byte)0, 0, pngChunk.Data.Length));
                    if (key == TextChunkKeyCreationTime)
                    {
                        hasTextCreationTime = true;
                    }
                } else if (pngChunk.Type == ChunkNameTime) {
                    hasTimeChunk = true;
                } else if (pngChunk.Type == ChunkTypeIend) {
                    // Insert tEXt and tIME chunks before IEND.
                    if (!hasTextCreationTime && !string.IsNullOrEmpty(execOptions.TextCreationTimeFormat))
                    {
                        PngChunk.WriteTextChunk(
                            dstPngStream,
                            TextChunkKeyCreationTime,
                            createTime.ToString(execOptions.TextCreationTimeFormat));
                    }
                    if (!hasTimeChunk && execOptions.IsAddTimeChunk)
                    {
                        PngChunk.WriteTimeChunk(dstPngStream, createTime);
                    }
                }
                pngChunk.WriteTo(dstPngStream);
            } while (pngChunk.Type != ChunkTypeIend);
        }

        /// <summary>
        /// Identify the specified binary data has a PNG signature or not.
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a PNG signature, otherwise false.</returns>
        private static bool HasPngSignature(byte[] data)
        {
            return HasPngSignature(data.AsSpan());
        }

        /// <summary>
        /// Identify the specified binary data has a PNG signature or not.
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a PNG signature, otherwise false.</returns>
        private static bool HasPngSignature(Span<byte> data)
        {
            if (data.Length < PngSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (data[i] != PngSignature[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
