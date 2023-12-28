using Flecs.NET.Core;
using UnityEngine;
using System.Collections.Generic;

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
    public struct Path { };
    public struct Tile { };
    public struct Enemy { };

    public class materials
    {
        public struct Metal { };
    }
}


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

public class Main : MonoBehaviour
{
    const int TileCountX = 10;
    const int TileCountZ = 10;
    const float TileSize = 3.0f;
    const float TileSpacing = 0;
    const float TileHeight = 0.5f;
    const float PathHeight = 0.1f;
    const float EnemySize = 0.7f;
    const float EnemySpeed = 4.0f;
    const float EnemySpawnInterval = 0.2f;

    World ecs;
    private Routine spawnEnemy;

    void Start()
    {
        ecs = World.Create();

        ecs.Observer(
            name: "asd",
            filter: ecs.FilterBuilder<Position, Color, Box>(),
            observer: ecs.ObserverBuilder().Event(Ecs.OnSet),
            callback: (Iter it, int e) =>
            {
                var p = it.Field<Position>(1);
                var c = it.Field<Color>(2);
                var b = it.Field<Box>(3);

                {
                    Vector3 pos = new(p[e].X, p[e].Y, p[e].Z);
                    var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    v.transform.localScale = new(b[e].X, b[e].Y, b[e].Z);
                    v.transform.position = pos;
                    v.GetComponent<Renderer>().material.color = c[e];
                }
            });


        ecs.Set<Game>(new Game());

        init_game();
        init_prefabs();
        init_level();
        init_systems();
    }

    void Update()
    {
        ecs.Progress(Time.deltaTime);
    }

    void init_systems()
    {
        spawnEnemy = ecs.Routine(filter: ecs.FilterBuilder<Game>(), callback: (Iter i, Column<Game> g) =>
        {
            Debug.Log("spawn");

            //foreach (var e in i)
            {
                var game = ecs.Get<Game>();
                var lvl = game.Level.Get<Level>();

                i.World().Entity().IsA<prefabs.Enemy>()
                .Set<Direction>(new(0))
                .Set<Position>(new(lvl.spawn_point.X, 1.2f, lvl.spawn_point.Y));
            }
        });

        spawnEnemy.Interval(2);
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


    void init_prefabs()
    {
        ecs.Prefab<prefabs.Tile>()
            .Set<Color>(new(0.2f, 0.34f, 0.15f))
            .Set<Box>(new(TileSize, TileHeight, TileSize));

        ecs.Prefab<prefabs.Path>()
            .Set<Color>(new(0.2f, 0.2f, 0.2f))
            .Set<Box>(new(TileSize + TileSpacing, PathHeight, TileSize + TileSpacing));

        ecs.Prefab<prefabs.materials.Metal>()
            .Set<Color>(new(.1f, .1f, .1f));

        ecs.Prefab<prefabs.Enemy>()
            .IsA<prefabs.materials.Metal>()
            .Add<Enemy>()
            .Add<Health>()
            .SetOverride<Color>(new(.05f, .05f, .05f))
            .Set<Box>(new(EnemySize, EnemySize, EnemySize));
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

                    //var e = ecs.Entity().Set<Position>(new(xc, TileHeight / 2, zc));
                }
            }
        }
    }
}
