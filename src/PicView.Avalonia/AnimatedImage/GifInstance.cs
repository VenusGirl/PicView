using Avalonia;
using Avalonia.Animation;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PicView.Avalonia.AnimatedImage.Decoding;

namespace PicView.Avalonia.AnimatedImage;

public class GifInstance : IGifInstance
{
    public IterationCount IterationCount { get; set; }
    public bool AutoStart => true;
    private readonly GifDecoder _gifDecoder;
    private readonly WriteableBitmap _targetBitmap;
    private TimeSpan _totalTime;
    private readonly List<TimeSpan> _frameTimes;
    private uint _iterationCount;
    private int _currentFrameIndex;

    public CancellationTokenSource CurrentCts { get; }

    public GifInstance(Stream currentStream)
    {
        if (!currentStream.CanSeek)
            throw new InvalidDataException("The provided stream is not seekable.");

        if (!currentStream.CanRead)
            throw new InvalidOperationException("Can't read the stream provided.");

        currentStream.Seek(0, SeekOrigin.Begin);

        CurrentCts = new CancellationTokenSource();

        _gifDecoder = new GifDecoder(currentStream, CurrentCts.Token);
        var pixSize = new PixelSize(_gifDecoder.Header.Dimensions.Width, _gifDecoder.Header.Dimensions.Height);

        _targetBitmap = new WriteableBitmap(pixSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        GifPixelSize = pixSize;

        _totalTime = TimeSpan.Zero;

        _frameTimes = _gifDecoder.Frames.Select(frame =>
        {
            _totalTime = _totalTime.Add(frame.FrameDelay);
            return _totalTime;
        }).ToList();

        _gifDecoder.RenderFrame(0, _targetBitmap);
    }

    public int GifFrameCount => _frameTimes.Count;

    public PixelSize GifPixelSize { get; }
    public bool IsDisposed { get; set; }

    public void Dispose()
    {
        if (IsDisposed) return;
            
        GC.SuppressFinalize(this);

        IsDisposed = true;
        CurrentCts.Cancel();
        _targetBitmap.Dispose();
    }

    [CanBeNull]
    public WriteableBitmap? ProcessFrameTime(TimeSpan elapsed)
    {
        if (!IterationCount.IsInfinite && _iterationCount > IterationCount.Value)
        {
            return null;
        }

        if (CurrentCts.IsCancellationRequested)
        {
            return null;
        }
            
        var totalTicks = _totalTime.Ticks;

        if (totalTicks == 0)
        {
            return ProcessFrameIndex(0);
        }

        var elapsedTicks = elapsed.Ticks;
        var timeModulus = TimeSpan.FromTicks(elapsedTicks % totalTicks);
        var targetFrame = _frameTimes.FirstOrDefault(x => timeModulus < x);
        var currentFrame = _frameTimes.IndexOf(targetFrame);
        if (currentFrame == -1) currentFrame = 0;

        if (_currentFrameIndex == currentFrame)
            return _targetBitmap;

        _iterationCount = (uint)(elapsedTicks / totalTicks);

        return ProcessFrameIndex(currentFrame);
    }

    internal WriteableBitmap ProcessFrameIndex(int frameIndex)
    {
        _gifDecoder.RenderFrame(frameIndex, _targetBitmap);
        _currentFrameIndex = frameIndex;

        return _targetBitmap;
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Delegate | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class CanBeNullAttribute : Attribute { }

