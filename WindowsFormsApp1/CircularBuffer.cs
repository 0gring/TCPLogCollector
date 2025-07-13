using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1
{
    internal class CircularBuffer
    {
    }
}
public class CircularBuffer<T>
{
    private readonly T[] buffer;
    private int start, end, size;
    private readonly int capacity;

    public CircularBuffer(int capacity)
    {
        this.capacity = capacity;
        buffer = new T[capacity];
        start = 0; end = 0; size = 0;
    }

    public void Add(T item)
    {
        buffer[end] = item;
        end = (end + 1) % capacity;
        if (size == capacity)
            start = (start + 1) % capacity;
        else
            size++;
    }

    public T[] GetItems()
    {
        T[] items = new T[size];
        for (int i = 0; i < size; i++)
            items[i] = buffer[(start + i) % capacity];
        return items;
    }
}
