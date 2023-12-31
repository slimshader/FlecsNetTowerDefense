public struct Position
{
    public float X, Y, Z;

    public Position(float x, float y, float z) => (X, Y, Z) = (x, y, z);
}

public struct Position2
{
    public float X, Y;

    public Position2(float x, float y) => (X, Y) = (x, y);
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

public struct Direction
{
    public int Value;

    public Direction(int value)
    {
        Value = value;
    }
}

public struct Enemy { }

public struct Health { }