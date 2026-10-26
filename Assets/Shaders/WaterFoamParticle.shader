Shader "ThirdPersonDemo/WaterFoamParticle"
{
    // A crisp disc or ring drawn from the particle quad's UVs, so the water foam and splashes need no texture.
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _RingInner ("Ring Inner Radius", Range(0, 0.95)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            // After the water (Transparent), so foam lying on the surface is never sorted under it.
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "WaterFoamParticle"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _RingInner;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2.0 - 1.0);
                float edge = max(fwidth(radius), 1e-4) * 1.5;
                float outer = 1.0 - smoothstep(1.0 - edge, 1.0, radius);
                float inner = _RingInner > 0.0 ? smoothstep(_RingInner - edge, _RingInner, radius) : 1.0;
                half alpha = outer * inner * input.color.a * _Color.a;
                return half4(input.color.rgb * _Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
