using Flecs.NET.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static Flecs.NET.Bindings.flecs;
using tower_defense;
using static Utils;
using static Constants;

public struct vec3
{
    public float x, y, z;

    // accessor
    public float this[int i]
    {
        get => i == 0 ? x : i == 1 ? y : z;
        set
        {
            if (i == 0) x = value;
            else if (i == 1) y = value;
            else z = value;
        }
    }
}

public static class Utils
{
    public static int ToInt(this bool b) => b ? 1 : 0;
    public static float Rand(float max) => Random.Range(0.0f, max);
}

public struct Waypoint
{
    public float x, y;

    public static implicit operator Waypoint((float x, float y) p) => new() { x = p.x, y = p.y };
};

public enum TileKind
{
    Turret = 0, // Default
    Path,
    Other
};

public static class Constants
{
    // Game constants
    public const int LevelScale = 1;

    public const int TileCountX = 20;
    public const int TileCountZ = 20;

}

public sealed class Waypoints
{
    public Waypoints(grid<TileKind> g, params Waypoint[] pts)
    {
        tiles = g;
        foreach (Waypoint p in pts)
            add(p, TileKind.Path);
    }

    void add(Waypoint next, TileKind kind)
    {
        next.x *= LevelScale; next.y *= LevelScale;
        if (next.x == last.x)
        {
            do
            {
                last.y += Utils.ToInt(last.y < next.y) - Utils.ToInt(last.y > next.y);
                tiles.set((int)last.x, (int)last.y, kind);
            } while (next.y != last.y);
        }
        else if (next.y == last.y)
        {
            do
            {
                last.x += Utils.ToInt(last.x < next.x) - Utils.ToInt(last.x > next.x);
                tiles.set((int)(int)last.x, (int)last.y, kind);
            } while (next.x != last.x);
        }

        last.x = next.x;
        last.y = next.y;
    }

    void fromTo(Waypoint first, Waypoint second, TileKind kind)
    {
        last = first;
        add(second, kind);
    }

    grid<TileKind> tiles;
    Waypoint last;
};

// Components
namespace tower_defense
{
    public struct Game
    {
        public Entity Window;
        public Entity Level;

        public Position3 center;
        public float size;
    }

    public struct Level
    {
        public Level(grid<TileKind> arg_map, Position2 spawn)
        {
            map = arg_map;
            spawn_point = spawn;
        }

        public grid<TileKind> map;
        public Position2 spawn_point;
    }

    public struct Enemy { }

    public struct Health
    {
        public float value;
    }

    public struct Turret
    {
        public Turret(float fire_interval_arg = 1.0f)
        {
            lr = 1;
            t_since_fire = 0;
            fire_interval = fire_interval_arg;
        }

        public float fire_interval;
        public float t_since_fire;
        public int lr;
    };

    public struct HitCooldown
    {
        public float value;
    }

    public struct Laser { }

    public struct Target
    {
        Entity target;
        vec3 prev_position;
        vec3 aim_position;
        float angle;
        float distance;
        bool Lock;
    }
}

// scopes
public struct level { }
public struct enemies { }
public struct turrets { }

public class Main : MonoBehaviour
{
    public ushort _port = 27750;


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

    private void OnDestroy()
    {
        ecs.Dispose();
    }

    void Start()
    {
        ecs = World.Create();
        ecs.Set(new EcsRest());


        init_components();
        init_game();
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

        ecs.Progress();

        foreach (var cmd in renderCommands)
        {
            Graphics.DrawMesh(cmd.Mesh, cmd.Transform, cmd.Material, 0, null, 0, cmd.Properties);
        }
    }

    void init_systems()
    {
        ecs.Routine<Position3, GlobalPosition, GlobalPosition>()
            .TermAt(1).Optional()
            .TermAt(2).Parent().Cascade().Optional()
            .Each((Entity e, ref Position3 p, ref GlobalPosition global, ref GlobalPosition parentGlobal) =>
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

            ecs.Entity()
            .ChildOf<enemies>()
            .IsA<tower_defense.prefabs.Enemy>()
            .Set<Direction>(new(0))
            .Set<Position3>(new(lvl.spawn_point.X, 1.2f, lvl.spawn_point.Y));
        });

        _spawnEnemy.Interval(EnemySpawnInterval);

        _moveEnemy = ecs.Routine<Position3, Direction, Game>()
            .TermAt(2).Singleton()
            .With<Enemy>()
            .Each((Iter it, int i, ref Position3 p, ref Direction d, ref Game g) =>
            {
                MoveEnemy(it, i, ref p, ref d, ref g);
            });
    }

    void init_game()
    {
        ref var g = ref ecs.Ensure<Game>();
        g.center = new(toX(TileCountX / 2), 0, toZ(TileCountZ / 2));
        g.size = TileCountX * (TileSize + TileSpacing) + 2;

        ecs.ScriptRunFile(ScriptPath("materials.flecs"));
        ecs.ScriptRunFile(ScriptPath("tile.flecs"));
        ecs.ScriptRunFile(ScriptPath("tree.flecs"));
        ecs.ScriptRunFile(ScriptPath("enemy.flecs"));
        ecs.ScriptRunFile(ScriptPath("turret.flecs"));
        ecs.ScriptRunFile(ScriptPath("cannon.flecs"));
        ecs.ScriptRunFile(ScriptPath("laser.flecs"));

    }

    private static string ScriptPath(string name) => System.IO.Path.Combine(Application.streamingAssetsPath, name);

    private void init_components()
    {
        ecs.Component<Position3>()
            .Member<float>("X")
            .Member<float>("Y")
            .Member<float>("Z");

        // rotation3
        ecs.Component<Rotation3>()
            .Member<float>("X")
            .Member<float>("Y")
            .Member<float>("Z");

        ecs.Component<Rectangle>()
            .Member<float>("Width")
            .Member<float>("Height");

        ecs.Component<Box>()
            .Member<float>("X")
            .Member<float>("Y")
            .Member<float>("Z");

        ecs.Component<PointLight>()
            //.Member<Color>("color")
            .Member<float>("intensity")
            .Member<float>("distance");


        // register Specular component
        ecs.Component<Specular>()
            .Member<float>("a")
            .Member<float>("b");

        // Emmisive component
        ecs.Component<Emissive>("Emissive")
            .Member<float>("Value");

        ecs.Component<Color>("Rgb")
            .Member<float>("r")
            .Member<float>("g")
            .Member<float>("b");

        // tree
        ecs.Component<tower_defense.prefabs.Tree>()
            .Member<float>("height")
            .Member<float>("variation");

        ecs.Component<tower_defense.Health>()
            .Member<float>("Value");

        ecs.Component<tower_defense.HitCooldown>()
            .Member<float>("Value");

        // enemy
        ecs.Component<tower_defense.Enemy>();
        ecs.Component<tower_defense.Laser>();

        // game
        ecs.Component<Game>()
            .Member(Ecs.Entity, "Window")
            .Member(Ecs.Entity, "Level")
            .Member<Position3>("center")
            .Member<float>("size");

        ecs.Component<Turret>()
            .Member<float>("fire_interval");

        ecs.Component<Target>()
            .Member(Ecs.Entity, "target")
            .Member<float>("angle")
            .Member<float>("distance")
            .Member<bool>("Lock");
    }

    float to_coord(float x)
    {
        return x * (TileSpacing + TileSize) - (TileSize / 2.0f);
    }

    float toX(float x)
    {
        return to_coord(x + 0.5f) - to_coord((TileCountX / 2.0f));
    }

    float toZ(float z)
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

    void init_level()
    {
        ref Game g = ref ecs.Ensure<Game>();

        var path = new grid<TileKind>(TileCountX * LevelScale, TileCountZ * LevelScale);

        Waypoints waypoints = new Waypoints(path, new Waypoint[]
        {
            (0, 1), (8, 1), (8, 3), (1, 3), (1, 8), (4, 8), (4, 5), (8, 5), (8, 7),
            (6, 7), (6, 9), (11, 9), (11, 1), (18, 1), (18, 3), (16, 3), (16, 5),
            (18, 5), (18, 7), (16, 7), (16, 9), (18, 9), (18, 12), (1, 12), (1, 18),
            (3, 18), (3, 15), (5, 15), (5, 18), (7, 18), (7, 15), (9, 15), (9, 18),
            (12, 18), (12, 14), (18, 14), (18, 16), (14, 16), (14, 19), (19, 19)
        });

        Position2 spawn_point = new(
            toX(TileCountX - 1),
            toZ(TileCountZ - 1));

        g.Level = ecs.Entity().ChildOf<Level>()
            .Set<Level>(new(path, spawn_point));

        ecs.Entity("GroundPlane")
            .ChildOf<level>()
            .Set<Position3>(new(0, -2.7f, toZ(TileCountZ / 2.0f - 0.5f)))
            .Set<Box>(new(toX(TileCountX + 0.5f) * 20, 5, toZ(TileCountZ + 2) * 10))
            .Set<Color>(new(0.11f, 0.15f, 0.1f));

        for (int x = 0; x < TileCountX * LevelScale; x++)
        {
            for (int z = 0; z < TileCountZ * LevelScale; z++)
            {
                float xc = toX(x);
                float zc = toZ(z);

                var t = ecs.Entity().ChildOf<level>().Set<Position3>(new(xc, 0, zc));
                if (path[x, z] == TileKind.Path)
                {
                    t.IsA<tower_defense.prefabs.Path>();
                }
                else if (path[x, z] == TileKind.Turret)
                {
                    t.IsA<tower_defense.prefabs.Tile>();


                    bool canTurret = false;
                    if (x < (TileCountX * LevelScale - 1) && (z < (TileCountZ * LevelScale - 1)))
                    {
                        canTurret |= (path[x + 1, z] == TileKind.Path);
                        canTurret |= (path[x, z + 1] == TileKind.Path);
                    }
                    if (x > 0 && z > 0)
                    {
                        canTurret |= (path[x - 1, z] == TileKind.Path);
                        canTurret |= (path[x, z - 1] == TileKind.Path);
                    }

                    var e = ecs.Entity().Set<Position3>(new(xc, TileHeight / 2, zc));

                    if (!canTurret || Rand(1.0f) > 3.0f)
                    {
                        if (Rand(1.0f) > .05f)
                        {
                            e.ChildOf<level>();
                            e.Set(new tower_defense.prefabs.Tree { height = 1.5f + Rand(2.5f), variation = Rand(.1f) });
                            e.Set(new Rotation3(0.0f, Rand(2.0f * Mathf.PI), 0.0f));
                        }
                        else
                        {
                            e.Destruct();
                        }
                    }
                    else
                    {
                        e.ChildOf<turrets>();

                        if (Rand(1.0f) > 0.3f)
                        {
                            e.IsA<tower_defense.prefabs.Cannon>();
                        }
                        else
                        {
                            e.IsA<tower_defense.prefabs.Laser>();
                        }
                    }
                }
                else if (path[x, z] == TileKind.Other)
                {
                    t.IsA<tower_defense.prefabs.Tile>();
                }
            }
        }
    }



    bool find_path(in Position3 p, ref Direction d, in Level lvl)
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
            var tiles = lvl.map;

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
                        if (tiles[n_x, n_y] == TileKind.Path)
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

    void MoveEnemy(Iter it, int i, ref Position3 p, ref Direction d, ref Game g)
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
