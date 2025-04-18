﻿using System.ComponentModel.DataAnnotations;
using System.Numerics;
using Beutl.Language;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Beutl.Graphics.Effects;

public sealed class LutEffect : FilterEffect
{
    public static readonly CoreProperty<FileInfo?> SourceProperty;
    public static readonly CoreProperty<float> StrengthProperty;

    private static readonly ILogger<LutEffect> s_logger =
        BeutlApplication.Current.LoggerFactory.CreateLogger<LutEffect>();

    private static readonly SKRuntimeEffect? s_runtimeEffect;
    private FileInfo? _source;
    private float _strength = 100;
    private CubeFile? _cube;

    static LutEffect()
    {
        SourceProperty = ConfigureProperty<FileInfo?, LutEffect>(nameof(Source))
            .Accessor(o => o.Source, (o, v) => o.Source = v)
            .Register();

        StrengthProperty = ConfigureProperty<float, LutEffect>(nameof(Strength))
            .Accessor(o => o.Strength, (o, v) => o.Strength = v)
            .DefaultValue(100)
            .Register();

        AffectsRender<LutEffect>(SourceProperty, StrengthProperty);

        // https://shizenkarasuzon.hatenablog.com/entry/2020/08/13/185223
        string sksl =
            """
            uniform shader src;
            // 横に長いシェーダー指定
            uniform shader lut;
            uniform int lutSize;
            uniform float strength;

            int modInt(int a, int b) {
                return a - b * (a / b);
            }

            float3 trilinear_interpolate(float4 color)
            {
                int3 pos; // 0~33
                float3 delta; //
                int lutSize2 = lutSize * lutSize;

                pos.x = int(clamp((color.r * 255.0) * float(lutSize) / 256.0, 0, 255));
                pos.y = int(clamp((color.g * 255.0) * float(lutSize) / 256.0, 0, 255));
                pos.z = int(clamp((color.b * 255.0) * float(lutSize) / 256.0, 0, 255));

                // 小数点部分
                delta.x = ((color.r * 255.0) * float(lutSize) / 256.0) - float(pos.x);
                delta.y = ((color.g * 255.0) * float(lutSize) / 256.0) - float(pos.y);
                delta.z = ((color.b * 255.0) * float(lutSize) / 256.0) - float(pos.z);

                float3 vertex_color_0, vertex_color_1, vertex_color_2, vertex_color_3, vertex_color_4, vertex_color_5, vertex_color_6, vertex_color_7;
                float3 surf_color_0, surf_color_1, surf_color_2, surf_color_3;
                float3 line_color_0, line_color_1;
                float3 out_color;

                int index = pos.x + pos.y * lutSize + pos.z * lutSize2;

                int next_index_0 = 1;
                int next_index_1 = lutSize;
                int next_index_2 = lutSize2;

                if (modInt(index, lutSize) == lutSize - 1)
                {
                    next_index_0 = 0;
                }
                if (modInt(index / lutSize, lutSize) == lutSize - 1)
                {
                    next_index_1 = 0;
                }
                if (modInt(index / lutSize2, lutSize) == lutSize - 1)
                {
                    next_index_2 = 0;
                }

                // https://en.wikipedia.org/wiki/Trilinear_interpolation
                vertex_color_0 = float3(lut.eval(float2(index, 0)).rgb);
                vertex_color_1 = float3(lut.eval(float2(index + next_index_0, 0)).rgb);
                vertex_color_2 = float3(lut.eval(float2(index + next_index_0 + next_index_1, 0)).rgb);
                vertex_color_3 = float3(lut.eval(float2(index + next_index_1, 0)).rgb);
                vertex_color_4 = float3(lut.eval(float2(index + next_index_2, 0)).rgb);
                vertex_color_5 = float3(lut.eval(float2(index + next_index_0 + next_index_2, 0)).rgb);
                vertex_color_6 = float3(lut.eval(float2(index + next_index_0 + next_index_1 + next_index_2, 0)).rgb);
                vertex_color_7 = float3(lut.eval(float2(index + next_index_1 + next_index_2, 0)).rgb);

                surf_color_0 = vertex_color_0 * (1.0 - delta.z) + vertex_color_4 * delta.z;
                surf_color_1 = vertex_color_1 * (1.0 - delta.z) + vertex_color_5 * delta.z;
                surf_color_2 = vertex_color_2 * (1.0 - delta.z) + vertex_color_6 * delta.z;
                surf_color_3 = vertex_color_3 * (1.0 - delta.z) + vertex_color_7 * delta.z;

                line_color_0 = surf_color_0 * (1.0 - delta.x) + surf_color_1 * delta.x;
                line_color_1 = surf_color_3 * (1.0 - delta.x) + surf_color_2 * delta.x;

                out_color = line_color_0 * (1.0 - delta.y) + line_color_1 * delta.y;

                return out_color;
            }

            float4 mix_strength(float3 color, float4 original) {
                float4 newColor;

                newColor.r = color.r * strength + (original.r * (1.0 - strength));
                newColor.g = color.g * strength + (original.g * (1.0 - strength));
                newColor.b = color.b * strength + (original.b * (1.0 - strength));
                newColor.a = original.a;

                return newColor;
            }

            half4 main(float2 fragCoord) {
                // 入力画像から色を取得
                float4 c = float4(src.eval(fragCoord));

                float3 newColor = trilinear_interpolate(c);

                return half4(mix_strength(newColor, c));
            }
            """;

        s_runtimeEffect = SKRuntimeEffect.CreateShader(sksl, out string? errorText);
        if (errorText is not null)
        {
            s_logger.LogError("Failed to compile SKSL: {ErrorText}", errorText);
        }
    }

    public FileInfo? Source
    {
        get => _source;
        set
        {
            if (SetAndRaise(SourceProperty, ref _source, value))
            {
                OnSourceChanged(value);
            }
        }
    }

    private void OnSourceChanged(FileInfo? value)
    {
        _cube = null;
        if (value != null)
        {
            using FileStream stream = value.OpenRead();
            try
            {
                _cube = CubeFile.FromStream(stream);
            }
            catch (Exception ex)
            {
                s_logger.LogError(ex, "Cubeファイルの解析に失敗しました。{FileName}", value.FullName);
            }
        }
    }

    [Display(Name = nameof(Strings.Strength), ResourceType = typeof(Strings))]
    [Range(0, 100)]
    public float Strength
    {
        get => _strength;
        set => SetAndRaise(StrengthProperty, ref _strength, value);
    }

    public override void ApplyTo(FilterEffectContext context)
    {
        if (_cube != null)
        {
            if (_cube.Dimention == CubeFileDimension.OneDimension)
            {
                context.LookupTable(
                    _cube,
                    _strength / 100,
                    (CubeFile cube, (byte[] A, byte[] R, byte[] G, byte[] B) data) =>
                    {
                        LookupTable.Linear(data.A);
                        cube.ToLUT(1, data.R, data.G, data.B);
                    });
            }
            else
            {
                context.CustomEffect((_cube, _strength / 100), OnApply3DLUT_GPU, (_, r) => r);
            }
        }
    }

    private void OnApply3DLUT_GPU((CubeFile, float) data, CustomFilterEffectContext c)
    {
        for (int i = 0; i < c.Targets.Count; i++)
        {
            EffectTarget effectTarget = c.Targets[i];
            var renderTarget = effectTarget.RenderTarget!;

            using var image = renderTarget.Value.Snapshot();
            using var baseShader = SKShader.CreateImage(image);

            // SKRuntimeShaderBuilderを作成して、child shaderとuniformを設定
            var builder = new SKRuntimeShaderBuilder(s_runtimeEffect);

            using var lutImage = SKImage.Create(new SKImageInfo(data.Item1.Data.Length, 1, SKColorType.RgbaF32));
            using (var pixmap = lutImage.PeekPixels())
            {
                var span = pixmap.GetPixelSpan<Vector4>();
                for (int j = 0; j < data.Item1.Data.Length; j++)
                {
                    var color = data.Item1.Data[j];
                    span[j] = new Vector4(color, 1);
                }
            }
            using var lutShader = SKShader.CreateImage(lutImage);

            // child shaderとしてテクスチャ用のシェーダーを設定
            builder.Children["src"] = baseShader;
            builder.Children["lut"] = lutShader;
            builder.Uniforms["lutSize"] = data.Item1.Size;
            builder.Uniforms["strength"] = data.Item2;

            // 最終的なシェーダーを生成
            using (SKShader finalShader = builder.Build())
            using (var paint = new SKPaint())
            {
                var newTarget = c.CreateTarget(effectTarget.Bounds);
                var canvas = newTarget.RenderTarget!.Value.Canvas;
                paint.Shader = finalShader;
                canvas.DrawRect(new SKRect(0, 0, effectTarget.Bounds.Width, effectTarget.Bounds.Height), paint);

                c.Targets[i] = newTarget;
            }

            effectTarget.Dispose();
        }
    }
}
