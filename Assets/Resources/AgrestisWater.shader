Shader "Agrestis/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Colour", Color)      = (0.25,0.65,0.75,0.55)
        _DeepColor    ("Deep Colour",    Color)      = (0.05,0.22,0.38,0.9)
        _WaveAmp      ("Wave Amplitude", Range(0,1)) = 0.12
        _WaveFreq     ("Wave Frequency", Range(0,4)) = 0.55
        _WaveSpeed    ("Wave Speed",     Range(0,4)) = 1.1
        _SpecPower    ("Specular Power", Range(1,128)) = 48
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float  _WaveAmp;
                float  _WaveFreq;
                float  _WaveSpeed;
                float  _SpecPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            float WaveHeight(float2 p, float t, out float3 normalWS)
            {
                float w1 = sin(p.x * _WaveFreq + t * _WaveSpeed);
                float w2 = cos(p.y * _WaveFreq * 1.37 + t * _WaveSpeed * 0.81);
                float h  = (w1 + w2) * 0.5 * _WaveAmp;

                float dx = cos(p.x * _WaveFreq + t * _WaveSpeed) * _WaveFreq * 0.5 * _WaveAmp;
                float dz = -sin(p.y * _WaveFreq * 1.37 + t * _WaveSpeed * 0.81) * _WaveFreq * 1.37 * 0.5 * _WaveAmp;
                normalWS = normalize(float3(-dx, 1.0, -dz));
                return h;
            }

            Varyings Vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                float3 n;
                positionWS.y += WaveHeight(positionWS.xz, _Time.y, n);

                OUT.positionWS = positionWS;
                OUT.normalWS   = n;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                float3 normalWS  = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                Light mainLight = GetMainLight();

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 2.0h);
                half4 baseColor = lerp(_ShallowColor, _DeepColor, fresnel);

                half ndotl = saturate(dot(normalWS, mainLight.direction)) * 0.6h + 0.4h;
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), _SpecPower);

                half3 color = baseColor.rgb * mainLight.color * ndotl
                            + SampleSH(normalWS) * baseColor.rgb * 0.5h
                            + mainLight.color * spec;

                color = MixFog(color, IN.fogFactor);
                return half4(color, baseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
