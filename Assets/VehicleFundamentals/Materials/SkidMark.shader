Shader "Vehicle Fundamentals/Skid Mark"
{
    Properties
    {
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-10" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fog : TEXCOORD1;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Grain(float2 uv)
            {
                float2 p = floor(uv), f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(p), Hash(p + float2(1, 0)), f.x),
                    lerp(Hash(p + float2(0, 1)), Hash(p + 1), f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float edge = min(input.uv.x, 1.0 - input.uv.x);
                float feather = smoothstep(0, max(0.12, fwidth(input.uv.x)), edge);
                float tread = 0.86 + 0.14 * sin(input.uv.x * 31.4159);
                float grain = lerp(0.65, 1.0, Grain(input.uv * float2(22, 32)));
                half alpha = input.color.a * feather * tread * grain;
                return half4(MixFog(input.color.rgb, input.fog), alpha);
            }
            ENDHLSL
        }
    }
}
