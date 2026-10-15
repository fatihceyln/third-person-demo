Shader "ThirdPersonDemo/ToonWaterSurface"
{
    // Flat toon water for a regular octagon whose UVs span the unit square (see Tools/agent/make-well.py).
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.03, 0.45, 0.52, 1)
        _ShallowColor ("Shallow Color", Color) = (0.045, 0.76, 0.87, 1)
        _FoamColor ("Foam Color", Color) = (0.92, 1, 0.98, 1)
        _Apothem ("Octagon Apothem (UV)", Range(0.3, 0.5)) = 0.4619
        _FoamWidth ("Foam Width", Range(0, 0.2)) = 0.03
        _ShallowWidth ("Shallow Width", Range(0, 0.3)) = 0.08
        _WaterWobbleSpeed ("Water Wobble Speed", Range(0, 3)) = 0.8
        _WaterWobble ("Water Wobble", Range(0, 0.05)) = 0.015
        _ShineColor ("Shine Color", Color) = (0.75, 1, 0.95, 1)
        _ShineScale ("Shine Scale", Range(1, 20)) = 5
        _ShineWidth ("Shine Width", Range(0, 0.5)) = 0.12
        _ShineSpeed ("Shine Speed", Range(0, 1)) = 0.12
        _ShineWaveAmount ("Shine Wave Amount", Range(0, 0.5)) = 0.12
        _ShineWaveFrequency ("Shine Wave Frequency", Range(0, 30)) = 9
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonWaterSurface"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                half _Apothem;
                half _FoamWidth;
                half _ShallowWidth;
                half _WaterWobbleSpeed;
                half _WaterWobble;
                half4 _ShineColor;
                half _ShineScale;
                half _ShineWidth;
                half _ShineSpeed;
                half _ShineWaveAmount;
                half _ShineWaveFrequency;
            CBUFFER_END

            // The GPU Resident Drawer reads material values from here, not from the cbuffer.
            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _DeepColor)
                    UNITY_DOTS_INSTANCED_PROP(float4, _ShallowColor)
                    UNITY_DOTS_INSTANCED_PROP(float4, _FoamColor)
                    UNITY_DOTS_INSTANCED_PROP(float, _Apothem)
                    UNITY_DOTS_INSTANCED_PROP(float, _FoamWidth)
                    UNITY_DOTS_INSTANCED_PROP(float, _ShallowWidth)
                    UNITY_DOTS_INSTANCED_PROP(float, _WaterWobbleSpeed)
                    UNITY_DOTS_INSTANCED_PROP(float, _WaterWobble)
                    UNITY_DOTS_INSTANCED_PROP(float4, _ShineColor)
                    UNITY_DOTS_INSTANCED_PROP(float, _ShineScale)
                    UNITY_DOTS_INSTANCED_PROP(float, _ShineWidth)
                    UNITY_DOTS_INSTANCED_PROP(float, _ShineSpeed)
                    UNITY_DOTS_INSTANCED_PROP(float, _ShineWaveAmount)
                    UNITY_DOTS_INSTANCED_PROP(float, _ShineWaveFrequency)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define _DeepColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _DeepColor)
                #define _ShallowColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ShallowColor)
                #define _FoamColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FoamColor)
                #define _Apothem UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Apothem)
                #define _FoamWidth UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FoamWidth)
                #define _ShallowWidth UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShallowWidth)
                #define _WaterWobbleSpeed UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _WaterWobbleSpeed)
                #define _WaterWobble UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _WaterWobble)
                #define _ShineColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ShineColor)
                #define _ShineScale UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShineScale)
                #define _ShineWidth UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShineWidth)
                #define _ShineSpeed UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShineSpeed)
                #define _ShineWaveAmount UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShineWaveAmount)
                #define _ShineWaveFrequency UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShineWaveFrequency)
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv - 0.5;

                // Distance to the octagon outline, whose flat sides face the UV axes and diagonals.
                float octagonRadius = max(max(abs(centered.x), abs(centered.y)), 0.7071 * (abs(centered.x) + abs(centered.y)));
                float insideDistance = _Apothem - octagonRadius;

                // The foam line along the wall breathes a little; whole-number angle frequencies keep it seamless.
                float angle = atan2(centered.y, centered.x);
                float phase = _Time.y * _WaterWobbleSpeed;
                float ripple = sin(angle * 3.0 + phase) * 0.5
                    + sin(angle * 5.0 - phase * 1.3) * 0.3
                    + sin(angle * 7.0 + phase * 0.7) * 0.2;
                insideDistance += ripple * _WaterWobble;

                half3 color = _DeepColor.rgb;
                color = insideDistance < _FoamWidth + _ShallowWidth ? _ShallowColor.rgb : color;
                color = insideDistance < _FoamWidth ? _FoamColor.rgb : color;

                // Diagonal bands that slide across the water; the sine bends them so they read as water.
                float2 shineDirection = float2(0.7071, 0.7071);
                float2 shineSideways = float2(-0.7071, 0.7071);
                float shineCoord = dot(centered, shineDirection) * _ShineScale
                    + sin(dot(centered, shineSideways) * _ShineWaveFrequency + _Time.y * _ShineSpeed * 6.0) * _ShineWaveAmount
                    - _Time.y * _ShineSpeed;
                float shinePhase = frac(shineCoord);
                float shineEdge = max(fwidth(shineCoord), 1e-4);
                float shine = smoothstep(0.0, shineEdge, shinePhase)
                    * (1.0 - smoothstep(_ShineWidth, _ShineWidth + shineEdge, shinePhase));
                // Keep the foam rim clean.
                float shineArea = saturate((insideDistance - _FoamWidth) / max(fwidth(insideDistance), 1e-5));
                color = lerp(color, _ShineColor.rgb, shine * shineArea * _ShineColor.a);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
