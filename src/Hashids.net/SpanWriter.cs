using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Buffers;

public ref struct SpanWriter
{
    private readonly bool _resize;
    private char[] _arrayToReturnToPool;
    private Span<char> _buffer;
    private int _position;

    public int BytesWritten { get; private set; }

    public int Position
    {
        get => _position;
        private set
        {
            _position = value;

            if (value > BytesWritten)
            {
                BytesWritten = value;
            }
        }
    }

    public int Capacity => _buffer.Length;

    public ReadOnlySpan<char> Span => _buffer[..Position];

    public Span<char> RawBuffer => _buffer;

    /**
     * Converts the writer to a Span<char> using a SpanOwner.
     * If the buffer was stackalloc, it will be copied to a rented buffer.
     * Otherwise the existing rented buffer is used.
     *
     * Note:
     * Do not use the SpanWriter after calling this method.
     * This method will effectively dispose of the SpanWriter and is therefore considered terminal.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanOwner ToSpan()
    {
        var toReturn = _arrayToReturnToPool;

        SpanOwner apo;
        if (_position == 0)
        {
            apo = new SpanOwner(_position, Array.Empty<char>());
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }
        else if (toReturn != null)
        {
            apo = new SpanOwner(_position, toReturn);
        }
        else
        {
            var buffer = ArrayPool<char>.Shared.Rent(_position);
            _buffer.CopyTo(buffer);
            apo = new SpanOwner(_position, buffer);
        }

        this = default; // Don't allow two references to the same buffer
        return apo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanWriter(Span<char> initialBuffer, bool resize = false)
    {
        _resize = resize;
        _buffer = initialBuffer;
        _position = 0;
        BytesWritten = 0;
        _arrayToReturnToPool = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanWriter(int initialCapacity, bool resize = false)
    {
        _resize = resize;
        _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
        _buffer = _arrayToReturnToPool;
        _position = 0;
        BytesWritten = 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacity)
    {
        var newSize = Math.Max(BytesWritten + additionalCapacity, _buffer.Length * 2);
        var poolArray = ArrayPool<char>.Shared.Rent(newSize);

        _buffer[..BytesWritten].CopyTo(poolArray);

        var toReturn = _arrayToReturnToPool;
        _buffer = _arrayToReturnToPool = poolArray;
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GrowIfNeeded(int count)
    {
        if (_position + count > _buffer.Length)
        {
            if (!_resize)
            {
                throw new OutOfMemoryException();
            }

            Grow(count);
        }
    }

    public ref char GetPinnableReference() => ref MemoryMarshal.GetReference(_buffer);

    public void EnsureCapacity(int capacity)
    {
        if (capacity > _buffer.Length)
        {
            if (!_resize)
            {
                throw new OutOfMemoryException();
            }

            Grow(capacity - BytesWritten);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(char value)
    {
        GrowIfNeeded(1);
        _buffer[Position++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlySpan<char> buffer)
    {
        var count = buffer.Length;
        GrowIfNeeded(count);
        buffer.CopyTo(_buffer[_position..]);
        Position += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int count)
    {
        GrowIfNeeded(count);
        _buffer.Slice(_position, count).Clear();
        Position += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Seek(int offset, SeekOrigin origin)
    {
        Debug.Assert(
            origin != SeekOrigin.End || _resize || offset <= 0,
            "Attempting to seek to a position beyond capacity using SeekOrigin.End without resize"
        );

        Debug.Assert(
            origin != SeekOrigin.End || offset >= -_buffer.Length,

            "Attempting to seek to a negative position using SeekOrigin.End"
        );

        Debug.Assert(
            origin != SeekOrigin.Begin || offset >= 0,
            "Attempting to seek to a negative position using SeekOrigin.Begin"
        );

        Debug.Assert(
            origin != SeekOrigin.Begin || _resize || offset <= _buffer.Length,
            "Attempting to seek to a position beyond the capacity using SeekOrigin.Begin without resize"
        );

        Debug.Assert(
            origin != SeekOrigin.Current || _position + offset >= 0,
            "Attempting to seek to a negative position using SeekOrigin.Current"
        );

        Debug.Assert(
            origin != SeekOrigin.Current || _resize || _position + offset <= _buffer.Length,
            "Attempting to seek to a position beyond the capacity using SeekOrigin.Current without resize"
        );

        var newPosition = Math.Max(0, origin switch
        {
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End     => BytesWritten + offset,
            _                  => offset // Begin
        });

        if (newPosition >= _buffer.Length)
        {
            Grow(newPosition - _buffer.Length + 1);
        }

        return Position = newPosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var toReturn = _arrayToReturnToPool;
        this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    public struct SpanOwner : IDisposable
    {
        private readonly int _length;
        private readonly char[] _arrayToReturnToPool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SpanOwner(int length, char[] buffer)
        {
            _length = length;
            _arrayToReturnToPool = buffer;
        }

        public Span<char> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(_arrayToReturnToPool), _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var toReturn = _arrayToReturnToPool;
            this = default;
            if (_length > 0)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }
    }
}
