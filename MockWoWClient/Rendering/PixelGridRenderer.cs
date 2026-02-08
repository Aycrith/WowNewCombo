using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using MockWoWClient.Contracts;
using System.Runtime.CompilerServices;

namespace MockWoWClient.Rendering;

/// <summary>
/// Renders the 324 pixel frames that simulate the DataToColor addon output.
/// </summary>
public sealed class PixelGridRenderer : IDisposable
{
    private readonly Image<Bgra32> _bitmap;
    private readonly int[] _frameValues;
    private readonly FrameData[] _framePositions;
    private int _globalTick;
    private bool _isConfigMode;

    /// <summary>
    /// Width of the entire pixel grid in pixels.
    /// </summary>
    public int Width => _bitmap.Width;

    /// <summary>
    /// Height of the entire pixel grid in pixels.
    /// </summary>
    public int Height => _bitmap.Height;

    /// <summary>
    /// Creates a new PixelGridRenderer with the specified dimensions.
    /// </summary>
    public PixelGridRenderer(int cellSize = AddonConstants.CellSize, int cellSpacing = AddonConstants.CellSpacing)
    {
        // Calculate grid dimensions
        int cols = (int)Math.Ceiling((double)AddonConstants.NumberOfFrames / AddonConstants.FrameRows);
        int pixelWidth = cols * (cellSize + cellSpacing);
        int pixelHeight = AddonConstants.FrameRows * (cellSize + cellSpacing);

        _bitmap = new Image<Bgra32>(pixelWidth, pixelHeight);
        _frameValues = new int[AddonConstants.NumberOfFrames];
        _framePositions = new FrameData[AddonConstants.NumberOfFrames];
        _globalTick = 0;
        _isConfigMode = false;

        InitializeFramePositions(cellSize, cellSpacing);
        InitializeValidationFrames();
    }

    /// <summary>
    /// Gets the current global tick counter.
    /// </summary>
    public int GlobalTick => _globalTick;

    /// <summary>
    /// Gets or sets config mode. In config mode, frames display their index.
    /// </summary>
    public bool IsConfigMode
    {
        get => _isConfigMode;
        set
        {
            if (_isConfigMode != value)
            {
                _isConfigMode = value;
                if (_isConfigMode)
                {
                    RenderConfigMode();
                }
            }
        }
    }

    /// <summary>
    /// Sets a frame value and updates the pixel data.
    /// </summary>
    /// <param name="frameIndex">The frame index (0-323).</param>
    /// <param name="value">The value to encode.</param>
    public void SetFrame(int frameIndex, int value)
    {
        if ((uint)frameIndex >= (uint)AddonConstants.NumberOfFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), $"Frame index must be between 0 and {AddonConstants.NumberOfFrames - 1}");
        }

        _frameValues[frameIndex] = value;
        
        if (!_isConfigMode)
        {
            RenderFrame(frameIndex, value);
        }
    }

    /// <summary>
    /// Gets the current value of a frame.
    /// </summary>
    public int GetFrame(int frameIndex)
    {
        if ((uint)frameIndex >= (uint)AddonConstants.NumberOfFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        return _frameValues[frameIndex];
    }

    /// <summary>
    /// Sets a float value at the specified frame.
    /// </summary>
    public void SetFrameFloat(int frameIndex, float value)
    {
        int intValue = (int)(value * 100000f);
        SetFrame(frameIndex, intValue);
    }

    /// <summary>
    /// Sets a string value (up to 3 chars) at the specified frame.
    /// </summary>
    public void SetFrameString(int frameIndex, string text)
    {
        var (r, g, b) = FrameEncoder.EncodeString(text);
        int value = FrameEncoder.DecodeInt(r, g, b);
        SetFrame(frameIndex, value);
    }

    /// <summary>
    /// Sets boolean bits at the specified cell.
    /// </summary>
    /// <param name="cellIndex">The cell index (e.g., 8 or 9).</param>
    /// <param name="bits">The boolean flags to encode.</param>
    public void SetFrameBits(int cellIndex, ReadOnlySpan<bool> bits)
    {
        int value = 0;
        int bitCount = Math.Min(bits.Length, 24);

        for (int i = 0; i < bitCount; i++)
        {
            if (bits[i])
            {
                value |= 1 << i;
            }
        }

        SetFrame(cellIndex, value);
    }

    /// <summary>
    /// Sets a single bit in the specified cell.
    /// </summary>
    public void SetBit(int cellIndex, int bitIndex, bool value)
    {
        int currentValue = GetFrame(cellIndex);
        int mask = 1 << bitIndex;

        if (value)
        {
            currentValue |= mask;
        }
        else
        {
            currentValue &= ~mask;
        }

        SetFrame(cellIndex, currentValue);
    }

    /// <summary>
    /// Gets a single bit from the specified cell.
    /// </summary>
    public bool GetBit(int cellIndex, int bitIndex)
    {
        int value = GetFrame(cellIndex);
        int mask = 1 << bitIndex;
        return (value & mask) != 0;
    }

    /// <summary>
    /// Captures the current screen as an image.
    /// This is what the bot's IWowScreen will read.
    /// </summary>
    public Image<Bgra32> CaptureScreen()
    {
        // Update global time cell before capture
        _globalTick++;
        if (!_isConfigMode)
        {
            SetFrame(AddonConstants.GlobalTimeCellIndex, _globalTick);
        }

        // Clone the bitmap for the caller
        return _bitmap.Clone();
    }

    /// <summary>
    /// Gets the frame positions for configuration.
    /// </summary>
    public ReadOnlySpan<FrameData> GetFramePositions() => _framePositions;

    /// <summary>
    /// Clears all frame data and resets to initial state.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_frameValues, 0, _frameValues.Length);
        InitializeValidationFrames();
    }

    private void InitializeFramePositions(int cellSize, int cellSpacing)
    {
        int cols = (int)Math.Ceiling((double)AddonConstants.NumberOfFrames / AddonConstants.FrameRows);

        for (int i = 0; i < AddonConstants.NumberOfFrames; i++)
        {
            int row = i % AddonConstants.FrameRows;
            int col = i / AddonConstants.FrameRows;

            int x = col * (cellSize + cellSpacing);
            int y = row * (cellSize + cellSpacing);

            _framePositions[i] = new FrameData(i, x, y, cellSize, cellSize, 0);
        }
    }

    private void InitializeValidationFrames()
    {
        // Frame 0 is always black (0, 0, 0)
        SetFrame(0, 0);

        // Frame 323 is validation value 2000001
        SetFrame(AddonConstants.ValidationCellIndex, AddonConstants.ValidationCellValue);

        // Set end marker RGB values for last frame position
        // This is stored in frame 323
        RenderFrame(AddonConstants.ValidationCellIndex, AddonConstants.ValidationCellValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RenderFrame(int frameIndex, int value)
    {
        var frame = _framePositions[frameIndex];
        var (r, g, b) = FrameEncoder.EncodeInt(value);

        // Render the cell as a solid color block
        for (int y = 0; y < frame.Height; y++)
        {
            for (int x = 0; x < frame.Width; x++)
            {
                int pixelX = frame.X + x;
                int pixelY = frame.Y + y;

                if (pixelX < _bitmap.Width && pixelY < _bitmap.Height)
                {
                    _bitmap[pixelX, pixelY] = new Bgra32(r, g, b, 255);
                }
            }
        }
    }

    private void RenderConfigMode()
    {
        // In config mode, each frame shows its index as RGB
        for (int i = 0; i < AddonConstants.NumberOfFrames; i++)
        {
            RenderFrame(i, i);
        }
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
    }
}
