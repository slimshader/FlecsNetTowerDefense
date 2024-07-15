using System.Collections.Generic;

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
