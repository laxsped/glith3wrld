Shader "Custom/HDRP/ProximityGlitch"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _ColorA       ("Color A",            Color)  = (1, 0, 0.2, 1)
        [HDR] _ColorB       ("Color B",            Color)  = (0, 0.8, 1, 1)
        [HDR] _ColorC       ("Color C (bg)",       Color)  = (0, 0, 0, 0)

        [Header(Pixel Resolution)]
        _Resolution         ("Resolution (px)",    Range(4, 64)) = 32

        [Header(Glitch)]
        _GlitchSpeed        ("Glitch Speed",       Float)  = 8.0
        _GlitchScale        ("Glitch Block Scale", Float)  = 4.0
        _ShiftAmount        ("Horizontal Shift",   Float)  = 0.3

        [Header(Proximity)]
        _MaxDistance        ("Max Distance",       Float)  = 10.0
        _MinDistance        ("Min Distance",       Float)  = 1.0

        [Header(Direction)]
        // 0 = Horizontal, 1 = Vertical, 2 = Both, 3 = Radial
        [KeywordEnum(Horizontal, Vertical, Both, Radial)]
        _GlitchDir          ("Glitch Direction",   Float)  = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma shader_feature _GLITCHDIR_HORIZONTAL _GLITCHDIR_VERTICAL _GLITCHDIR_BOTH _GLITCHDIR_RADIAL

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            // ── Properties ─────────────────────────────────────────────────────
            float4 _ColorA, _ColorB, _ColorC;
            float  _Resolution;
            float  _GlitchSpeed, _GlitchScale, _ShiftAmount;
            float  _MaxDistance, _MinDistance;

            // Позиция игрока — устанавливается через скрипт
            float3 _PlayerWorldPos;

            // ── Structs ─────────────────────────────────────────────────────────
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            // ── Хэш-функции ────────────────────────────────────────────────────
            float hash1(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float hash1_f(float f)
            {
                return frac(sin(f * 127.1) * 43758.5453);
            }

            // ── Vertex ─────────────────────────────────────────────────────────
            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 ws    = TransformObjectToWorld(i.positionOS);
                o.positionCS = TransformWorldToHClip(ws);
                o.positionWS = ws;
                o.uv         = i.uv;
                return o;
            }

            // ── Fragment ───────────────────────────────────────────────────────
            float4 frag(Varyings i) : SV_Target
            {
                // ── Proximity ───────────────────────────────────────────────────
                float dist      = distance(i.positionWS, _PlayerWorldPos);
                float proximity = 1.0 - saturate((dist - _MinDistance) / (_MaxDistance - _MinDistance));
                float intensity = proximity; // 0 = далеко, 1 = близко

                // ── Пиксельный UV ───────────────────────────────────────────────
                float2 uv = i.uv;
                float2 pixelUV = floor(uv * _Resolution) / _Resolution;

                // ── Время для глитча ────────────────────────────────────────────
                float t = _Time.y * _GlitchSpeed;

                // ── Блочный глитч-шум ───────────────────────────────────────────
                // Разбиваем на горизонтальные (или вертикальные) полосы
                float2 blockCoord;

                #if defined(_GLITCHDIR_HORIZONTAL)
                    blockCoord = float2(floor(pixelUV.y * _GlitchScale), floor(t));
                #elif defined(_GLITCHDIR_VERTICAL)
                    blockCoord = float2(floor(pixelUV.x * _GlitchScale), floor(t));
                #elif defined(_GLITCHDIR_BOTH)
                    blockCoord = float2(
                        floor(pixelUV.x * _GlitchScale) + floor(pixelUV.y * _GlitchScale),
                        floor(t)
                    );
                #elif defined(_GLITCHDIR_RADIAL)
                    float2 center   = float2(0.5, 0.5);
                    float  angle    = atan2(pixelUV.y - center.y, pixelUV.x - center.x);
                    float  ring     = floor(length(pixelUV - center) * _GlitchScale * 2.0);
                    blockCoord      = float2(floor(angle * 4.0) + ring, floor(t));
                #else
                    blockCoord = float2(floor(pixelUV.y * _GlitchScale), floor(t));
                #endif

                // ── Шумы для сдвига и выбора цвета ─────────────────────────────
                float noiseShift  = hash1(blockCoord);
                float noiseColor  = hash1(blockCoord + 99.7);
                float noiseFlick  = hash1(blockCoord + 7.3 + floor(t * 3.0)); // быстрые мерцания

                // ── Применяем сдвиг пикселей ────────────────────────────────────
                float2 shiftedUV = pixelUV;

                #if defined(_GLITCHDIR_HORIZONTAL) || defined(_GLITCHDIR_BOTH)
                    shiftedUV.x += (noiseShift - 0.5) * _ShiftAmount * intensity;
                #endif
                #if defined(_GLITCHDIR_VERTICAL) || defined(_GLITCHDIR_BOTH)
                    shiftedUV.y += (noiseShift - 0.5) * _ShiftAmount * intensity;
                #endif
                #if defined(_GLITCHDIR_RADIAL)
                    float2 dir   = normalize(pixelUV - float2(0.5, 0.5) + 0.0001);
                    shiftedUV   += dir * (noiseShift - 0.5) * _ShiftAmount * intensity;
                #endif

                // ── Повторный пиксельный snap после сдвига ──────────────────────
                shiftedUV = floor(shiftedUV * _Resolution) / _Resolution;

                // ── Выбор цвета ─────────────────────────────────────────────────
                float4 col;
                if (noiseColor < 0.33)
                    col = _ColorA;
                else if (noiseColor < 0.66)
                    col = _ColorB;
                else
                    col = _ColorC;

                // ── Мерцание: часть блоков исчезает ────────────────────────────
                float visible = step(0.25, noiseFlick); // 75% блоков видны

                // ── Финальная прозрачность ──────────────────────────────────────
                // Без intensity — ничего нет; при intensity=1 — полный глитч
                float alpha = col.a * intensity * visible;

                // Если сдвинутый UV вышел за [0,1] — прозрачный
                alpha *= step(0.0, shiftedUV.x) * step(shiftedUV.x, 1.0)
                       * step(0.0, shiftedUV.y) * step(shiftedUV.y, 1.0);

                return float4(col.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
