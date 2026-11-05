// The chalk outlines of the 3D lab, drawn over the whole picture by the renderer's full-screen pass: a line wherever
// the depth breaks (silhouettes) or the surface turns (creases). Depth is compared as 1/depth, which changes evenly
// across any flat face, so a floor seen at a grazing angle stays clean. The line wobbles a little and redraws itself
// a few times a second, like a hand going over it again, and its chalk breaks up into grain.
Shader "Chalk/Lines"
{
    Properties
    {
        _Line ("Line colour", Color) = (0.93, 0.93, 0.89, 1)
        _Width ("Width, pixels", Float) = 1.3
        _DepthCut ("Depth edge threshold", Float) = 0.06
        _NormalCut ("Crease threshold", Float) = 0.35
        _Wobble ("Wobble, pixels", Float) = 1.2
        _Boil ("Redraws per second", Float) = 5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ChalkLines"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _Line;
            float _Width, _DepthCut, _NormalCut, _Wobble, _Boil;

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            float InvDepth(float2 uv) { return 1.0 / max(LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams), 1e-3); }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 px = 1.0 / _ScreenParams.xy;
                float2 sp = uv * _ScreenParams.xy;

                float t = floor(_Time.y * _Boil);
                float2 wob = float2(Noise(sp / 37 + t * 7.31), Noise(sp / 37 + 19.7 + t * 3.17)) - 0.5;
                float2 c = uv + wob * 2 * _Wobble * px;
                float2 dx = float2(_Width, 0) * px, dy = float2(0, _Width) * px;

                float q0 = InvDepth(c);
                float ql = InvDepth(c - dx), qr = InvDepth(c + dx), qd = InvDepth(c - dy), qu = InvDepth(c + dy);
                float lap = (abs(ql + qr - 2 * q0) + abs(qd + qu - 2 * q0)) / max(q0, 1e-3);
                float depthEdge = smoothstep(_DepthCut, _DepthCut * 2, lap);

                float3 n0 = SampleSceneNormals(c);
                float turn = 4 - dot(n0, SampleSceneNormals(c - dx)) - dot(n0, SampleSceneNormals(c + dx))
                               - dot(n0, SampleSceneNormals(c - dy)) - dot(n0, SampleSceneNormals(c + dy));
                // where nothing was drawn (out of the windows) there are no normals to compare
                float raw = SampleSceneDepth(c);
                #if UNITY_REVERSED_Z
                    float drawn = step(1e-6, raw);
                #else
                    float drawn = step(raw, 1 - 1e-6);
                #endif
                float creaseEdge = smoothstep(_NormalCut, _NormalCut * 2, turn) * drawn;

                float edge = max(depthEdge, creaseEdge);
                float breaks = lerp(0.45, 1, smoothstep(0.25, 0.55, Noise(sp / 9 + t * 1.3)));
                float grain = 0.7 + 0.3 * Hash(floor(sp));
                edge *= breaks * grain;
                // lines far away are lighter, so the room recedes and the table stays the sharpest thing in view
                float far = saturate((1.0 / max(q0, 1e-3) - 4) / 10);
                edge *= 1 - far * 0.45;
                return half4(lerp(col.rgb, _Line.rgb, saturate(edge) * _Line.a), col.a);
            }
            ENDHLSL
        }
    }
}
