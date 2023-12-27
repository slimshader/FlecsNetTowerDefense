public struct Position
{
    public float X, Y, Z;

    public Position(float x, float y, float z) => (X, Y, Z) = (x, y, z);
}

public struct Position2
{
    public float X, Z;

    public Position2(float x, float z) => (X, Z) = (x, z);
}


public struct Velocity
{
    public float X, Y, Z;
}

public struct Box
{
    public float X, Y, Z;

    public Box(float x, float y, float z) => (X, Y, Z) = (x, y, z);
}