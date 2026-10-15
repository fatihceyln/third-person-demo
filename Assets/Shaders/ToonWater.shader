Shader "ThirdPersonDemo/ToonWater"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.05, 0.62, 0.66, 1)
        _ShallowColor ("Shallow Color", Color) = (0.2, 0.88, 0.82, 1)
        _FoamColor ("Foam Color", Color) = (0.92, 1, 0.98, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Radius ("Radius", Range(0.1, 0.5)) = 0.42
        _EdgeWobble ("Edge Wobble", Range(0, 0.4)) = 0.12
        _WobbleSeed ("Wobble Seed", Range(0, 10)) = 1.7
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
        _WallColor ("Pit Wall Top Color", Color) = (0.68, 0.44, 0.32, 1)
        _WallDarkColor ("Pit Wall Bottom Color", Color) = (0.36, 0.22, 0.17, 1)
        _RimColor ("Pit Rim Color", Color) = (0.3, 0.18, 0.14, 1)
        _PitDepth ("Pit Depth", Range(0, 0.4)) = 0.1
        _WaterInset ("Water Inset", Range(0, 0.2)) = 0.06
        _RimWidth ("Rim Width", Range(0, 0.05)) = 0.012
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonWater"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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
                half _Opacity;
                half _Radius;
                half _EdgeWobble;
                half _WobbleSeed;
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
                half4 _WallColor;
                half4 _WallDarkColor;
                half4 _RimColor;
                half _PitDepth;
                half _WaterInset;
                half _RimWidth;
            CBUFFER_END

            // The GPU Resident Drawer reads material values from here, not from the cbuffer.
            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _DeepColor)
                    UNITY_DOTS_INSTANCED_PROP(float4, _ShallowColor)
                    UNITY_DOTS_INSTANCED_PROP(float4, _FoamColor)
                    UNITY_DOTS_INSTANCED_PROP(float, _Opacity)
                    UNITY_DOTS_INSTANCED_PROP(float, _Radius)
                    UNITY_DOTS_INSTANCED_PROP(float, _EdgeWobble)
                    UNITY_DOTS_INSTANCED_PROP(float, _WobbleSeed)
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
                    UNITY_DOTS_INSTANCED_PROP(float4, _WallColor)
                    UNITY_DOTS_INSTANCED_PROP(float4, _WallDarkColor)
                    UNITY_DOTS_INSTANCED_PROP(float4, _RimColor)
                    UNITY_DOTS_INSTANCED_PROP(float, _PitDepth)
                    UNITY_DOTS_INSTANCED_PROP(float, _WaterInset)
                    UNITY_DOTS_INSTANCED_PROP(float, _RimWidth)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define _DeepColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _DeepColor)
                #define _ShallowColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ShallowColor)
                #define _FoamColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FoamColor)
                #define _Opacity UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Opacity)
                #define _Radius UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Radius)
                #define _EdgeWobble UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _EdgeWobble)
                #define _WobbleSeed UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _WobbleSeed)
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
                #define _WallColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _WallColor)
                #define _WallDarkColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _WallDarkColor)
                #define _RimColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _RimColor)
                #define _PitDepth UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _PitDepth)
                #define _WaterInset UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _WaterInset)
                #define _RimWidth UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RimWidth)
            #endif

            // Steps used to find where the view ray meets the pit wall.
            #define PIT_MARCH_STEPS 8
            // Caps how far the view ray can slide sideways, so grazing camera angles do not smear the pit.
            #define PIT_MAX_SHIFT 0.45

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
                float3 viewOS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                // Object space is used so the pit follows the quad's rotation and position.
                output.viewOS = TransformWorldToObject(GetCameraPositionWS()) - input.positionOS.xyz;
                return output;
            }

            // Positive inside the outline, zero on it. Static on purpose: the pit must not move, only the water does.
            float OutlineDistance(float2 uv)
            {
                float2 centered = uv - 0.5;
                float angle = atan2(centered.y, centered.x);

                // Whole-number angle frequencies keep the outline seamless where atan2 wraps.
                float wobble = sin(angle * 3.0 + _WobbleSeed) * 0.5
                    + sin(angle * 5.0 + _WobbleSeed * 1.7) * 0.3
                    + sin(angle * 7.0 + _WobbleSeed * 2.3) * 0.2;
                float edgeRadius = _Radius * (1.0 + wobble * _EdgeWobble);

                return edgeRadius - length(centered);
            }

            // Small ripple along the waterline. It stays smaller than the water inset so the water never reaches the rim.
            float WaterRipple(float2 uv)
            {
                float2 centered = uv - 0.5;
                float angle = atan2(centered.y, centered.x);
                float phase = _Time.y * _WaterWobbleSpeed;
                float ripple = sin(angle * 3.0 + _WobbleSeed + phase) * 0.5
                    + sin(angle * 5.0 + _WobbleSeed * 1.7 - phase * 1.3) * 0.3
                    + sin(angle * 7.0 + _WobbleSeed * 2.3 + phase * 0.7) * 0.2;
                return ripple * _WaterWobble;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // The camera looks into the pit along this slide; the quad's local -Z is up.
                float2 shift = input.viewOS.xy / min(input.viewOS.z, -1e-3) * _PitDepth;
                shift *= min(1.0, PIT_MAX_SHIFT / max(length(shift), 1e-4));

                // The walls slope inward, so the water outline is smaller than the opening.
                float openingDistance = OutlineDistance(uv);
                float2 waterUV = uv + shift;
                float waterDistance = OutlineDistance(waterUV) - _WaterInset + WaterRipple(waterUV);

                // Screen-space width keeps the cartoon edges crisp at any distance.
                float openingWidth = max(fwidth(openingDistance), 1e-5);
                float waterWidth = max(fwidth(waterDistance), 1e-5);

                // Find how deep the view ray hits the wall; 1 means it reaches the water.
                float wallFraction = 1.0;
                bool wallFound = false;
                [unroll]
                for (int stepIndex = 1; stepIndex <= PIT_MARCH_STEPS; stepIndex++)
                {
                    float fraction = (float)stepIndex / PIT_MARCH_STEPS;
                    float rayDistance = OutlineDistance(uv + shift * fraction) - _WaterInset * fraction;
                    if (!wallFound && rayDistance <= 0.0)
                    {
                        wallFraction = fraction;
                        wallFound = true;
                    }
                }

                // Three flat bands, lighter at the top and darker near the water.
                float wallBand = floor(saturate(wallFraction) * 2.999) / 2.0;
                half3 wallColor = lerp(_WallColor.rgb, _WallDarkColor.rgb, wallBand);

                half3 waterColor = _DeepColor.rgb;
                waterColor = waterDistance < _FoamWidth + _ShallowWidth ? _ShallowColor.rgb : waterColor;
                waterColor = waterDistance < _FoamWidth ? _FoamColor.rgb : waterColor;

                // Diagonal bands that slide across the water; the sine bends them so they read as water.
                float2 waterCentered = waterUV - 0.5;
                float2 shineDirection = float2(0.7071, 0.7071);
                float2 shineSideways = float2(-0.7071, 0.7071);
                float shineCoord = dot(waterCentered, shineDirection) * _ShineScale
                    + sin(dot(waterCentered, shineSideways) * _ShineWaveFrequency + _Time.y * _ShineSpeed * 6.0) * _ShineWaveAmount
                    - _Time.y * _ShineSpeed;
                float shinePhase = frac(shineCoord);
                float shineEdge = max(fwidth(shineCoord), 1e-4);
                float shine = smoothstep(0.0, shineEdge, shinePhase)
                    * (1.0 - smoothstep(_ShineWidth, _ShineWidth + shineEdge, shinePhase));
                // Keep the foam rim clean.
                float shineArea = saturate((waterDistance - _FoamWidth) / waterWidth);
                waterColor = lerp(waterColor, _ShineColor.rgb, shine * shineArea * _ShineColor.a);

                float waterMask = saturate(waterDistance / waterWidth);
                half3 pitColor = lerp(wallColor, waterColor, waterMask);
                float pitAlpha = lerp(1.0, _Opacity, waterMask);

                // The rim is a thin outline just outside the opening that separates the pit from the floor.
                float openingMask = saturate(openingDistance / openingWidth);
                float rimMask = saturate((openingDistance + _RimWidth) / openingWidth);
                half3 color = lerp(_RimColor.rgb, pitColor, openingMask);
                float alpha = lerp(rimMask, pitAlpha, openingMask);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
