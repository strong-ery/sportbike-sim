Shader "Custom/Raindrop_GoPro_Foggy" {
    Properties {
        iChannel0("Albedo (RGB)", 2D) = "white" {}
        _RainAmount("Rain Amount", Range(0, 1)) = 0.5
        _Speed("Speed", Range(0.0, 5.0)) = 1.0
        _StreakStretch("Streak Length", Range(1.0, 50.0)) = 10.0
        _DropSize("Drop Scale", Range(0.1, 5.0)) = 1.0
        _FogOpacity("Fog/Condensation", Range(0.0, 1.0)) = 0.5
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Pass{
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D iChannel0;
            float _RainAmount;
            float _Speed;
            float _StreakStretch;
            float _DropSize;
            float _FogOpacity;

            #define S(a, b, t) smoothstep(a, b, t)

            // --- NOISE FUNCTIONS ---
            float3 N13(float p) {
                float3 p3 = frac(float3(p, p, p) * float3(.1031, .11369, .13787));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac(float3((p3.x + p3.y)*p3.z, (p3.x + p3.z)*p3.y, (p3.y + p3.z)*p3.x));
            }

            float N(float t) {
                return frac(sin(t*12345.564)*7658.76);
            }

            float Saw(float b, float t) {
                return S(0., b, t)*S(1., b, t);
            }
            // -----------------------

            float2 DropLayer2(float2 uv, float t) {
                float2 UV = uv;

                uv.y += t*0.75;
                
                float2 a = float2(6., 1.);
                float2 grid = a*2.;
                float2 id = floor(uv*grid);

                float colShift = N(id.x);
                uv.y += colShift;

                id = floor(uv*grid);
                float3 n = N13(id.x*35.2 + id.y*2376.1);
                float2 st = frac(uv*grid) - float2(.5, 0);

                float x = n.x - .5;

                float y = UV.y*20.;
                
                float wiggle = sin(y + sin(y));
                x += wiggle*(.5 - abs(x))*(n.z - .5) * 0.5; 
                x *= .7;
                
                float ti = frac(t + n.z);
                y = (Saw(.85, ti) - .5)*.9 + .5;
                float2 p = float2(x, y);

                float d = length((st - p)*a.yx);

                float mainDrop = S(.4, .0, d);

                float r = sqrt(S(1., y, st.y));
                float cd = abs(st.x - x);
                float trail = S(.23*r, .15*r*r, cd);
                float trailFront = S(-.02, .02, st.y - y);
                trail *= trailFront*r*r;

                y = UV.y;
                float trail2 = S(.2*r, .0, cd);
                float droplets = max(0., (sin(y*(1. - y)*120.) - st.y))*trail2*trailFront*n.z;
                y = frac(y*10.) + (st.y - .5);
                float dd = length(st - float2(x, y));
                droplets = S(.3, 0., dd);
                float m = mainDrop + droplets*r*trailFront;

                return float2(m, trail);
            }

            float StaticDrops(float2 uv, float t) {
                uv *= 40.;

                float2 id = floor(uv);
                uv = frac(uv) - .5;
                float3 n = N13(id.x*107.45 + id.y*3543.654);
                float2 p = (n.xy - .5)*.7;
                float d = length(uv - p);

                float fade = Saw(.025, frac(t + n.z));
                float c = S(.3, 0., d)*frac(n.z*10.)*fade;
                return c;
            }

            float2 Drops(float2 uv, float t, float l0, float l1, float l2) {
                float s = StaticDrops(uv, t)*l0;
                float2 m1 = DropLayer2(uv, t)*l1;
                float2 m2 = DropLayer2(uv*1.85, t)*l2;

                float c = s + m1.x + m2.x;
                c = S(.3, 1., c);

                return float2(c, max(m1.y*l0, m2.y*l1));
            }

            fixed4 frag(v2f_img i) : SV_Target{
                // 1. Prepare Screen UVs
                float2 uvScreen = ((i.uv * _ScreenParams.xy) - .5*_ScreenParams.xy) / _ScreenParams.y;
                float2 UV = i.uv.xy;
                
                // 2. CONVERT TO POLAR COORDINATES (Streak Effect)
                float2 centered = UV - 0.5;
                float phi = atan2(centered.y, centered.x) / 6.28318; 
                float r = length(centered);

                float2 polarUV = float2(phi * 4.0, r); 
                polarUV.y /= _StreakStretch; 
                polarUV.y -= _Time.y * _Speed * 0.1; 
                
                polarUV *= _DropSize; 

                // 3. Calculate Drops in Polar Space
                float t = _Time.y * 0.2;
                float rainAmount = _RainAmount;
                float staticDrops = S(-.5, 1., rainAmount)*2.;
                float layer1 = S(.25, .75, rainAmount);
                float layer2 = S(.0, .5, rainAmount);

                float2 c = Drops(polarUV, t, staticDrops, layer1, layer2);

                // 4. Calculate Distortion Normals
                #ifdef CHEAP_NORMALS
                    float2 n = float2(dFdx(c.x), dFdy(c.x));
                #else
                    float2 e = float2(.001, 0.);
                    float cx = Drops(polarUV + e, t, staticDrops, layer1, layer2).x;
                    float cy = Drops(polarUV + e.yx, t, staticDrops, layer1, layer2).x;
                    float2 n = float2(cx - c.x, cy - c.x);
                #endif

                // --- FOG LOGIC START ---
                
                // Calculate "Clean Factor"
                // c.x is the drop itself, c.y is the trail behind it.
                // We combine them to find where the glass is WET (aka Clean).
                float isWet = saturate(c.x + c.y);

                // If it is NOT wet, it is Foggy.
                float fogFactor = (1.0 - isWet) * _FogOpacity;

                // 5. Calculate Blur based on Fog
                // If Foggy -> Blur is high (5.0)
                // If Wet   -> Blur is low (0.0)
                float blurLevel = lerp(0.0, 5.0, fogFactor);
                
                // 6. Sample Texture with Blur
                float2 texCoord = UV + n * 0.5; 
                float4 lod = tex2Dlod(iChannel0, float4(texCoord, 0, blurLevel));
                float3 col = lod.rgb;

                // 7. Apply Fog Color Overlay
                // We mix the image color with a milky white/grey based on fog density
                float3 fogColor = float3(0.9, 0.9, 0.95);
                col = lerp(col, fogColor, fogFactor * 0.8); // *0.8 prevents it from going purely white

                // --- FOG LOGIC END ---

                // Vignette
                col *= 1. - dot(UV -= .5, UV) * 0.8; 

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}