// A 3D surface drawn in chalk on the blackboard: the face itself is bare board, and the light lays chalk on it in
// hatching strokes. Strokes are fixed to the world (triplanar, by the face's main axis), so walking around does not
// make them swim; the brighter the face, the more strokes show up, and the brightest faces get a cross-hatch.
// The outlines are drawn over everything by Chalk/Lines (the renderer's full-screen pass), not here.
// _Local = 1 (things carried about: the samples, the stamp): the strokes are fixed to the object and lit from a fixed
// side instead, so a thing in the hand does not swim with strokes or flicker as it turns with the eyes.
Shader "Chalk/Lab"
{
    Properties
    {
        _Chalk ("Chalk colour", Color) = (0.93, 0.93, 0.89, 1)
        _Board ("Board colour", Color) = (0.105, 0.14, 0.125, 1)
        _Fill ("Chalk on a lit face", Range(0, 1.5)) = 0.6
        _Hatch ("Strokes per metre", Float) = 60
        _Angle ("Stroke angle, radians", Float) = 0.75
        _Smudge ("Rubbed chalk", Range(0, 1)) = 0.2
        _Ink ("Strength of a stroke", Range(0, 1)) = 0.85
        _Glow ("Highlight", Range(0, 1)) = 0
        _Local ("Strokes fixed to the object", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Chalk;
            half4 _Board;
            half _Fill;
            float _Hatch;
            float _Angle;
            half _Smudge;
            half _Ink;
            half _Glow;
            half _Local;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionOS : TEXCOORD2; float3 normalOS : TEXCOORD3; };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            // in the object's own axes but in metres: the strokes as dense as on anything else
            float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
            o.positionOS = v.positionOS.xyz * scale;
            o.normalOS = v.normalOS;
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 3; k++) { v += a * Noise(p); p = float2(1.6 * p.x + 1.2 * p.y, -1.2 * p.x + 1.6 * p.y); a *= 0.5; }
                return v;
            }

            // One layer of hatching across uv at `angle`, the way a hand shades with chalk: rows of short strokes laid
            // down in patches. A patch is where a soft noise of the face is under `tone`, so a brighter face gets more
            // and bigger patches; every stroke decides as a whole, so strokes are never cut by a patch's edge.
            // Fades to its mean when the rows get thinner than a pixel.
            float Strokes(float2 uv, float angle, float tone, float seed)
            {
                float s = sin(angle), c = cos(angle);
                float2 r = float2(c * uv.x - s * uv.y, s * uv.x + c * uv.y) * _Hatch;
                float row = floor(r.y);
                float shift = Hash(float2(row, seed)) * 7;
                float along = r.x / 6 + shift;
                float piece = floor(along);
                float2 mid = float2((piece + 0.5 - shift) * 6, row + 0.5) / _Hatch;
                float2 at = float2(c * mid.x + s * mid.y, -s * mid.x + c * mid.y);
                float jitter = (Hash(float2(row * 1.37 + piece, seed)) - 0.5) * 0.3;
                float on = step(Fbm(at * 1.7 + seed) * 1.25 - 0.12 + jitter, tone);
                float t = frac(along);
                float len = 0.6 + 0.35 * Hash(float2(piece, row + seed));
                float taper = smoothstep(0, 0.06, t) * (1 - smoothstep(len - 0.1, len, t));
                float y = frac(r.y) - 0.5 + (Hash(float2(piece + 3.1, row)) - 0.5) * 0.25;
                float width = (0.18 + 0.2 * Noise(float2(r.x * 0.4, row * 3.1 + seed))) * taper;
                float aa = fwidth(r.y);
                float body = 1 - smoothstep(width * 0.5 - aa * 0.5, width * 0.5 + aa * 0.5, abs(y));
                return lerp(body * on, saturate(tone) * 0.2, saturate(aa * 1.5 - 0.3));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                bool local = _Local > 0.5;
                float3 n = local ? normalize(i.normalOS) : normalize(i.normalWS);
                Light light = GetMainLight();
                float ndl = saturate(dot(n, local ? normalize(float3(0.35, 0.8, -0.45)) : light.direction));
                float facing = local ? 0.6 : saturate(dot(n, normalize(_WorldSpaceCameraPos - i.positionWS)));
                // the sun's strength (the day goes on: it is weaker in the morning and at dusk) lays more or less chalk
                float sunlit = dot(light.color, float3(0.3, 0.5, 0.2));
                float tone = saturate(ndl * 0.6 * sunlit + (n.y * 0.5 + 0.5) * 0.25 * saturate(sunlit + 0.2) + facing * 0.2) * _Fill + _Glow * 0.35;

                float3 a = abs(n);
                float3 p = local ? i.positionOS : i.positionWS;
                float2 uv = a.x > a.y && a.x > a.z ? p.zy : (a.y > a.z ? p.xz : p.xy);

                float hatch = Strokes(uv, _Angle, tone, 1.7);
                hatch = max(hatch, Strokes(uv, _Angle + 1.35, (tone - 0.65) * 2.2, 5.3));
                float smudge = saturate(Fbm(uv * 1.3) * 1.8 - 0.55) * _Smudge * tone;
                float grain = Hash(floor(i.positionCS.xy));
                float cover = saturate(max(hatch * _Ink, smudge)) * (0.7 + 0.3 * grain);

                // far things fade into the board, the way a drawing gets lighter in the distance
                float far = saturate((length(i.positionWS - _WorldSpaceCameraPos) - 4) / 8);
                cover *= 1 - far * 0.5;
                return half4(lerp(_Board.rgb, _Chalk.rgb, cover), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5);
                    return half4(PackFloat2To888(oct), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
