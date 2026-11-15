using Godot;
using System.Collections.Generic;

public partial class TerrainWorld : Node3D
{
    [Export] public int Seed = 12345;
    [Export] public int ChunkSize = 16;
    [Export] public int RenderDistance = 2;
    [Export] public float CellSize = 2f;
    [Export] public float HeightScale = 20f;
    [Export] public float NoiseFrequency = 0.02f;
    [Export] public Node3D Player;
    [Export] public bool DebugGround = true;

    private FastNoiseLite _noise;
    private StandardMaterial3D _material;
    private readonly Dictionary<Vector2I, Node3D> _chunks = new();
    private Vector2I _lastChunk = new(int.MaxValue, int.MaxValue);
    private bool _groundChecked;

    public override void _Ready()
    {
        if (ChunkSize < 1 || CellSize <= 0f)
        {
            GD.PushError("TerrainWorld: ChunkSize and CellSize must be positive.");
            SetPhysicsProcess(false);
            return;
        }

        _noise = new FastNoiseLite
        {
            Seed = Seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = NoiseFrequency
        };

        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.65f, 0.3f),
            Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        UpdateChunks();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GetPlayerChunk() != _lastChunk)
            UpdateChunks();
        if (DebugGround && !_groundChecked && Player != null)
        {
            _groundChecked = true;
            TestGroundUnderPlayer();
        }
    }

    private Vector2I GetPlayerChunk()
    {
        Vector3 pos = Player != null
            ? ToLocal(Player.GlobalPosition)
            : Vector3.Zero;

        float width = ChunkSize * CellSize;
        return new Vector2I(
            Mathf.FloorToInt(pos.X / width),
            Mathf.FloorToInt(pos.Z / width)
        );
    }

    private void UpdateChunks()
    {
        Vector2I center = GetPlayerChunk();
        _lastChunk = center;
        HashSet<Vector2I> needed = new();

        for (int x = -RenderDistance; x <= RenderDistance; x++)
        {
            for (int z = -RenderDistance; z <= RenderDistance; z++)
            {
                Vector2I coord = center + new Vector2I(x, z);
                needed.Add(coord);

                if (!_chunks.ContainsKey(coord))
                    GenerateChunk(coord);
            }
        }

        foreach (Vector2I coord in new List<Vector2I>(_chunks.Keys))
        {
            if (needed.Contains(coord))
                continue;

            _chunks[coord].QueueFree();
            _chunks.Remove(coord);
        }
    }

    private void GenerateChunk(Vector2I coord)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int z = 0; z < ChunkSize; z++)
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                Vector3 a = GetVertex(coord, x, z);
                Vector3 b = GetVertex(coord, x + 1, z);
                Vector3 c = GetVertex(coord, x, z + 1);
                Vector3 d = GetVertex(coord, x + 1, z + 1);

                AddTriangle(st, a, c, b);
                AddTriangle(st, b, c, d);
            }
        }

        ArrayMesh mesh = st.Commit();
        var chunk = new Node3D
        {
            Name = $"Chunk_{coord.X}_{coord.Y}",
            Position = new Vector3(
                coord.X * ChunkSize * CellSize,
                0,
                coord.Y * ChunkSize * CellSize
            )
        };
        var instance = new MeshInstance3D
        {
            Name = "TerrainMesh",
            Mesh = mesh,
            MaterialOverride = _material
        };

        var shape = mesh.CreateTrimeshShape();
        shape.BackfaceCollision = true;

        var body = new StaticBody3D
        {
            Name = "TerrainBody",
            CollisionLayer = 1,
            CollisionMask = 1
        };
        var collision = new CollisionShape3D
        {
            Name = "TerrainCollision",
            Shape = shape
        };


        body.AddChild(collision);
        chunk.AddChild(instance);
        chunk.AddChild(body);
        AddChild(chunk);
        _chunks.Add(coord, chunk);
    }

    private Vector3 GetVertex(Vector2I coord, int x, int z)
    {
        int worldX = coord.X * ChunkSize + x;
        int worldZ = coord.Y * ChunkSize + z;
        float y = _noise.GetNoise2D(worldX, worldZ) * HeightScale;
        return new Vector3(x * CellSize, y, z * CellSize);
    }

    private static void AddTriangle(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = (b - a).Cross(c - a).Normalized();
        st.SetNormal(normal);
        st.AddVertex(a);
        st.SetNormal(normal);
        st.AddVertex(b);
        st.SetNormal(normal);
        st.AddVertex(c);
    }

    private void TestGroundUnderPlayer()
    {
        Vector3 p = ToLocal(Player.GlobalPosition);
        Vector3 from = ToGlobal(new Vector3(p.X, HeightScale + 50f, p.Z));
        Vector3 to = ToGlobal(new Vector3(p.X, -HeightScale - 50f, p.Z));

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1;

        // Do not let the car's own collision obscure the ground result.
        if (Player is CollisionObject3D physicsPlayer)
        {
            query.Exclude = new Godot.Collections.Array<Rid>
            {
                physicsPlayer.GetRid()
            };
        }

        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0)
        {
            GD.PrintErr("TERRAIN UNDER PLAYER: NO HIT");
            return;
        }       
        Vector3 hit = (Vector3)result["position"];
        GD.Print($"TERRAIN UNDER PLAYER: {result["collider"]}");
        GD.Print($"Player Y={Player.GlobalPosition.Y:F2}, ground Y={hit.Y:F2}");
    }
}
