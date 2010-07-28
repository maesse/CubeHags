#include "shaderhelp.fx"
static float4 one = float4(1,1,1,1);
static float4 two = float4(2,2,2,2);
static float2 center = float2(0.5f,0.5f);
static float3 zero = float3(0,0,0);

// HDR rendering
float2 g_avSampleOffsets[16];
float4 g_avSampleWeights[16];
static const float3 LUMINANCE_VECTOR  = float3(0.2125f, 0.7154f, 0.0721f);
uniform const float  MIDDLE_GRAY = 0.5f;
uniform const float  LUM_WHITE = 0.82f;
uniform const float  BRIGHT_THRESHOLD = 0.9f;
uniform const float  bloomMulti = 0.6f;
const float fTau = 0.4f;

struct LightCube {
	float4 AmbientCubeX [ 2 ];
	float4 AmbientCubeY [ 2 ];
	float4 AmbientCubeZ [ 2 ];
};
const float3x3 ambientLight[64] : register(vs, c8);

sampler BaseTextureSampler	 : register(s0);
sampler LightmapSampler		 : register(s1);
sampler EnvmapSampler		 : register(s2);
sampler DetailSampler		 : register(s3);
sampler BumpmapSampler		 : register(s4);
sampler EnvmapMaskSampler	 : register(s5);
sampler NormalizeSampler	 : register(s6);

const float g_OverbrightFactor	: register( c6 );
const float4 g_SelfIllumTint	: register( c7 );

uniform const float invLogLumRange = 0.0f;
uniform const float logLumOffset = 0.0f;
uniform const float avgLogLum = 64.0f;
uniform const float detailMultiplier = 2.0f;

uniform float4x4 WorldViewProj;
uniform float4x4 World;
uniform float4x4 View;
uniform float4 Eye;
uniform float gamma = 2.2f;
uniform float fTimeElap;

static const bool g_bBaseTexture = true;
static const bool g_bDetailTexture = false;
static const bool g_bBumpmap = false;
static const bool g_bDiffuseBumpmap = false;
static const bool g_bCubemap = false;
static const bool g_bVertexColor = false;
static const bool g_bEnvmapMask = false;
static const bool g_bBaseAlphaEnvmapMask = false;
static const bool g_bSelfIllum = false;
static const bool g_bNormalMapAlphaEnvmapMask = false;
// IO Structures

struct VS_INPUT
{
	float4 pos : POSITION;
	float3 normal : NORMAL0;
	float2 texcoord : TEXCOORD0;
	float2 lightmap : TEXCOORD1;
};

struct POSTEX
{
	float4 pos : POSITION;
	float2 texcoord : TEXCOORD0;
};

struct POSTEXCOL
{
	float4 pos : POSITION;
	float2 texcoord : TEXCOORD0;
	float4 color : COLOR;
};

struct POSCOLOR
{
	float4 pos : POSITION;
	float4 color : COLOR;
};

struct POSNORTEX
{
	float4 pos : POSITION;
	float3 normal : NORMAL0;
	float2 texcoord : TEXCOORD0;
};

struct VS_INPUTTAN
{
	float4 pos : POSITION;
	float3 normal : NORMAL0;
	float2 texcoord : TEXCOORD0;
	float2 lightmap : TEXCOORD1;
	float4 tangent : TANGENT;
};

struct VS_POSCOLOR
{
	float4 pos : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 pos : POSITION;
	float2 texcoord : TEXCOORD0;
	float2 lightmap : TEXCOORD1;
	float3 normal : TEXCOORD2;
	float4 color : TEXCOORD3;
	float4 realpos : TEXCOORD4;
};

struct VS_OUTPUT_DIR
{
	float4 position : POSITION;
	float2 texCoord : TEXCOORD0;
	float3 halfVector : TEXCOORD1;
	float3 lightDir : TEXCOORD2;
	float2 lightmap : TEXCOORD3;
};


// Vertex Shaders
VS_OUT VertexNorTexLight( VS_INPUT input)
{
	VS_OUT Out = (VS_OUT)0;

	Out.pos = mul(input.pos, WorldViewProj).xyzw;
	Out.realpos = Out.pos;
	Out.normal = mul(input.normal, World).xyz;
	Out.texcoord = input.texcoord;
	Out.lightmap = input.lightmap;
	Out.color = float4(0.0f,0.0f,0.0f,0.0f);
	return Out;
}
// Vertex Shaders
VS_OUT VertexNorTexLight2( VS_INPUT input)
{
	VS_OUT Out = (VS_OUT)0;

	Out.pos = mul(input.pos, WorldViewProj).xyzw;
	Out.pos.z += 1.9f;
	Out.realpos = Out.pos;
	Out.normal = mul(input.normal, World).xyz;
	Out.texcoord = input.texcoord;
	Out.lightmap = input.lightmap;
	Out.color = float4(0.0f,0.0f,0.0f,0.0f);
	return Out;
}

float3 DoAmbientCube(const float3 worldNormal, const float inindex)
{
	float3 nSquared = worldNormal * worldNormal;
	int3 isNegative = ( worldNormal < 0.0 );
	int index = inindex*255;
	float3 linearColor;
	float3x3 cube = ambientLight[index*2];
	float3x3 cube2 = ambientLight[index*2+1];
	linearColor.xyz = nSquared.x * cube[isNegative.x] +
	              nSquared.z * cube2[2+isNegative.z];
	if(isNegative.y > 0.0) {
		linearColor.xyz += nSquared.y * cube[2];
	} else {
    	linearColor.xyz += nSquared.y * cube2[0];
	}

	// * cube.AmbientCubeX[0]
	//linearColor = nSquared.x * cube.AmbientCubeX[isNegative.x] + nSquared.y + nSquared.z;
	
	return linearColor;
}

// Converts from linear RGB space to sRGB.
float3 LinearToSRGB(in float3 color)
{
    return pow(abs(color), 1/2.2f);
}
// Converts from sRGB space to linear RGB.
float3 SRGBToLinear(in float3 color)
{
    return pow(abs(color), 2.2f);
}

VS_OUT VertexNorTexInstancing(float4 vPos : POSITION0,
								float3 vNormal : NORMAL,
								float2 vTexcoord : TEXCOORD0,
								float4 vInstrow1 : POSITION1,
								float4 vInstrow2 : POSITION2,
								float4 vInstrow3 : POSITION3,
								float3 cubex : TEXCOORD1,
								float3 cubex1 : TEXCOORD2,
								float3 cubey : TEXCOORD3,
								float3 cubey1 : TEXCOORD4,
								float3 cubez : TEXCOORD5,
								float3 cubez1 : TEXCOORD6 )
{
	VS_OUT Out;
	// Create modelview
	float4x3 modelview = float4x3(vInstrow1.xyz, vInstrow2.xyz, vInstrow3.xyz, float3(vInstrow1.w, vInstrow2.w, vInstrow3.w));

	vPos.xyz = mul(vPos, modelview);
	Out.pos = mul(vPos, WorldViewProj).xyzw;
	Out.realpos = Out.pos;
	Out.normal = mul(vNormal, modelview).xyz;
	Out.texcoord = vTexcoord;
	Out.lightmap = center;
	Out.normal = normalize(Out.normal);

	// Do ambient lighting
	float3 nSquared = Out.normal * Out.normal;
	float3 isNegative = (Out.normal < 0.0 );
	float3 isPositive = 1-isNegative;

	isNegative *= nSquared;
	isPositive *= nSquared;

	Out.color.rgb = isPositive.x * cubex1 + isNegative.x * cubex +
				  isPositive.y * cubey1 + isNegative.y * cubey +
				  isPositive.z * cubez + isNegative.z * cubez1;
	Out.color.a = 1.0f;
	
	//Out.color = zero;

	//if(Out.normal[0] < 0.0) {
	//	Out.color += nSquared.x * cubex;
	//} else
	//	Out.color += nSquared.x * cubex1;

	//if(Out.normal[1] < 0.0) {
	//	Out.color += nSquared.y * cubey;
	//} else
	//	Out.color += nSquared.y * cubey1;

	//if(Out.normal[2] < 0.0) {
	//	Out.color += nSquared.z * cubez1;
	//} else
	//	Out.color += nSquared.z * cubez;

		
	
	//int3 isNegative = ( worldNormal < 0.0 );


	//Out.color = float4(DoAmbientCube(normalize(Out.normal), vInstrow4.w),1.0f);

	return Out;
}



VS_OUT VertexPosTex( POSTEX input) 
{
	VS_OUT Out;
	Out.pos = input.pos;
	Out.realpos = Out.pos;
	Out.texcoord = input.texcoord;
	Out.lightmap = float2(0.5f,0.5f);
	Out.normal = float3(0.0f,0.0f,0.0f);
	Out.color = float4(0.0f,0.0f,0.0f,0.0f);
	return Out;
}

VS_OUT VertexPosTexColor( POSTEXCOL input) 
{
	VS_OUT Out;
	Out.pos = input.pos;
	Out.realpos = Out.pos;
	Out.texcoord = input.texcoord;
	Out.lightmap = float2(0.5f,0.5f);
	Out.normal = float3(0.0f,0.0f,0.0f);
	Out.color = input.color;
	return Out;
}

VS_OUT VertexPosTex2( POSTEX input) 
{
	VS_OUT Out;
	//Out.pos = input.pos;
	Out.pos = mul(input.pos, WorldViewProj);
	Out.realpos = Out.pos;
	Out.texcoord = input.texcoord;
	Out.lightmap = float2(0.5f,0.5f);
	Out.normal = float3(0.0f,0.0f,0.0f);
	Out.color = float4(0.0f,0.0f,0.0f,0.0f);
	return Out;
}

VS_POSCOLOR VertexPosColor( POSCOLOR input) 
{
	VS_POSCOLOR Out;
	Out.pos = mul(input.pos, WorldViewProj);
	Out.color = input.color;
	return Out;
}

VS_OUT VS_Sky( POSNORTEX input) 
{
	VS_OUT Out;
	Out.pos = mul(input.pos+Eye, WorldViewProj);
	Out.realpos = Out.pos;
	Out.texcoord = input.texcoord;
	Out.lightmap = float2(0.5f,0.5f);
	Out.normal = float3(0.0f,0.0f,0.0f);
	Out.color = float4(0.0f,0.0f,0.0f,0.0f);
	return Out;
}

float4 Screen(float4 tex1, float4 tex2)
{
	return one - (one - tex1) * (one - tex2);
}

float SoftLightf(float tex1, float tex2) 
{
	return (tex2 < 0.5f) ?  (2 * tex1 * tex2 + tex1 * tex1 * (1 - 2 * tex2))  : (sqrt(tex1) * (2 * tex2 - 1) + 2 * tex1 * (1 - tex2));
}

float4 SoftLight(float4 tex1, float4 tex2)
{
	float4 result;
	result.r = SoftLightf(tex1.r, tex2.r);
	result.g = SoftLightf(tex1.g, tex2.g);
	result.b = SoftLightf(tex1.b, tex2.b);
	result.a = 0;
	return result;
}

float get_log_luminance(const float3 color)
{
	float luminance = dot(color, LUMINANCE_VECTOR);
	luminance = log(1e-5 + luminance); 
	return luminance;
}

float3 GetAlbedo(in VS_OUT i)
{
	float3 albedo = tex2D(BaseTextureSampler, i.texcoord).rgb;
	return albedo;
}

float GetAlpha(in VS_OUT i)
{
	float alpha = tex2D(BaseTextureSampler, i.texcoord).a;
	return alpha;
}

float3 GetDiffuseLightingUnbumped(in VS_OUT i)
{
	return tex2D(LightmapSampler, i.lightmap).rgb;
}



float3 GetDiffuseLighting(in VS_OUT i)
{
	if( g_bBumpmap )
	{
		
	}
	else 
	{
		return GetDiffuseLightingUnbumped( i );
	}
}

float3 TonemapPixel(in float3 color)
{
	color *= MIDDLE_GRAY / (avgLogLum + 0.001f);
    color *= (1.0f + (color/(LUM_WHITE))) ;
	color /= (1.0f + color);

	return color;
}

float3 GetSpecularLighting(in VS_OUT i)
{
	float3 specularLighting = float3( 0.0f, 0.0f, 0.0f );
	if( g_bCubemap )
	{

	}

	return specularLighting;
}

// Pixel Shaders
float4 PixelTexLightLogLum(VS_OUT i) : COLOR0
{
	float4 tex = tex2D(BaseTextureSampler, i.texcoord);
	tex.rgb *= tex2D(LightmapSampler, i.lightmap).rgb;
	
	//tex.a = get_log_luminance(tex.rgb) * invLogLumRange + logLumOffset; 
	//tex.rgb = LinearToSRGB(tex.rgb);
	float b = 0.001f;
	float fogAmount = exp(-i.realpos.z*b);
	float3 fogColor = float3(0.5f, 0.6f, 0.7f);
	
	tex.rgb = TonemapPixel(tex.rgb);
	//tex.rgb = lerp(fogColor, tex.rgb, fogAmount);
	//
	return tex;
	float3 albedo = GetAlbedo( i );
	
	float3 diffuseLighting = GetDiffuseLighting( i );
	float3 specularLighting = GetSpecularLighting( i );

	float3 diffuseComponent = albedo * diffuseLighting;
	diffuseComponent *= g_OverbrightFactor;

	if( g_bSelfIllum )
	{
		float3 selfIllumComponent = g_SelfIllumTint.rgb * albedo;
		diffuseComponent = lerp(diffuseComponent, selfIllumComponent, GetAlpha( i ));
	}

	float alpha = get_log_luminance(diffuseComponent) * invLogLumRange + logLumOffset; 
	diffuseComponent = TonemapPixel(diffuseComponent);

	return float4(diffuseComponent, alpha);
}

// Pixel Shaders
float4 PixelTexLogLum(VS_OUT i) : COLOR0
{
	float4 tex = tex2D(BaseTextureSampler, i.texcoord);
	clip(tex.a-0.01f);
	tex.rgb *= i.color;
	tex *= avgLogLum;
	//tex.a = get_log_luminance(tex.rgb) * invLogLumRange + logLumOffset; 
	tex.rgb = TonemapPixel(tex.rgb);
	return tex;
}


float4 PixelTexLightAlpha(VS_OUT i) : COLOR0
{
	float3 albedo = GetAlbedo( i );
	float alpha = GetAlpha( i );

	float3 diffuseLighting = GetDiffuseLighting( i );
	float3 specularLighting = GetSpecularLighting( i );

	float3 diffuseComponent = albedo * diffuseLighting;
	diffuseComponent *= g_OverbrightFactor;

	if( g_bSelfIllum )
	{
		float3 selfIllumComponent = g_SelfIllumTint.rgb * albedo;
		diffuseComponent = lerp(diffuseComponent, selfIllumComponent, alpha);
	}

	diffuseComponent = TonemapPixel(diffuseComponent);

	return float4(diffuseComponent, alpha);
}



float4 PS_Sky(VS_OUT input) : COLOR0
{
	float4 tex = tex2D(BaseTextureSampler, input.texcoord);
	float3 tex2 = tex2D(LightmapSampler, center).rgb;
	tex.rgb = tex.rgb * tex2.rgb;

	tex.a = get_log_luminance(tex.rgb) * invLogLumRange + logLumOffset; 
	tex.rgb = TonemapPixel(tex.rgb);
	return tex;
}

float4 CalcAvgLum ( in float2 vScreenPosition : TEXCOORD0 ) : COLOR0
{
	float4 lastAvg = tex2D(BaseTextureSampler, center);
	float4 currentAvg = tex2D(LightmapSampler, center);
	
	float lastR = lastAvg.a;
	float currR = currentAvg.a;
	
	float fAdapted = lastR + (currR - lastR) * (1 - exp(-fTimeElap * fTau) );
    return fAdapted;
}

float4 BloomPS( in float2 vScreenPosition : TEXCOORD0 ) : COLOR
{
    float4 vSample = 0.0f;
    float4 vColor = 0.0f;
    float2 vSamplePosition;
    
    for( int iSample = 0; iSample < 15; iSample++ )
    {
        // Sample from adjacent points
        vSamplePosition = vScreenPosition + g_avSampleOffsets[iSample];
        vColor = tex2D(BaseTextureSampler, vSamplePosition);
        
        vSample += g_avSampleWeights[iSample]*vColor;
    }
    
    return vSample;
}

//-----------------------------------------------------------------------------
// Name: FinalPass
// Type: Pixel Shader
// Desc: 
//-----------------------------------------------------------------------------
float4 FinalPass( float2 Tex : TEXCOORD0 ) : COLOR
{
    float4 vColor = tex2D( BaseTextureSampler, Tex );
    //float3 vBloom = tex2D( s2, Tex );
	
	//vColor.rgb +=  vBloom * bloomMulti;
	vColor.a = 1.0f;
	return vColor;
}

float4 PixelColorAlpha( float4 color : COLOR ) : COLOR
{
	float4 col = float4(color.r, color.g, color.b, color.a);
	return col;
}

float4 PixelGUIAlpha( VS_OUT input ) : COLOR
{

    float4 vColor = tex2D( BaseTextureSampler, input.texcoord );
	vColor *= input.color;
	vColor.rgb *= vColor.a;
	return vColor; 
}

//-----------------------------------------------------------------------------
// Name: DownScale3x3
// Type: Pixel shader                                      
// Desc: Scale down the source texture from the average of 3x3 blocks
//-----------------------------------------------------------------------------
float4 DownScale3x3( in float2 vScreenPosition : TEXCOORD0 ) : COLOR
{
    float fAvg = 0.0f; 

    for( int i = 0; i < 9; i++ )
    {
        // Compute the sum of color values
        float4 vColor = tex2D( BaseTextureSampler, vScreenPosition + g_avSampleOffsets[i] );
		fAvg += vColor.a;
    }
    
    // Divide the sum to complete the average
    fAvg /= 9;
	return float4(fAvg,fAvg,fAvg,fAvg);
    return EncodeRE8( fAvg );
}

float4 DownScale3x3_BrightPass( in float2 vScreenPosition : TEXCOORD0) : COLOR
{
    float3 vColor = 0.0f;
        
    for( int i = 0; i < 9; i++ )
    {
        // Compute the sum of color values
        float4 vSample = tex2D( BaseTextureSampler, vScreenPosition + g_avSampleOffsets[i] );
        vColor += DecodeRGBE8( vSample );
    }
    
    // Divide the sum to complete the average
    vColor /= 9;
 
    // Bright pass and tone mapping
    vColor *= MIDDLE_GRAY / (avgLogLum + 0.001f);
    vColor.rgb *= (1.0f + (vColor/(LUM_WHITE))) ;
	vColor.rgb /= (1.0f + vColor);
	vColor = smoothstep(BRIGHT_THRESHOLD,1.0f, dot( vColor.rgb, LUMINANCE_VECTOR )) * vColor;
    return float4(vColor, 1.0f);
}

float4 DownScale2x2_Lum ( in float2 vScreenPosition : TEXCOORD0 ) : COLOR0
{
    float4 vColor = 0.0f;
    float  fAvg = 0.0f;
    
    for( int i = 0; i < 4; i++ )
    {
        // Compute the sum of color valuesx
        vColor = tex2D( BaseTextureSampler, vScreenPosition  + g_avSampleOffsets[i] );
        fAvg += vColor.a;
    }
    fAvg /= 4;
	
	float4 color = float4(fAvg,fAvg,fAvg,fAvg);	
	return color;
}

technique TexturedInstaced
{
	pass P0
	{
	AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		CullMode = None;
		VertexShader = compile vs_2_0 VertexNorTexInstancing();
		PixelShader = compile ps_2_0 PixelTexLogLum();
	}

}

// Techniques
technique TexturedLightmap
{
	pass P0
	{
		DestBlend = Zero;
		SrcBlend = SrcAlpha;
		CullMode = CCW;
		VertexShader = compile vs_1_1 VertexNorTexLight();
		PixelShader = compile ps_2_0 PixelTexLightLogLum();
	}
}
technique Sky3d
{
	pass P0
	{
		DestBlend = Zero;
		SrcBlend = SrcAlpha;
		CullMode = CCW;
		VertexShader = compile vs_1_1 VertexNorTexLight2();
		PixelShader = compile ps_2_0 PixelTexLightLogLum();
	}
}
technique TexturedLightmapAlpha
{
	pass P0
	{
		CullMode = None;
		VertexShader = compile vs_1_1 VertexNorTexLight();
		PixelShader = compile ps_2_0 PixelTexLightAlpha();

		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
	}
	
}
technique Sky
{
	pass P0
	{
		ZEnable = false; 
		ZWriteEnable = false;
		
		CullMode = CW;
		VertexShader = compile vs_1_1 VS_Sky();
		PixelShader = compile ps_2_0 PS_Sky();
	}
}
technique FinalPass_RGBE8
{
    pass p0
    {
    	CullMode = CW;
		VertexShader = compile vs_1_1 VertexPosTex();
        PixelShader = compile ps_2_0 FinalPass();
    }
}
technique GUIAlpha
{
	pass p0
	{
		CullMode = None;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		VertexShader = compile vs_1_1 VertexPosTexColor();
        PixelShader = compile ps_2_0 PixelGUIAlpha();
	}
}


technique PositionColorAlpha
{
	pass p0
	{
		CullMode = None;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		VertexShader = compile vs_1_1 VertexPosColor();
        PixelShader = compile ps_2_0 PixelColorAlpha();
	}
}
technique WorldAlpha
{
	pass p0
	{
		CullMode = None;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		VertexShader = compile vs_1_1 VertexPosTex2();
        PixelShader = compile ps_2_0 PixelGUIAlpha();
	}
}
technique CalcAvgLum_RGBE8
{
    pass p0
    {
		CullMode = CW;
		VertexShader = compile vs_1_1 VertexPosTex();
        PixelShader = compile ps_2_0 CalcAvgLum( );
    }
}
technique DownScale2x2_Lum_RGBE8
{
    pass p0
    {
		CullMode = CW;
		VertexShader = compile vs_1_1 VertexPosTex();
        PixelShader = compile ps_2_0 DownScale2x2_Lum();
    }
}
technique DownScale3x3_RGBE8
{
    pass p0
    {
		CullMode = CW;
		VertexShader = compile vs_3_0 VertexPosTex();
        PixelShader = compile ps_3_0 DownScale3x3( );
    }
}
technique DownScale3x3_BrightPass_RGBE8
{
    pass p0
    {
		CullMode = CW;
		VertexShader = compile vs_1_1 VertexPosTex();
        PixelShader = compile ps_2_0 DownScale3x3_BrightPass( );
    }
}
technique Bloom
{
    pass p0
    {
		CullMode = CW;
        PixelShader = compile ps_3_0 BloomPS();
    }
}
float4 main(VS_OUT i) : COLOR0
{
	return PixelTexLightLogLum(i);
	//return BloomPS(i);
}
