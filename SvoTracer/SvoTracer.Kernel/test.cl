typedef struct
{
    float Pigment[3];
    float Opacity;
} Colour;

typedef struct
{
    ushort Chunk;
} BaseBlock;

typedef struct
{
    ushort Chunk;
    uint Child;
    Colour Colour;
} Block;

typedef struct
{
    //Position
    float Origin[3];
    //Direction faced
    float Facing[3];
    //Horizonal/vertical FoV angle of the screen
    float FoV[2];
    //Depth of field made up of focal depth(the angle of the forced depth) and focal point(how deep the minimum is)
    float DoF[2];
    //Screen size
    uint ScreenSize[2];
    float MaxOpacity;
    //Depth of inviolate memory(Specific to voxels)
    uchar BaseDepth;
} InputData;

typedef struct
{
    //Current location
    ulong3 Location;
    Colour Colour;
    //Position
    float3 Origin;
    //Vector direction
    float3 Direction;
    //Inverse vector direction
    float3 InvDirection;
    //Depth of field made up of focal depth(the angle of the forced depth) and focal point(how deep the minimum is)
    float2 DoF;
    float PixelFoV;
    //Max Opacity
    float MaxOpacity;
    //Depth of inviolate memory(Specific to voxels)
    uchar BaseDepth;
    //Signs of the vector direction
    bool DirectionSignX;
    bool DirectionSignY;
    bool DirectionSignZ;
} WorkingData;

__kernel void voxelTrace(__write_only image2d_t outputImage, InputData _input, global BaseBlock *bases, global Block *blocks)
{
    float time = 0;
    unsigned int x = get_global_id(0);
    unsigned int y = get_global_id(1);
    int2 coord = (int2)(x, y);
    // calculate uv coordinates
    float u = x / (float) _input.ScreenSize[0];
    float v = y / (float)  _input.ScreenSize[1];
    u = u*2.0f - 1.0f;
    v = v*2.0f - 1.0f;

    // calculate simple sine wave pattern
    float freq = 4.0f;
    float w = sin(u*freq + time) * cos(v*freq + time) * 0.5f;
    write_imagef(outputImage, coord, (float4)(1, w, 0, 1));
}