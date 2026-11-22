using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Overbryce.Core.IO;

namespace Overbryce.Core.Model;

/// <summary>
/// The geometry of a <see cref="PolyMeshElement"/>: indexed triangles with a normal and texture coordinates per
/// vertex. Immutable: elements, key states and their in-between copies share one instance, and a change makes a
/// new one (<see cref="WithPositions"/>, <see cref="Transform"/>, <see cref="Blend"/>). The arrays must not be
/// modified after construction.
/// </summary>
public sealed class PolyMeshData
{
    private static readonly ConditionalWeakTable<int[], int[]> EdgeCache = new();
    private string? _id;
    private (Vector3 Min, Vector3 Max)? _bounds;
    private bool? _closed;

    /// <param name="positions">x, y, z per vertex.</param>
    /// <param name="normals">x, y, z per vertex (need not be unit length).</param>
    /// <param name="uvs">s, t per vertex, or null for none.</param>
    /// <param name="indices">three vertex indices per triangle, counter-clockwise seen from the front.</param>
    /// <param name="groups">
    /// Optional smoothing groups: vertices with the same group share one normal when the normals are recomputed
    /// (<see cref="WithPositions"/>); a vertex split only for its texture coordinates is in the same group as its
    /// twin, a vertex on a sharp edge in a group of its own. Null: every vertex is its own group.
    /// </param>
    public PolyMeshData(float[] positions, float[] normals, float[]? uvs, int[] indices, int[]? groups = null)
    {
        if (positions.Length % 3 != 0) throw new ArgumentException("Positions must be x, y, z triples.", nameof(positions));
        int n = positions.Length / 3;
        if (normals.Length != positions.Length) throw new ArgumentException("There must be one normal per vertex.", nameof(normals));
        if (uvs != null && uvs.Length != n * 2) throw new ArgumentException("There must be one texture coordinate pair per vertex.", nameof(uvs));
        if (indices.Length % 3 != 0) throw new ArgumentException("Indices must be triples.", nameof(indices));
        if (groups != null && groups.Length != n) throw new ArgumentException("There must be one group per vertex.", nameof(groups));
        foreach (int i in indices)
            if ((uint)i >= (uint)n) throw new ArgumentException($"Vertex index {i} is out of range (0..{n - 1}).", nameof(indices));
        Positions = positions;
        Normals = normals;
        Uvs = uvs;
        Indices = indices;
        Groups = groups;
    }

    public float[] Positions { get; }
    public float[] Normals { get; }
    public float[]? Uvs { get; }
    public int[] Indices { get; }
    public int[]? Groups { get; }

    public int VertexCount => Positions.Length / 3;
    public int TriangleCount => Indices.Length / 3;

    /// <summary>
    /// When this mesh is an in-between of key states (<see cref="Blend"/>): the key-state meshes and their weights
    /// (the positions are exactly their weighted sum). Lets the Blender export turn a morph into shape keys.
    /// </summary>
    public IReadOnlyList<(PolyMeshData Mesh, float Weight)>? BlendedFrom { get; private init; }

    /// <summary>Identity of the content (16 hex digits of a SHA-256): equal meshes have equal ids.</summary>
    public string Id => _id ??= ComputeId();

    /// <summary>Axis-aligned bounds of the vertices.</summary>
    public (Vector3 Min, Vector3 Max) Bounds => _bounds ??= ComputeBounds();

    /// <summary>
    /// Every edge (between vertices at the same places) belongs to exactly two triangles: a closed surface, so
    /// glass refracts where a ray enters and where it leaves.
    /// </summary>
    public bool IsClosed => _closed ??= ComputeClosed();

    /// <summary>Unique edges as pairs of vertex indices (for the wire frame).</summary>
    public int[] Edges => EdgeCache.GetValue(Indices, ComputeEdges);

    // ------------------------------------------------------------------ construction

    /// <summary>A mesh with normals computed from the triangles (smooth within each group).</summary>
    public static PolyMeshData Create(float[] positions, float[]? uvs, int[] indices, int[]? groups = null)
        => new(positions, ComputeNormals(positions, indices, groups), uvs, indices, groups);

    /// <summary>The same topology with other vertex positions; the normals are recomputed from the triangles and groups.</summary>
    public PolyMeshData WithPositions(float[] positions)
    {
        if (positions.Length != Positions.Length) throw new ArgumentException("The vertex count must not change.", nameof(positions));
        return new PolyMeshData(positions, ComputeNormals(positions, Indices, Groups), Uvs, Indices, Groups);
    }

    /// <summary>The mesh transformed by <paramref name="m"/> (a scale, shear or any affine map); a mirroring map keeps the triangles facing out.</summary>
    public PolyMeshData Transform(Matrix4x4 m)
    {
        int n = VertexCount;
        var p = new float[n * 3];
        var nn = new float[n * 3];
        Matrix4x4.Invert(m, out var inv);
        var normalMatrix = Matrix4x4.Transpose(inv);
        for (int i = 0; i < n; i++)
        {
            var v = Vector3.Transform(new Vector3(Positions[i * 3], Positions[i * 3 + 1], Positions[i * 3 + 2]), m);
            var nv = Vector3.TransformNormal(new Vector3(Normals[i * 3], Normals[i * 3 + 1], Normals[i * 3 + 2]), normalMatrix);
            float len = nv.Length();
            if (len > 0 && float.IsFinite(len)) nv /= len;
            p[i * 3] = v.X; p[i * 3 + 1] = v.Y; p[i * 3 + 2] = v.Z;
            nn[i * 3] = nv.X; nn[i * 3 + 1] = nv.Y; nn[i * 3 + 2] = nv.Z;
        }
        var indices = Indices;
        if (m.GetDeterminant() < 0)
        {
            indices = (int[])Indices.Clone();
            for (int t = 0; t < indices.Length; t += 3) (indices[t + 1], indices[t + 2]) = (indices[t + 2], indices[t + 1]);
        }
        return new PolyMeshData(p, nn, Uvs, indices, Groups);
    }

    /// <summary>
    /// The weighted sum of up to four meshes of the same vertex count (an in-between of key states, see
    /// <see cref="SplineWeights.Coefficients"/>). The topology and texture coordinates come from
    /// <paramref name="own"/>; the normals are blended and normalised. Returns <paramref name="own"/> itself when
    /// all the meshes are the same or a vertex count differs (such meshes cannot be morphed).
    /// </summary>
    public static PolyMeshData Blend(PolyMeshData own, ReadOnlySpan<PolyMeshData> meshes, ReadOnlySpan<float> weights)
    {
        // merge equal meshes and expand meshes that are blends themselves
        var parts = new List<(PolyMeshData Mesh, float Weight)>(4);
        void Add(PolyMeshData m, float w)
        {
            if (m.BlendedFrom != null)
            {
                foreach (var (inner, iw) in m.BlendedFrom) Add(inner, w * iw);
                return;
            }
            for (int i = 0; i < parts.Count; i++)
                if (ReferenceEquals(parts[i].Mesh, m)) { parts[i] = (m, parts[i].Weight + w); return; }
            parts.Add((m, w));
        }
        for (int i = 0; i < meshes.Length; i++)
        {
            if (meshes[i].VertexCount != own.VertexCount) return own;
            Add(meshes[i], weights[i]);
        }
        parts.RemoveAll(p => p.Weight == 0);
        if (parts.Count == 0) return own;
        if (parts.Count == 1 && MathF.Abs(parts[0].Weight - 1) < 1e-6f) return parts[0].Mesh;

        int count = own.Positions.Length;
        var pos = new float[count];
        var nrm = new float[count];
        foreach (var (m, w) in parts)
        {
            var mp = m.Positions;
            var mn = m.Normals;
            for (int i = 0; i < count; i++)
            {
                pos[i] += w * mp[i];
                nrm[i] += w * mn[i];
            }
        }
        for (int i = 0; i < count; i += 3)
        {
            float len = MathF.Sqrt(nrm[i] * nrm[i] + nrm[i + 1] * nrm[i + 1] + nrm[i + 2] * nrm[i + 2]);
            if (len > 0) { nrm[i] /= len; nrm[i + 1] /= len; nrm[i + 2] /= len; }
        }
        return new PolyMeshData(pos, nrm, own.Uvs, own.Indices, own.Groups) { BlendedFrom = parts };
    }

    /// <summary>
    /// Area-weighted vertex normals: each triangle adds its (unnormalised) face normal to its corners' groups,
    /// and every vertex gets the normalised sum of its group.
    /// </summary>
    public static float[] ComputeNormals(float[] positions, int[] indices, int[]? groups)
    {
        int n = positions.Length / 3;
        int groupCount = n;
        if (groups != null)
        {
            groupCount = 0;
            foreach (int g in groups) groupCount = Math.Max(groupCount, g + 1);
        }
        var sums = new Vector3[groupCount];
        for (int t = 0; t + 2 < indices.Length; t += 3)
        {
            int a = indices[t], b = indices[t + 1], c = indices[t + 2];
            var pa = new Vector3(positions[a * 3], positions[a * 3 + 1], positions[a * 3 + 2]);
            var pb = new Vector3(positions[b * 3], positions[b * 3 + 1], positions[b * 3 + 2]);
            var pc = new Vector3(positions[c * 3], positions[c * 3 + 1], positions[c * 3 + 2]);
            var face = Vector3.Cross(pb - pa, pc - pa);
            if (!float.IsFinite(face.X + face.Y + face.Z)) continue;
            sums[groups?[a] ?? a] += face;
            sums[groups?[b] ?? b] += face;
            sums[groups?[c] ?? c] += face;
        }
        var normals = new float[n * 3];
        for (int i = 0; i < n; i++)
        {
            var s = sums[groups?[i] ?? i];
            float len = s.Length();
            if (len > 0) s /= len;
            normals[i * 3] = s.X; normals[i * 3 + 1] = s.Y; normals[i * 3 + 2] = s.Z;
        }
        return normals;
    }

    // ------------------------------------------------------------------ derived values

    private string ComputeId()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add<T>(T[]? array) where T : struct
        {
            Span<byte> length = stackalloc byte[4];
            BitConverter.TryWriteBytes(length, array?.Length ?? -1);
            hash.AppendData(length);
            if (array != null) hash.AppendData(MemoryMarshal.AsBytes(array.AsSpan()));
        }
        Add(Positions);
        Add(Normals);
        Add(Uvs);
        Add(Indices);
        Add(Groups);
        return Convert.ToHexString(hash.GetHashAndReset(), 0, 8).ToLowerInvariant();
    }

    private (Vector3, Vector3) ComputeBounds()
    {
        if (VertexCount == 0) return (Vector3.Zero, Vector3.Zero);
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (int i = 0; i < Positions.Length; i += 3)
        {
            var p = new Vector3(Positions[i], Positions[i + 1], Positions[i + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }

    private bool ComputeClosed()
    {
        if (TriangleCount == 0) return false;
        // vertices at the same place are one (split normals and texture seams do not open the surface)
        var (min, max) = Bounds;
        float scale = 1e5f / MathF.Max(1e-12f, (max - min).Length());
        var weld = new Dictionary<(long, long, long), int>();
        var id = new int[VertexCount];
        for (int i = 0; i < VertexCount; i++)
        {
            var key = ((long)MathF.Round(Positions[i * 3] * scale), (long)MathF.Round(Positions[i * 3 + 1] * scale), (long)MathF.Round(Positions[i * 3 + 2] * scale));
            if (!weld.TryGetValue(key, out int w)) weld[key] = w = weld.Count;
            id[i] = w;
        }
        var edges = new Dictionary<(int, int), int>();
        for (int t = 0; t < Indices.Length; t += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = id[Indices[t + k]], b = id[Indices[t + (k + 1) % 3]];
                if (a == b) continue;
                var e = a < b ? (a, b) : (b, a);
                edges[e] = edges.GetValueOrDefault(e) + 1;
            }
        foreach (int c in edges.Values)
            if (c != 2) return false;
        return true;
    }

    private static int[] ComputeEdges(int[] indices)
    {
        var seen = new HashSet<long>();
        var list = new List<int>();
        for (int t = 0; t + 2 < indices.Length; t += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = indices[t + k], b = indices[t + (k + 1) % 3];
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!seen.Add(key)) continue;
                list.Add(a);
                list.Add(b);
            }
        return list.ToArray();
    }
}

/// <summary>
/// A polygon mesh (new in 2.4): triangles with normals and texture coordinates, for shapes the original
/// primitives cannot express, such as the meshes of an imported Blender scene. Like the other shapes it has one
/// material and one texture. Key states with meshes of the same vertex count morph into each other.
/// <para>
/// In the <c>.ani</c> file it is an empty group (so the 1998 program and earlier versions of the port open the file
/// and only miss the mesh); the extension block names the mesh (<c>mesh=id</c>) and the vertices are stored in a
/// binary file next to the animation (see <see cref="MeshStore"/> and docs/ani-format.md).
/// </para>
/// </summary>
public sealed class PolyMeshElement : Element
{
    private string? _missingId;

    public PolyMeshElement(string name = "NovyPolymesh", PolyMeshData? data = null) : base(name)
    {
        Data = data;
    }

    public override ElementType Type => ElementType.PolyMesh;

    /// <summary>The geometry; null when the mesh file was not found (the element is then not drawn but kept).</summary>
    [Browsable(false)]
    public PolyMeshData? Data
    {
        get;
        set
        {
            field = value;
            if (value != null) _missingId = null;
        }
    }

    /// <summary>The id the file refers to: the data's, or the one that could not be found.</summary>
    [Browsable(false)]
    public string? MeshId => Data?.Id ?? _missingId;

    /// <summary>Remembers the id of a mesh that is not (yet) loaded.</summary>
    internal void SetMeshId(string id)
    {
        if (Data == null || Data.Id != id)
        {
            Data = null;
            _missingId = id;
        }
    }

    [Category("8 Shape"), DisplayName("Vertices"), Description("Number of vertices of the polygon mesh.")]
    public int VertexCount => Data?.VertexCount ?? 0;

    [Category("8 Shape"), DisplayName("Triangles"), Description("Number of triangles of the polygon mesh.")]
    public int TriangleCount => Data?.TriangleCount ?? 0;

    public override void Resize(float factor)
    {
        if (factor > 0 && Data != null) Data = Data.Transform(Matrix4x4.CreateScale(factor));
    }

    public override void Interpolate(in SplineWeights w, float time, Element prev, Element next, Element nextNext)
    {
        base.Interpolate(w, time, prev, next, nextNext);
        var own = Data;
        var p = ((PolyMeshElement)prev).Data;
        var n = ((PolyMeshElement)next).Data;
        var nn = ((PolyMeshElement)nextNext).Data;
        if (own == null || p == null || n == null || nn == null) return;
        if (ReferenceEquals(p, own) && ReferenceEquals(n, own) && ReferenceEquals(nn, own)) return;
        var (w1, w2, w3, w4) = w.Coefficients();
        Data = PolyMeshData.Blend(own, [p, own, n, nn], [w1, w2, w3, w4]);
    }

    /// <summary>Not read by tag: the file stores a placeholder group, which the extension block turns into a mesh.</summary>
    internal override void ReadBody(AniTokenReader r) => AniFile.ReadChildren(r, new GroupElement(), AniTags.GroupEnd);

    /// <summary>An empty group with this element's common values (the placeholder older versions see).</summary>
    internal override void Write(AniTokenWriter w)
    {
        WriteCommon(w, AniTags.Group);
        w.Write(AniTags.GroupEnd);
    }
}
