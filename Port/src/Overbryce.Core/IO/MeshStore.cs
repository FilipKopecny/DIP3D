using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Overbryce.Core.Model;

namespace Overbryce.Core.IO;

/// <summary>
/// The binary file next to an animation that holds the vertices of its polygon meshes (<c>name.meshes</c>, new in
/// 2.4). Text would make big meshes slow to read (Lumina's 37 MB of text already takes a while), so the meshes are
/// stored as raw little-endian arrays, compressed with Deflate. Arrays with the same content are stored once, so
/// key states that share a mesh, or morph targets that share their triangles and texture coordinates, cost little.
/// <code>
/// "OBMESH" version(byte)            then, Deflate compressed:
/// int32 arrayCount                  per array: kind(byte: 0 float32, 1 int32), int32 length, the values
/// int32 meshCount                   per mesh:  id (string), positions, normals, uvs, indices, groups (array numbers, -1 none)
/// </code>
/// </summary>
public static class MeshStore
{
    public const string Extension = ".meshes";
    private static readonly byte[] Magic = "OBMESH"u8.ToArray();
    private const byte Version = 1;

    /// <summary>Writes the meshes (each distinct one once) to <paramref name="path"/>.</summary>
    public static void Write(string path, IEnumerable<PolyMeshData> meshes)
    {
        var distinct = new Dictionary<string, PolyMeshData>(StringComparer.Ordinal);
        foreach (var m in meshes) distinct.TryAdd(m.Id, m);

        var arrays = new List<Array>();
        var byContent = new Dictionary<string, int>(StringComparer.Ordinal);
        var byReference = new Dictionary<Array, int>(ReferenceEqualityComparer.Instance);
        int Index(Array? a)
        {
            if (a == null) return -1;
            if (byReference.TryGetValue(a, out int i)) return i;
            string key = (a is int[] ? "i" : "f") + Hash(a);
            if (!byContent.TryGetValue(key, out i))
            {
                i = arrays.Count;
                arrays.Add(a);
                byContent[key] = i;
            }
            byReference[a] = i;
            return i;
        }
        var records = distinct.Values.Select(m => (m.Id, Index(m.Positions), Index(m.Normals), Index(m.Uvs), Index(m.Indices), Index(m.Groups))).ToList();

        string temp = path + ".tmp";
        using (var file = File.Create(temp))
        {
            file.Write(Magic);
            file.WriteByte(Version);
            using var deflate = new DeflateStream(file, CompressionLevel.Fastest);
            using var w = new BinaryWriter(deflate, Encoding.UTF8);
            w.Write(arrays.Count);
            foreach (var a in arrays)
            {
                w.Write((byte)(a is int[] ? 1 : 0));
                w.Write(a.Length);
                w.Write(a is int[] ints ? MemoryMarshal.AsBytes(ints.AsSpan()) : MemoryMarshal.AsBytes(((float[])a).AsSpan()));
            }
            w.Write(records.Count);
            foreach (var (id, p, n, uv, ix, g) in records)
            {
                w.Write(id);
                w.Write(p); w.Write(n); w.Write(uv); w.Write(ix); w.Write(g);
            }
        }
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// Reads the meshes of <paramref name="path"/> by id. A damaged file yields the meshes read before the damage;
    /// a mesh whose arrays do not fit together is skipped.
    /// </summary>
    public static Dictionary<string, PolyMeshData> Read(string path)
    {
        var result = new Dictionary<string, PolyMeshData>(StringComparer.Ordinal);
        using var file = File.OpenRead(path);
        var head = new byte[Magic.Length + 1];
        if (file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) != head.Length || !head.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new AniFormatException($"{Path.GetFileName(path)} is not an Overbryce mesh file.");
        if (head[^1] > Version)
            throw new AniFormatException($"{Path.GetFileName(path)} was written by a newer version (mesh format {head[^1]}).");
        try
        {
            using var deflate = new DeflateStream(file, CompressionMode.Decompress);
            using var r = new BinaryReader(deflate, Encoding.UTF8);
            int count = r.ReadInt32();
            if (count < 0) throw new InvalidDataException("Negative array count.");
            var arrays = new Array[count];
            for (int i = 0; i < count; i++)
            {
                byte kind = r.ReadByte();
                int length = r.ReadInt32();
                if (length < 0 || kind > 1) throw new InvalidDataException("Bad array header.");
                Array a = kind == 1 ? new int[length] : new float[length];
                var bytes = kind == 1 ? MemoryMarshal.AsBytes(((int[])a).AsSpan()) : MemoryMarshal.AsBytes(((float[])a).AsSpan());
                r.BaseStream.ReadExactly(bytes);
                arrays[i] = a;
            }
            int meshes = r.ReadInt32();
            for (int m = 0; m < meshes; m++)
            {
                string id = r.ReadString();
                int p = r.ReadInt32(), n = r.ReadInt32(), uv = r.ReadInt32(), ix = r.ReadInt32(), g = r.ReadInt32();
                T? Get<T>(int i) where T : class => i >= 0 && i < arrays.Length ? arrays[i] as T : null;
                try
                {
                    if (Get<float[]>(p) is { } pos && Get<float[]>(n) is { } nrm && Get<int[]>(ix) is { } idx)
                        result[id] = new PolyMeshData(pos, nrm, Get<float[]>(uv), idx, Get<int[]>(g));
                }
                catch (ArgumentException)
                {
                    // arrays that do not fit together: the mesh is lost, the others are kept
                }
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or IOException)
        {
            // damaged or truncated: keep what was read
        }
        return result;
    }

    private static string Hash(Array a)
    {
        var bytes = a is int[] ints ? MemoryMarshal.AsBytes(ints.AsSpan()) : MemoryMarshal.AsBytes(((float[])a).AsSpan());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
