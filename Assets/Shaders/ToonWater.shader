Shader "ThirdPersonDemo/ToonWater"
{
    // Flat toon water after https://roystan.net/articles/toon-water/: a depth color gradient with scrolling,
    // distorted noise lines that thicken into foam near the shore. The noise is generated in the shader, in world
    // space, so the pond and the well share its scale and need no textures.
    // Needs the URP asset's Depth Texture.
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.325, 0.807, 0.971, 0.725)
        _DeepColor ("Deep Color", Color) = (0.086, 0.407, 1, 0.749)
        _DepthMaxDistance ("Depth For Deep Color (m)", Range(0.05, 2)) = 0.5
        _NoiseScale ("Noise Scale (per m)", Vector) = (1.2, 3, 0, 0)
        _SurfaceNoiseCutoff ("Surface Noise Cutoff", Range(0, 1)) = 0.777
        _SurfaceNoiseScroll ("Surface Noise Scroll", Vector) = (0.03, 0.03, 0, 0)
        _DistortionScale ("Distortion Scale (per m)", Range(0.1, 10)) = 1.5
        _DistortionAmount ("Distortion Amount", Range(0, 1)) = 0.27
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamDistance ("Foam Distance (m)", Range(0, 1)) = 0.25
        _ContactFoamDistance ("Contact Foam Distance (m)", Range(0, 1)) = 0.4
        _ContactRadius ("Contact Radius (m)", Range(0.1, 2)) = 0.7
        _RippleColor ("Ripple Color", Color) = (1, 1, 1, 0.8)
        _RippleWidth ("Ripple Width (m)", Range(0.01, 0.3)) = 0.05
        _RippleBreakup ("Ripple Breakup", Range(0, 0.9)) = 0.35
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.35
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

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _DepthMaxDistance;
                float4 _NoiseScale;
                half _SurfaceNoiseCutoff;
                float4 _SurfaceNoiseScroll;
                half _DistortionScale;
                half _DistortionAmount;
                half4 _FoamColor;
                half _FoamDistance;
                half _ContactFoamDistance;
                half _ContactRadius;
                half4 _RippleColor;
                half _RippleWidth;
                half _RippleBreakup;
                half _ShadowStrength;
            CBUFFER_END

            // The GPU Resident Drawer reads material values from here, not from the cbuffer.
            #ifdef UNITY_DOTS_INSTANCING_ENABLED
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _ShallowColor)
                    UNITY_DOTS_INSTANCED_PROP(float4, _DeepColor)
                    UNITY_DOTS_INSTANCED_PROP(float, _DepthMaxDistance)
                    UNITY_DOTS_INSTANCED_PROP(float4, _NoiseScale)
                    UNITY_DOTS_INSTANCED_PROP(float, _SurfaceNoiseCutoff)
                    UNITY_DOTS_INSTANCED_PROP(float4, _SurfaceNoiseScroll)
                    UNITY_DOTS_INSTANCED_PROP(float, _DistortionScale)
                    UNITY_DOTS_INSTANCED_PROP(float, _DistortionAmount)
                    UNITY_DOTS_INSTANCED_PROP(float4, _FoamColor)
                    UNITY_DOTS_INSTANCED_PROP(float, _FoamDistance)
                    UNITY_DOTS_INSTANCED_PROP(float, _ContactFoamDistance)
                    UNITY_DOTS_INSTANCED_PROP(float, _ContactRadius)
                    UNITY_DOTS_INSTANCED_PROP(float4, _RippleColor)
                    UNITY_DOTS_INSTANCED_PROP(float, _RippleWidth)
                    UNITY_DOTS_INSTANCED_PROP(float, _RippleBreakup)
                    UNITY_DOTS_INSTANCED_PROP(float, _ShadowStrength)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define _ShallowColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ShallowColor)
                #define _DeepColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _DeepColor)
                #define _DepthMaxDistance UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DepthMaxDistance)
                #define _NoiseScale UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _NoiseScale)
                #define _SurfaceNoiseCutoff UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _SurfaceNoiseCutoff)
                #define _SurfaceNoiseScroll UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SurfaceNoiseScroll)
                #define _DistortionScale UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DistortionScale)
                #define _DistortionAmount UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DistortionAmount)
                #define _FoamColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _FoamColor)
                #define _FoamDistance UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _FoamDistance)
                #define _ContactFoamDistance UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ContactFoamDistance)
                #define _ContactRadius UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ContactRadius)
                #define _RippleColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _RippleColor)
                #define _RippleWidth UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RippleWidth)
                #define _RippleBreakup UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RippleBreakup)
                #define _ShadowStrength UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShadowStrength)
            #endif

            // Set by WaterRipples.cs. Must match MaxRipples there.
            #define WATER_RIPPLE_COUNT 16
            // xy: world XZ where the ripple started, z: start time.
            float4 _WaterRipples[WATER_RIPPLE_COUNT];
            // x: current time, y: ripple lifetime, z: ripple speed. Zero outside Play Mode, which hides all ripples.
            float4 _WaterRippleSettings;
            // xy: world XZ of the player, z: how much the player is in the water (0..1).
            float4 _WaterContact;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // Shader Graph's Gradient Noise, standing in for the article's Perlin noise texture.
            float2 GradientNoiseDirection(float2 position)
            {
                position = position % 289.0;
                float hash = (34.0 * position.x + 1.0) * position.x % 289.0 + position.y;
                hash = (34.0 * hash + 1.0) * hash % 289.0;
                hash = frac(hash / 41.0) * 2.0 - 1.0;
                return normalize(float2(hash - floor(hash + 0.5), abs(hash) - 0.5));
            }

            float GradientNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 local = frac(position);
                float d00 = dot(GradientNoiseDirection(cell), local);
                float d01 = dot(GradientNoiseDirection(cell + float2(0.0, 1.0)), local - float2(0.0, 1.0));
                float d10 = dot(GradientNoiseDirection(cell + float2(1.0, 0.0)), local - float2(1.0, 0.0));
                float d11 = dot(GradientNoiseDirection(cell + float2(1.0, 1.0)), local - float2(1.0, 1.0));
                local = local * local * local * (local * (local * 6.0 - 15.0) + 10.0);
                return lerp(lerp(d00, d01, local.y), lerp(d10, d11, local.y), local.x) + 0.5;
            }

            // Lays top over bottom with normal alpha blending, as in the article.
            half4 AlphaBlend(half4 top, half4 bottom)
            {
                half3 color = top.rgb * top.a + bottom.rgb * (1.0 - top.a);
                half alpha = top.a + bottom.a * (1.0 - top.a);
                return half4(color / max(alpha, 1e-4), alpha);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = -TransformWorldToView(input.positionWS).z;
                float waterDepth = max(sceneDepth - surfaceDepth, 0.0);
                half4 waterColor = lerp(_ShallowColor, _DeepColor, saturate(waterDepth / _DepthMaxDistance));

                // The foam band is wider around the player's legs and breathes a little, so wading reads as moving water.
                float2 contactOffset = input.positionWS.xz - _WaterContact.xy;
                float contactFactor = _WaterContact.z * (1.0 - smoothstep(_ContactRadius * 0.5, _ContactRadius, length(contactOffset)));
                float contactFoamDistance = _ContactFoamDistance
                    * (0.8 + 0.2 * sin(_WaterRippleSettings.x * 4.0 + atan2(contactOffset.y, contactOffset.x) * 5.0));
                float foamDistance = lerp(_FoamDistance, contactFoamDistance, contactFactor);

                // Shallow water lowers the cutoff, so the thin noise lines merge into foam at the shore.
                float foamFactor = saturate(waterDepth / max(foamDistance, 1e-4));
                float cutoff = foamFactor * _SurfaceNoiseCutoff;

                float2 distortionPosition = input.positionWS.xz * _DistortionScale;
                float2 distortion = float2(GradientNoise(distortionPosition), GradientNoise(distortionPosition + 17.3)) * 2.0 - 1.0;
                float2 noisePosition = input.positionWS.xz * _NoiseScale.xy + _Time.y * _SurfaceNoiseScroll.xy + distortion * _DistortionAmount;
                float noise = GradientNoise(noisePosition);
                float edge = max(fwidth(noise), 1e-4);
                half foam = smoothstep(cutoff - edge, cutoff + edge, noise);

                half4 color = AlphaBlend(half4(_FoamColor.rgb, _FoamColor.a * foam), waterColor);

                // Rings that spread out from where the player walked.
                float ripple = 0.0;
                [unroll]
                for (int rippleIndex = 0; rippleIndex < WATER_RIPPLE_COUNT; rippleIndex++)
                {
                    float4 rippleData = _WaterRipples[rippleIndex];
                    float age = _WaterRippleSettings.x - rippleData.z;
                    if (age >= 0.0 && age < _WaterRippleSettings.y)
                    {
                        float radius = age * _WaterRippleSettings.z;
                        float2 rippleOffset = input.positionWS.xz - rippleData.xy;
                        float distanceToRing = abs(length(rippleOffset) - radius);
                        float ring = 1.0 - smoothstep(_RippleWidth * 0.5, _RippleWidth, distanceToRing);
                        // Drop some arcs, picked per ripple by its start time, so the rings read as broken foam.
                        float arc = floor((atan2(rippleOffset.y, rippleOffset.x) / TWO_PI + 0.5) * 10.0);
                        float arcKeep = frac(sin(arc * 78.233 + rippleData.z * 12.9898) * 43758.5453);
                        ring *= step(_RippleBreakup, arcKeep);
                        ripple = max(ripple, ring * (1.0 - age / _WaterRippleSettings.y));
                    }
                }
                color = AlphaBlend(half4(_RippleColor.rgb, _RippleColor.a * ripple), color);

                // Unlit like the article, but shadows still darken it so the water does not glow under trees.
                half shadow = GetMainLight(TransformWorldToShadowCoord(input.positionWS)).shadowAttenuation;
                color.rgb *= lerp(1.0, shadow, _ShadowStrength);
                return color;
            }
            ENDHLSL
        }
    }
}
