// Lit, GPU-instanced body parts for the crowd. Each instance carries its own color (skin, hair, clothes),
// so a whole dance floor of different-looking people draws in a handful of calls.
Shader "DJMaster/CrowdInstanced"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.25
        _Rim ("Rim light from the stage", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard noforwardadd nolightmap nodynlightmap nodirlightmap
        #pragma multi_compile_instancing
        #pragma target 3.0

        struct Input { float3 viewDir; };

        half _Glossiness, _Rim;

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            // A soft rim keeps silhouettes readable in a dark club, like stage light catching people's edges.
            half rim = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = c.rgb * _Rim * rim * rim;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
