using Flecs.NET.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static Flecs.NET.Bindings.Native;

public struct Game
{
    public Entity Window;
    public Entity Level;

    public Position center;
    public float size;
}

public class grid<T>
{
    private readonly int _width;
    private readonly int _height;
    List<T> _values;

    public grid(int width, int height)
    {
        _width = width;
        _height = height;
        _values = new List<T>(_width * _height);

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                _values.Add(default);
            }
        }
    }

    public void set(int x, int y, T value)
    {
        _values[y * _width + x] = value;
    }

    public T this[int x, int y] => _values[y * _width + x];
}

public class prefabs
{
    public struct Tree
    {
        public struct Trunk { }
        public struct Canopy { }
    }

    public struct Path { };
    public struct Tile { };
    public struct Enemy { };

    public class materials
    {
        public struct Metal { };
        public struct CannonHead { };
    }

    public struct Turret
    {
        public struct Base { };
        public struct Head { };
    };

    public struct Cannon
    {
        public struct Head
        {
            public struct BarrelLeft { }
            public struct BarrelRight { }
        }

        public struct Barrel { }
    }
}


// scopes

public struct Level
{
    public Level(grid<bool> arg_map, Position2 spawn)
    {
        map = arg_map;
        spawn_point = spawn;
    }

    public grid<bool> map;
    public Position2 spawn_point;
}

public struct Turrets { }

public class Main : MonoBehaviour
{
    public int _port = 27750;

    const int TileCountX = 10;
    const int TileCountZ = 10;
    const float TileSize = 3.0f;
    const float TileSpacing = 0;
    const float TileHeight = 0.5f;
    const float PathHeight = 0.1f;
    const float EnemySize = 0.7f;
    const float EnemySpeed = 4.0f;
    const float EnemySpawnInterval = 0.2f;

    const float TurretRotateSpeed = 4.0f;
    const float TurretFireInterval = 0.12f;
    const float TurretRange = 5.0f;
    const float TurretCannonOffset = 0.2f;
    const float TurretCannonLength = 0.6f;

    World ecs;
    private Routine _spawnEnemy;
    private Routine _moveEnemy;
    private Mesh _cubeMesh;


    struct RenderCommand
    {
        public Mesh Mesh;
        public Matrix4x4 Transform;
        public Material Material;
        public MaterialPropertyBlock Properties;
    }

    List<RenderCommand> renderCommands = new List<RenderCommand>();

    // Direction vector. During pathfinding enemies will cycle through this vector
    // to find the next direction to turn to.
    static readonly Position2[] dir = {
        new (-1, 0),
        new (0, -1),
        new (1, 0),
        new (0, 1)};


    void Start()
    {
        ecs = World.Create();

        ecs.Set(new EcsRest());


        ecs.Set<Game>(new Game());

        init_game();
        init_prefabs();
        init_level();
        init_systems();

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.SetActive(false);
        _cubeMesh = go.GetComponent<MeshFilter>().mesh;
        Destroy(go);




        _renderSystem = ecs.Routine<GlobalPosition, Box, Color>()
            .Each((Entity e, ref GlobalPosition p, ref Box b, ref Color c /*, ref Position pp*/) =>
        {
            if (!_renderParams.TryGetValue(c, out var rp))
            {
                rp = new MaterialPropertyBlock();
                rp.SetColor("_Color", c);
                _renderParams.Add(c, rp);
            }

            var mtx = Matrix4x4.Translate(p.AsPrimitive()) * Matrix4x4.Scale(new(b.X, b.Y, b.Z));

            renderCommands.Add(new RenderCommand
            {
                Mesh = _cubeMesh,
                Transform = mtx,
                Material = _baseMaterial,
                Properties = rp
            });
        });
    }

    public Material _baseMaterial;

    private Dictionary<Color, MaterialPropertyBlock> _renderParams = new();
    private Routine _transformSystem;
    private Routine _renderSystem;

    void Update()
    {
        renderCommands.Clear();

        ecs.Progress(Time.deltaTime);

        foreach (var cmd in renderCommands)
        {
            Graphics.DrawMesh(cmd.Mesh, cmd.Transform, cmd.Material, 0, null, 0, cmd.Properties);
        }
    }

    void init_systems()
    {
        ecs.Routine<Position, GlobalPosition, GlobalPosition>()
            .TermAt(2).Optional()
            .TermAt(3).Parent().Cascade().Optional()
            .Each((Entity e, ref Position p, ref GlobalPosition global, ref GlobalPosition parentGlobal) =>
            {

                var newGlobal = new GlobalPosition(new Vector3(p.X, p.Y, p.Z) +
                    (Unsafe.IsNullRef(ref parentGlobal)
                    ? Vector3.zero
                    : parentGlobal.AsPrimitive()));

                if (Unsafe.IsNullRef(ref global))
                {
                    e.Set(newGlobal);
                }
                else
                {
                    global = newGlobal;
                }
            });

        _spawnEnemy = ecs.Routine<Game>().Each((Iter it, int i, ref Game g) =>
        {
            var game = ecs.Get<Game>();
            var lvl = game.Level.Get<Level>();

            ecs.Entity().IsA<prefabs.Enemy>()
            .Set<Direction>(new(0))
            .Set<Position>(new(lvl.spawn_point.X, 1.2f, lvl.spawn_point.Y));
        });

        _moveEnemy = ecs.Routine<Position, Direction, Game>()
            .TermAt(3).Singleton()
            .Term<Enemy>()
            .Each((Iter it, int i, ref Position p, ref Direction d, ref Game g) =>
            {
                MoveEnemy(it, i, ref p, ref d, ref g);
            });

        _spawnEnemy.Interval(EnemySpawnInterval);
    }

    void init_game()
    {
        ref var g = ref ecs.GetMut<Game>();
        g.center = new(to_x(TileCountX / 2), 0, to_z(TileCountZ / 2));
        g.size = TileCountX * (TileSize + TileSpacing) + 2;
    }

    float to_coord(float x)
    {
        return x * (TileSpacing + TileSize) - (TileSize / 2.0f);
    }

    float to_x(float x)
    {
        return to_coord(x + 0.5f) - to_coord((TileCountX / 2.0f));
    }

    float to_z(float z)
    {
        return to_coord(z);
    }

    float from_coord(float x)
    {
        return (x + (TileSize / 2.0f)) / (TileSpacing + TileSize);
    }

    float from_x(float x)
    {
        return from_coord(x + to_coord((TileCountX / 2.0f))) - 0.5f;
    }

    float from_z(float z)
    {
        return from_coord(z);
    }


    void init_prefabs()
    {
        ecs.Prefab<prefabs.Tree>().Scope(() =>
        {
            ecs.Prefab<prefabs.Tree.Trunk>()
                .Set<Position>(new(0, 0.75f, 0))
                .Set<Color>(new(0.25f, 0.2f, 0.1f))
                .Set<Box>(new(.5f, 1.5f, .5f));

            ecs.Prefab<prefabs.Tree.Canopy>()
                .Set<Position>(new(0.0f, 2.0f, 0.0f))
                .Set<Color>(new(0.2f, 0.3f, 0.15f))
                .Set<Box>(new(1.5f, 1.8f, 1.5f));
        });


        ecs.Prefab<prefabs.Tile>()
            .Set<Color>(new(0.2f, 0.34f, 0.15f))
            .Set<Box>(new(TileSize, TileHeight, TileSize));

        ecs.Prefab<prefabs.Path>()
            .Set<Color>(new(0.2f, 0.2f, 0.2f))
            .Set<Box>(new(TileSize + TileSpacing, PathHeight, TileSize + TileSpacing));

        ecs.Prefab<prefabs.materials.Metal>()
            .Set<Color>(new(.1f, .1f, .1f));

        ecs.Prefab<prefabs.materials.CannonHead>()
            .Set<Color>(new(.35f, .4f, .3f));

        ecs.Prefab<prefabs.Enemy>()
            .IsA<prefabs.materials.Metal>()
            .Add<Enemy>()
            .Add<Health>()
            .SetOverride<Color>(new(.05f, .05f, .05f))
            .Set<Box>(new(EnemySize, EnemySize, EnemySize));

        // Turret
        ecs.Prefab<prefabs.Turret>();
        ecs.Prefab<prefabs.Turret.Base>()
            .Slot()
            .Set(new Position(0, 0, 0));

        ecs.Prefab().IsA<prefabs.materials.Metal>()
            .ChildOf<prefabs.Turret.Base>()
            .Set(new Box(0.6f, 0.2f, 0.6f))
            .Set(new Position(0, 0.1f, 0));

        ecs.Prefab().IsA<prefabs.materials.Metal>()
            .ChildOf<prefabs.Turret.Base>()
            .Set(new Box(0.4f, 0.6f, 0.4f))
            .Set(new Position(0, 0.3f, 0));

        //ecs.Prefab<prefabs.Turret.Head>().Slot();



        ecs.Prefab<prefabs.Cannon>()
            .IsA<prefabs.Turret>()
            .Set<Turret>(new(TurretFireInterval));

        ecs.Prefab<prefabs.Cannon.Head>()
            .IsA<prefabs.materials.CannonHead>()
            .Set(new Box(0.8f, 0.4f, 0.8f))
            .Set(new Position(0, 0.8f, 0));

        //ecs.Prefab<prefabs.Cannon.Barrel>()
        //    .IsA<prefabs.materials.Metal>()
        //    .Set<Box>(new(0.8f, 0.14f, 0.14f));

        //ecs.Prefab<prefabs.Cannon.Head.BarrelLeft>()
        //    .SlotOf<prefabs.Cannon>()
        //    .IsA<prefabs.Cannon.Barrel>()
        //    .Set<Position>(new (TurretCannonLength, 0, -TurretCannonOffset));
    }

    void init_level()
    {
        ref Game g = ref ecs.GetMut<Game>();

        grid<bool> path = new grid<bool>(TileCountX, TileCountZ);
        path.set(0, 1, true); path.set(1, 1, true); path.set(2, 1, true);
        path.set(3, 1, true); path.set(4, 1, true); path.set(5, 1, true);
        path.set(6, 1, true); path.set(7, 1, true); path.set(8, 1, true);
        path.set(8, 2, true); path.set(8, 3, true); path.set(7, 3, true);
        path.set(6, 3, true); path.set(5, 3, true); path.set(4, 3, true);
        path.set(3, 3, true); path.set(2, 3, true); path.set(1, 3, true);
        path.set(1, 4, true); path.set(1, 5, true); path.set(1, 6, true);
        path.set(1, 7, true); path.set(1, 8, true); path.set(2, 8, true);
        path.set(3, 8, true); path.set(4, 8, true); path.set(4, 7, true);
        path.set(4, 6, true); path.set(4, 5, true); path.set(5, 5, true);
        path.set(6, 5, true); path.set(7, 5, true); path.set(8, 5, true);
        path.set(8, 6, true); path.set(8, 7, true); path.set(7, 7, true);
        path.set(6, 7, true); path.set(6, 8, true); path.set(6, 9, true);
        path.set(7, 9, true); path.set(8, 9, true); path.set(9, 9, true);

        Position2 spawn_point = new(
        to_x(TileCountX - 1),
        to_z(TileCountZ - 1));

        g.Level = ecs.Entity().Set<Level>(new(path, spawn_point));

        ecs.Entity()
            .Set<Position>(new(0, -2.5f, to_z(TileCountZ / 2.0f - 0.5f)))
            .Set<Box>(new(to_x(TileCountX + 0.5f) * 2, 5, to_z(TileCountZ + 2)))
            .Set<Color>(new(0.11f, 0.15f, 0.1f));

        for (int x = 0; x < TileCountX; x++)
        {
            for (int z = 0; z < TileCountZ; z++)
            {
                float xc = to_x(x);
                float zc = to_z(z);

                var t = ecs.Entity().Set<Position>(new(xc, 0, zc));
                if (path[x, z])
                {
                    t.IsA<prefabs.Path>();
                }
                else
                {
                    t.IsA<prefabs.Tile>();

                    var e = ecs.Entity().Set<Position>(new(xc, TileHeight / 2, zc));

                    if (Random.Range(0.0f, 1.0f) > .65f)
                    {
                        Debug.Log($"Creating tree at {e.Get<Position>()}");

                        e.ChildOf<Level>();
                        e.IsA<prefabs.Tree>();
                    }
                    else
                    {
                        e.ChildOf<Turrets>();

                        if (Random.Range(0.0f, 1.0f) > .3f)
                        {
                            e.IsA<prefabs.Turret>();
                        }
                    }

                }
            }
        }
    }

    bool find_path(in Position p, ref Direction d, in Level lvl)
    {
        // Check if enemy is in center of tile
        float t_x = from_x(p.X);
        float t_y = from_z(p.Z);
        int ti_x = (int)t_x;
        int ti_y = (int)t_y;
        float td_x = t_x - ti_x;
        float td_y = t_y - ti_y;

        // If enemy is in center of tile, decide where to go next
        if (td_x < 0.1 && td_y < 0.1)
        {
            grid<bool> tiles = lvl.map;

            // Compute backwards direction so we won't try to go there
            int backwards = (d.Value + 2) % 4;

            // Find a direction that the enemy can move to
            for (int i = 0; i < 3; i++)
            {
                int n_x = (int)(ti_x + dir[d.Value].X);
                int n_y = (int)(ti_y + dir[d.Value].Y);

                if (n_x >= 0 && n_x <= TileCountX)
                {
                    if (n_y >= 0 && n_y <= TileCountZ)
                    {
                        // Next tile is still on the grid, test if it's a path
                        if (tiles[n_x, n_y])
                        {
                            // Next tile is a path, so continue along current direction
                            return false;
                        }
                    }
                }

                // Try next direction. Make sure not to move backwards
                do
                {
                    d.Value = (d.Value + 1) % 4;
                } while (d.Value == backwards);
            }

            // If enemy was not able to find a next direction, it reached the end
            return true;
        }

        return false;
    }

    void MoveEnemy(Iter it, int i, ref Position p, ref Direction d, ref Game g)
    {
        var lvl = g.Level.Get<Level>();

        if (find_path(p, ref d, lvl))
        {

            Debug.Log("Destroying");
            it.Entity(i).Destruct(); // Enemy made it to the end
        }
        else
        {
            p.X += dir[d.Value].X * EnemySpeed * it.DeltaTime();
            p.Z += dir[d.Value].Y * EnemySpeed * it.DeltaTime();
        }
    }
}
