#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0
#define PS_SHADERMODEL ps_4_0
#endif

//==============================================================================
// External Parameters
//==============================================================================
SamplerState SpriteSampler : register(s0);
Texture2D Mask;
sampler2D MaskSampler = sampler_state
{
    Texture = <Mask>;
    AddressU = Clamp;
    AddressV = Clamp;
    MipFilter = Point;
    MinFilter = Point;
    MagFilter = Point;
};
float4x4 MatrixTransform;
float4x4 ViewProjection;
float4x4 WindowToClip;
float4x4 WindowToClipSquareMinor;
float4x4 WindowToClipSquareMajor;
float4x4 ClipToWindow;
float4x4 ClipToWindowSquareMinor;
float4x4 ClipToWindowSquareMajor;
float Time;
float NormalizedTime;
float2 Pan;
float2 FillScale;
float2 MaskScale;
float2 WorldPosition;
float2 TextureSize;

//==============================================================================
// Helper Functions
//==============================================================================
float map(float val, float min1, float max1, float min2, float max2)
{
    // Convert the current value to a percentage
    // 0% - min1, 100% - max1
    float perc = (val - min1) / (max1 - min1);

    // Do the same operation backwards with min2 and max2
    float result = perc * (max2 - min2) + min2;

    return result;
}

float2 map(float2 val, float min1, float max1, float min2, float max2)
{
    float x = map(val.x, min1, max1, min2, max2);
    float y = map(val.y, min1, max1, min2, max2);
    return float2(x, y);
}

float2 clipToTexture(float2 val)
{
    float x = map(val.x, -1, 1, 0, 1);
    float y = map(val.y, -1, 1, 1, 0);
    return float2(x, y);
}

float2 scaleFromCenter(float2 texCoord, float2 scale)
{
    float2 result = (texCoord - 0.5) * scale + 0.5;
    return result;
}

//==============================================================================
// Shader Stage Parameters
//==============================================================================
struct AssemblerToVertex
{
    float4 Position : POSITION;
    float4 Color : COLOR;
    float4 TexCoord : TEXCOORD;
};

struct VertexToPixel
{
    float4 Position : SV_POSITION;
    float4 DummyPosition : TEXCOORD0;
    float4 Color : COLOR; 
    float4 TexCoord : TEXCOORD1;
};

//==============================================================================
// Vertex Shaders
//==============================================================================
VertexToPixel VsMain(const in AssemblerToVertex input)
{
    VertexToPixel output;
    
    output.Position = mul(input.Position, ViewProjection);
    output.DummyPosition = float4(0, 0, 0, 0);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    
    return output;
}

//==============================================================================
// Pixel Shaders
//==============================================================================
float4 PsMain(VertexToPixel input) : SV_TARGET
{
    float2 worldOffsetInTexCoords = float2(WorldPosition.x, -WorldPosition.y) / TextureSize;
    float2 fillTexCoord = (input.TexCoord.xy + worldOffsetInTexCoords) / FillScale;
    
    float4 spriteColor = tex2D(SpriteSampler, fillTexCoord);

    float2 maskTexCoord = scaleFromCenter(input.TexCoord.xy, 1 / MaskScale);
    float4 maskColor = tex2D(MaskSampler, maskTexCoord).a * float4(1, 1, 1, 1);
    
    return spriteColor * maskColor;
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
