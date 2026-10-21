Shader "CetemVR/HologramURP"
{
    Properties
    {
        [HDR] _BaseColor ("Cor do Holograma", Color) = (0.0, 0.94, 1.0, 0.8)
        _RimPower ("Potência do Fresnel (Borda)", Range(0.5, 8.0)) = 2.0
        _RimIntensity ("Intensidade do Brilho de Borda", Range(0.5, 5.0)) = 2.2
        _AlphaMin ("Brilho Interno Suave", Range(0.0, 1.0)) = 0.25
        _PulseSpeed ("Velocidade do Pulso Suave", Range(0.0, 5.0)) = 1.5
        _PulseIntensity ("Intensidade do Pulso", Range(0.0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        Blend One One // Aditivo puro para visual holográfico brilhante sem tremor
        ZWrite Off
        Cull Off

        Pass
        {
            Name "HologramPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _RimPower;
                half _RimIntensity;
                half _AlphaMin;
                half _PulseSpeed;
                half _PulseIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float3 normalWS = normalize(input.normalWS);

                // Cálculo do Fresnel suave para o efeito holográfico de borda
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _RimPower) * _RimIntensity;

                // Pulsação suave e elegante (sem tremor nem scanlines agressivas)
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseIntensity;

                // Composição límpida e futurista
                float intensity = (fresnel + _AlphaMin) * pulse;
                half3 finalColor = _BaseColor.rgb * intensity;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
