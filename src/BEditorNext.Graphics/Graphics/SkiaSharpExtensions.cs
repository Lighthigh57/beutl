﻿
using BEditorNext.Media;

using SkiaSharp;

namespace BEditorNext.Graphics;

internal static class SkiaSharpExtensions
{
    public static SKFilterQuality ToSKFilterQuality(this BitmapInterpolationMode interpolationMode)
    {
        return interpolationMode switch
        {
            BitmapInterpolationMode.LowQuality => SKFilterQuality.Low,
            BitmapInterpolationMode.MediumQuality => SKFilterQuality.Medium,
            BitmapInterpolationMode.HighQuality => SKFilterQuality.High,
            BitmapInterpolationMode.Default => SKFilterQuality.None,
            _ => throw new ArgumentOutOfRangeException(nameof(interpolationMode), interpolationMode, null),
        };
    }

    public static SKPoint ToSKPoint(this Point p)
    {
        return new SKPoint((float)p.X, (float)p.Y);
    }

    public static SKPoint ToSKPoint(this Vector p)
    {
        return new SKPoint((float)p.X, (float)p.Y);
    }

    public static SKRect ToSKRect(this Rect r)
    {
        return new SKRect((float)r.X, (float)r.Y, (float)r.Right, (float)r.Bottom);
    }

    public static Rect ToGraphicsRect(this SKRect r)
    {
        return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    public static SKMatrix ToSKMatrix(this Matrix m)
    {
        var sm = new SKMatrix
        {
            ScaleX = (float)m.M11,
            SkewX = (float)m.M21,
            TransX = (float)m.M31,
            SkewY = (float)m.M12,
            ScaleY = (float)m.M22,
            TransY = (float)m.M32,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };

        return sm;
    }

    public static SKColor ToSKColor(this Color c)
    {
        return new SKColor(c.R, c.G, c.B, c.A);
    }

    public static SKShaderTileMode ToSKShaderTileMode(this GradientSpreadMethod m)
    {
        return m switch
        {
            GradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
            GradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
            _ => SKShaderTileMode.Clamp,
        };
    }
}
