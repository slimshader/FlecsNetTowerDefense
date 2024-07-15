using UnitGenerator;
using UnityEngine;

public struct Rgb
{
    public float R, G, B;
}

public struct Specular
{
    public float a, b;
}

public struct Emissive
{
    public float Value;
}

public struct Position3
{
    public float X, Y, Z;

    public Position3(float x, float y, float z) => (X, Y, Z) = (x, y, z);

    public override string ToString() => $"({X},{Y},{Z})";
}

public struct Rotation3
{
    public float X, Y, Z;

    public Rotation3(float x, float y, float z) => (X, Y, Z) = (x, y, z);
}

[UnitOf(typeof(Vector3))]
public partial struct GlobalPosition { }

public struct Position2
{
    public float X, Y;

    public Position2(float x, float y) => (X, Y) = (x, y);
}


public struct Velocity
{
    public float X, Y, Z;
}

public struct Rectangle
{
    public float Width, Height;

    //public Rectangle(float width, float height) => (Width, Height) = (width, height);
}

public struct Box
{
    public float X, Y, Z;

    public Box(float x, float y, float z) => (X, Y, Z) = (x, y, z);
}



public struct Direction
{
    public int Value;

    public Direction(int value)
    {
        Value = value;
    }
}


public struct Bullet { };
