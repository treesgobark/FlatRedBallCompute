#if OPENGL
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#else
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0
#endif

//==============================================================================
// GraphicsDevice Parameters
//==============================================================================
SamplerState SamplerState0 : register(s0);
Texture2D Texture : register(t0);

//==============================================================================
// External Parameters
//==============================================================================
float4x4 ViewProjection;
float Time;
float NormalizedTime;
float2 WorldPosition;
float2 TextureSize;

//==============================================================================
// Shader Stage Parameters
//==============================================================================
struct AssemblerToVertex
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float4 TexCoord : TEXCOORD0;
};

struct VertexToPixel
{
    float4 Position : SV_Position0;
    float4 Color : COLOR0;
    float4 TexCoord : TEXCOORD0;
};

//==============================================================================
// Vertex Shaders
//==============================================================================
VertexToPixel VsMain(const in AssemblerToVertex input)
{
    VertexToPixel output;
    
    output.Position = mul(input.Position, ViewProjection);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    
    return output;
}

//==============================================================================
// Pixel Shaders
//==============================================================================
float4 PsMain(VertexToPixel input) : SV_TARGET
{
    float4 diffuse = Texture.Sample(SamplerState0, input.TexCoord.xy);
    float4 finalColor = diffuse * input.Color;
    
    return finalColor;
}

//==============================================================================
// Techniques
//==============================================================================
technique Tech0
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VsMain();
        PixelShader = compile PS_SHADERMODEL PsMain();
    }
}
