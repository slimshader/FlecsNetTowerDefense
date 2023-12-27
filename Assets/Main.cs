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

    World ecs;

    void Start()
    {
        ecs = World.Create();

        ecs.Observer(
            name: "asd",
            filter: ecs.FilterBuilder<Position, Color, Box>(),
            observer: ecs.ObserverBuilder().Event(Ecs.OnSet),
            callback: (Iter i, Column<Position> p, Column<Color> c, Column<Box> b) =>
            {
                foreach (var e in i)
                {
                    var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    v.transform.position = new(p[e].X, p[e].Y, p[e].Z);
                    v.transform.localScale = new(b[e].X, b[e].Y, b[e].Z);
                    v.GetComponent<Renderer>().material.color = c[e];
                }
            });


        ecs.Add<Game>();

        init_game();
        init_prefabs();
        init_level();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void init_game()
    {
        var g = ecs.Get<Game>();
        g.center = new(to_x(TileCountX / 2), 0, to_z(TileCountZ / 2));
        g.size = TileCountX * (TileSize + TileSpacing) + 2;
    }

    const float TileSize = 3.0f;
    const float TileSpacing = 0;
    const float TileHeight = 0.5f;
    const float PathHeight = 0.1f;

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

    class prefabs
    {
        public struct Path { };
        public struct Tile { };
    }

    void init_prefabs()
    {
        ecs.Prefab<prefabs.Tile>()
            .Set<Color>(new(0.2f, 0.34f, 0.15f))
            .Set<Box>(new(TileSize, TileHeight, TileSize));

        ecs.Prefab<prefabs.Path>()
            .Set<Color>(new(0.2f, 0.2f, 0.2f))
            .Set<Box>(new(TileSize + TileSpacing, PathHeight, TileSize + TileSpacing));
    }

    void init_level()
    {
        Game g = ecs.Get<Game>();

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
