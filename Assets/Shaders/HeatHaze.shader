// Full-screen heat distortion. Vert/_BlitTexture come from Core RP's Blit.hlsl (used by AddBlitPass).
Shader "Hidden/MineEmbers/HeatHaze"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "HeatHaze"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _HazeStrength;
            float4 _HazeParams;  // x = speed, y = scale, z = frozen time (< 0: use _Time)

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float t = (_HazeParams.z >= 0 ? _HazeParams.z : _Time.y) * _HazeParams.x;
                // Rising shimmer: noise scrolls upward; stronger in the lower half of the screen,
                // where the lava and furnace usually are.
                float2 p = uv * _HazeParams.y + float2(0, -t);
                float2 offs = float2(noise(p) - 0.5, noise(p + 17.3) - 0.5);
                float mask = saturate(1.2 - uv.y) * saturate(uv.y * 8.0);
                uv += offs * _HazeStrength * mask;
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }
            ENDHLSL
        }
    }
}
