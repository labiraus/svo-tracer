typedef struct {
  float RayLength;
  float3 Direction;
  ulong3 Location;
  uchar Depth;
  float ConeDepth;
  float3 Normal;
  float Luminosity;
  uchar ColourR;
  uchar ColourG;
  uchar ColourB;
  uchar Opacity;
  uchar Specularity;
  uchar Gloss;
} RayData;

typedef struct {
  float ColourR;
  float ColourG;
  float ColourB;
  float TotalWeighting;
} RayAccumulator;

typedef struct {
  uint Child;
  ushort Chunk;
  short NormalPitch;
  short NormalYaw;
  uchar ColourR;
  uchar ColourG;
  uchar ColourB;
  uchar Opacity;
  uchar Specularity;
  uchar Gloss;
} Block;

typedef struct {
  ushort Tick;
  uint Parent;
} Usage;

typedef struct {
  uint ParentAddress;
  uint NextElement;
} Parent;

typedef struct {
  uchar Properties;
  // 0 CullChild
  // 1 AlterViolability
  // 2 MakeInviolate
  // 3 UpdateChunk
  // 4 BaseBlock
  uchar Depth;
  uint Address;
  ushort Chunk;
  uint ColourAddress;
  uint ChildAddress;
} Pruning;

typedef struct {
  uint GraftDataAddress;
  uint GraftTotalSize;
  uchar Depth;
  uint GraftAddress;
} Grafting;

typedef struct {
  uint Address;
  ushort Tick;
  uchar Depth;
  ulong Location[3];
  uchar TreeSize;
} ChildRequest;

typedef struct {
  // Position
  float Origin[3];
  // Direction faced
  float Facing[9]; // this should be a rotation matrix
  // Horizonal/vertical FoV angle of the screen
  float FoV[2];
  // Depth of field made up of focal depth(the angle of the forced depth) and
  // focal point(how deep the minimum is)
  float DoF[2];
  // Screen size
  uint2 ScreenSize;
  uchar MaxOpacity;
  // Depth of inviolate memory(Specific to voxels)
  uchar BaseDepth;
  ushort Tick;
  uint MaxChildRequestId;
  float FovMultiplier;
  float FovConstant;
  float WeightingMultiplier;
  float WeightingConstant;
} TraceInput;

typedef struct {
  uchar BaseDepth;
  ushort Tick;
  uint MaxChildRequestId;
  uint MemorySize;
  uint Offset;
  uint GraftSize;
} UpdateInputData;

typedef struct {
  // Position
  float3 Origin;
  // Vector direction
  float3 Direction;
  // Inverse vector direction
  float3 InvDirection;
  // Depth of field made up of focal depth(the angle of the forced depth) and
  // focal point(how deep the minimum is)
  float2 DoF;
  float TraceFoV;
  // Signs of the vector direction
  bool DirectionSignX;
  bool DirectionSignY;
  bool DirectionSignZ;
  ushort Tick;
  float ConeDepth;
  // Current chunk
  ulong3 Location;
  // Depth of inviolate memory(Specific to voxels)
  uint MaxChildRequestId;
  float Weighting;
  uchar Depth;
  uint LastAddress;
} TraceData;

typedef struct{
  // Position
  float3 Origin;
  // Vector direction
  float3 Direction;
  float2 DoF;
  float TraceFoV;
  float ConeDepth;
  ulong3 Location;
  float Weighting;
  uchar Depth;
} SavedTrace;

typedef struct {
  global ushort *bases;
  global Block *blocks;
  global Usage *usage;
  global uint *childRequestId;
  global ChildRequest *childRequests;
  global float3 *origins;
  global float3 *directions;
  global float2 *doFs;
  global float *traceFoVs;
  global ulong3 *locations;
  global float *weightings;
  global uchar *depths;
  global uint *lastAddress;
  global uint *baseTraces;
  global uint *traces;
  global uint *traceQueue;
  global uint *traceQueueId;
  global uint *baseTraceQueue;
  global uint *baseTraceQueueId;
  const uchar baseDepth;
} Geometry;

// Inline math
ulong floatToUlong(float x);
float ulongToFloat(ulong x);
ulong roundUlong(ulong value, uchar depth, bool roundUp);
uint powSum(uchar depth);
void getSemaphor(global int *semaphor);
void releaseSemaphor(global int *semaphor);
float3 normalVector(short pitch, short yaw);

//Memory
ushort getBase(Geometry geometry, uchar depth, ulong3 location);
Block getBlock(Geometry geometry, uint address);
void requestChild(Geometry geometry, ChildRequest request);
ChildRequest buildChildRequest(uint address, uchar depth, ushort tick, uchar treeSize, ulong3 location);
void updateUsage(Geometry geometry, uint address, ushort tick);

// Tree
float coneSize(float m, TraceData data);
void setConeDepth(TraceData *_data);

// Octree
uint baseLocation(uchar depth, ulong3 location);
uchar comparePositions(uchar depth, ulong3 previousLocation, TraceData data);
uchar chunkPosition(uchar depth, ulong3 location);
bool leaving(TraceData data);
void traverseChunk(uchar depth, TraceData *_data);
void traverseChunkNormal(uchar depth, float3 normal, TraceData *_data);
TraceData setupInitialTrace(int2 coord, TraceInput input);
TraceData setupTrace(ulong3 location, float3 normal, float3 direction, uchar depth, float fov, float weighting, TraceInput input);
bool traceIntoVolume(TraceData *_data);
Block average(uint address, Geometry geometry, TraceData data);
bool traceBlock(Geometry geometry, RayData *ray, TraceData *data, uint *depthHeap);
bool traceBase(Geometry geometry, RayData *ray, TraceData *data);
RayData traceVoxel(Geometry geometry, TraceData data);
RayData traceMesh(Geometry geometry, TraceData _data, RayData mask);
RayData traceParticle(Geometry geometry, TraceData _data, RayData mask);
RayData traceLight(Geometry geometry, TraceData data, RayData mask);
RayData traceRay(Geometry geometry, TraceData data);
void accumulateRay(Geometry geometry, TraceData data, RayAccumulator *_accumulator);
void spawnRays(Geometry geometry, RayData *_ray, RayAccumulator *_accumulator, float baseWeighting, TraceInput input);

// Writing
RayData resolveBackgroundRayData(TraceData data);
RayData resolveRayData(Block surfaceData, TraceData *_data);
void writeToAccumulator(RayData overlay, float weighting, RayAccumulator *_accumulator);
void draw(__write_only image2d_t outputImage, RayData ray, RayAccumulator accumulator, int2 coord);

// Tree Management
void helpDereference(global Block *blocks, global Usage *usage, global uint *parentSize, global bool *parentResidency, global Parent *parents,
                     global uint2 *dereferenceQueue, global int *dereferenceRemaining, global int *semaphor, ushort tick);
void dereference(global Block *blocks, global Usage *usage, global uint *parentSize, global bool *parentResidency, global Parent *parents,
                 global uint2 *dereferenceQueue, global int *dereferenceRemaining, global int *semaphor, uint startAddress, ushort tick);
uint findAddress(global Block *blocks, global Usage *usage, global uint *childRequestId, global ChildRequest *childRequests, global uint *parentSize,
                 global bool *parentResidency, global Parent *parents, global uint2 *dereferenceQueue, global int *dereferenceRemaining,
                 global int *semaphor, global ulong *addresses, UpdateInputData inputData, uint address, uint depth);

// Inline math

// Converts a float between 0 and 1 into a ulong coordinate
ulong floatToUlong(float x) {
  // float rounding errors mean that this is the closest you can get to 1 at 24
  // bits deep
  if (x >= 0.999999911f)
    return ULONG_MAX;
  else
    return (ulong)(fabs(x) * ULONG_MAX);
}

float ulongToFloat(ulong x) { return native_divide((float)x, (float)ULONG_MAX); }

ulong roundUlong(ulong value, uchar depth, bool roundUp) {
  if (roundUp)
    return ((value & (ULONG_MAX - (ULONG_MAX >> (depth + 1)))) + ((ULONG_MAX - (ULONG_MAX >> 1)) >> depth)) - value;

  ulong output = value - ((value & (ULONG_MAX - (ULONG_MAX >> (depth + 1)))) - 1);

  if (output >= value)
    return value;

  return output;
}

// uint powSum(uchar depth) { for (int i = 1; i <= depth; i++) output += (1 << (3 * i)); }
uint powSum(uchar depth) { return (0b1001001001001001001001001001001000 >> (11 - depth) * 3) - 1; }

void getSemaphor(global int *semaphor) {
  int occupied = atomic_xchg(semaphor, 1);
  while (occupied > 0) {
    occupied = atomic_xchg(semaphor, 1);
  }
}

void releaseSemaphor(global int *semaphor) { int prevVal = atomic_xchg(semaphor, 0); }

float3 normalVector(short pitch, short yaw) {
  // yaw * 2pi/short max
  float fYaw = yaw * M_PI_F / 32767.0f;
  float fPitch = pitch * M_PI_F / 32767.0f;
  float sinYaw = native_sin(fYaw);
  float cosYaw = native_cos(fYaw);
  float sinPitch = native_sin(fPitch);
  float cosPitch = native_cos(fPitch);

  return (float3)(cosYaw * cosPitch, sinYaw * cosPitch, sinPitch);
}

// Memory

ushort getBase(Geometry geometry, uchar depth, ulong3 location){
  uint address = powSum(depth - 1) + baseLocation(depth, location);
  return geometry.bases[address];
}

Block getBlock(Geometry geometry, uint address){
  return geometry.blocks[address];
}

void requestChild(Geometry geometry, ChildRequest request){
  uint id = atomic_inc(geometry.childRequestId);
  geometry.childRequests[id] = request;
}

ChildRequest buildChildRequest(uint address, uchar depth, ushort tick, uchar treeSize, ulong3 location) {
  ChildRequest request;
  request.Address = address;
  request.Tick = tick;
  request.Depth = depth;
  request.Location[0] = location.x;
  request.Location[1] = location.y;
  request.Location[2] = location.z;
  request.TreeSize = treeSize;
  return request;
}

void updateUsage(Geometry geometry, uint address, ushort tick) {
  Usage usageVal = geometry.usage[address];
  if (usageVal.Tick < USHRT_MAX - 1 && usageVal.Tick != tick)
    geometry.usage[address].Tick = tick;
}

// Tree

// Calculates the cone size at a given depth from FoV and pixel diameter data
// Largest cone size wins
float coneSize(float m, TraceData data) {
  float eye = data.TraceFoV * m;
  float fov = fabs(data.DoF.x * (data.DoF.y - m));
  return max(eye, fov);
}

// Determine the maximum tree depth for a cone at this location
void setConeDepth(TraceData *_data) {
  float cone =
      coneSize(fabs(fast_length((float3)(_data->Origin.x - ulongToFloat(_data->Location.x), _data->Origin.y - ulongToFloat(_data->Location.y),
                                         _data->Origin.z - ulongToFloat(_data->Location.z)))),
               *_data);
  _data->ConeDepth = -half_log2(cone);
}

// Octree

// Deduces array offset of a base's location given its depth
// Bases are stored as a dense octree down to depth BaseDepth
uint baseLocation(uchar depth, ulong3 location) {
  uint output = 0;
  for (uchar i = 0; i < depth; i++)
    output = (output << 3) + chunkPosition(i, location);

  return output;
}

uchar comparePositions(uchar depth, ulong3 previousLocation, TraceData data) {
  uchar newPosition = chunkPosition(depth, data.Location);
  uchar previousPosition = chunkPosition(depth, previousLocation);
  while (((previousPosition & 1) == data.DirectionSignX && (newPosition & 1) != data.DirectionSignX) ||
         (((previousPosition >> 1) & 1) == data.DirectionSignY && ((newPosition >> 1) & 1) != data.DirectionSignY) ||
         (((previousPosition >> 2) & 1) == data.DirectionSignZ && ((newPosition >> 2) & 1) != data.DirectionSignZ)) {
    if (depth == 1)
      break;
    depth--;
    newPosition = chunkPosition(depth, data.Location);
    previousPosition = chunkPosition(depth, previousLocation);
  }
  return depth;
}

uchar chunkPosition(uchar depth, ulong3 location) {
  return ((location.x >> (64 - depth - 1) & 1) + ((location.y >> (64 - depth - 1) & 1) << 1) + ((location.z >> (64 - depth - 1) & 1) << 2));
}

// Determines whether cone is leaving the octree
bool leaving(TraceData data) {
  return (!data.DirectionSignX && data.Location.x == 0) || (!data.DirectionSignY && data.Location.y == 0) ||
         (!data.DirectionSignZ && data.Location.z == 0) || (data.DirectionSignX && data.Location.x == ULONG_MAX) ||
         (data.DirectionSignY && data.Location.y == ULONG_MAX) || (data.DirectionSignZ && data.Location.z == ULONG_MAX);
}

// Moves to the nearest neighboring chunk along the Direction vector
void traverseChunk(uchar depth, TraceData *_data) {
  // determine distance from current location to x, y, z chunk boundary
  ulong dx = roundUlong(_data->Location.x, depth, _data->DirectionSignX);
  ulong dy = roundUlong(_data->Location.y, depth, _data->DirectionSignY);
  ulong dz = roundUlong(_data->Location.z, depth, _data->DirectionSignZ);

  // calulate the shortest of the three lengths
  float ax = fabs(dx * _data->InvDirection.x);
  float ay = fabs(dy * _data->InvDirection.y);
  float az = fabs(dz * _data->InvDirection.z);

  if (ax <= ay && ax <= az) {
    float udx = ulongToFloat(dx);
    dy = floatToUlong(_data->Direction.y * _data->InvDirection.x * udx);
    dz = floatToUlong(_data->Direction.z * _data->InvDirection.x * udx);
  } else if (ay <= ax && ay <= az) {
    float udy = ulongToFloat(dy);
    dx = floatToUlong(_data->Direction.x * _data->InvDirection.y * udy);
    dz = floatToUlong(_data->Direction.z * _data->InvDirection.y * udy);
  } else {
    float udz = ulongToFloat(dz);
    dx = floatToUlong(_data->Direction.x * _data->InvDirection.z * udz);
    dy = floatToUlong(_data->Direction.y * _data->InvDirection.z * udz);
  }

  if (_data->DirectionSignX)
    _data->Location.x += dx;
  else
    _data->Location.x -= dx;

  if (_data->DirectionSignY)
    _data->Location.y += dy;
  else
    _data->Location.y -= dy;

  if (_data->DirectionSignZ)
    _data->Location.z += dz;
  else
    _data->Location.z -= dz;

  // if trafersal has overflowed ulong then the octree has been left
  if (_data->DirectionSignX && _data->Location.x == 0)
    _data->Location.x = ULONG_MAX;
  else if (!_data->DirectionSignX && _data->Location.x == ULONG_MAX)
    _data->Location.x = 0;

  if (_data->DirectionSignY && _data->Location.y == 0)
    _data->Location.y = ULONG_MAX;
  else if (!_data->DirectionSignY && _data->Location.y == ULONG_MAX)
    _data->Location.y = 0;

  if (_data->DirectionSignZ && _data->Location.z == 0)
    _data->Location.z = ULONG_MAX;
  else if (!_data->DirectionSignZ && _data->Location.z == ULONG_MAX)
    _data->Location.z = 0;

  setConeDepth(_data);
}

// Moves to the furthest neighboring chunk along the Direction vector
void traverseChunkNormal(uchar depth, float3 normal, TraceData *_data) {
  bool directionSignX = normal.x >= 0;
  bool directionSignY = normal.y >= 0;
  bool directionSignZ = normal.z >= 0;
  float3 inv = native_divide(1, normal);
  // determine distance from current location to x, y, z chunk boundary
  ulong dx = roundUlong(_data->Location.x, depth, directionSignX);
  ulong dy = roundUlong(_data->Location.y, depth, directionSignY);
  ulong dz = roundUlong(_data->Location.z, depth, directionSignZ);

  // calulate the shortest of the three lengths
  float ax = fabs(dx * inv.x);
  float ay = fabs(dy * inv.y);
  float az = fabs(dz * inv.z);

  if (ax >= ay && ax >= az) {
    float udx = ulongToFloat(dx);
    dy = floatToUlong(normal.y * inv.x * udx);
    dz = floatToUlong(normal.z * inv.x * udx);
  } else if (ay >= ax && ay >= az) {
    float udy = ulongToFloat(dy);
    dx = floatToUlong(normal.x * inv.y * udy);
    dz = floatToUlong(normal.z * inv.y * udy);
  } else {
    float udz = ulongToFloat(dz);
    dx = floatToUlong(normal.x * inv.z * udz);
    dy = floatToUlong(normal.y * inv.z * udz);
  }

  if (directionSignX)
    _data->Location.x += dx;
  else
    _data->Location.x -= dx;

  if (directionSignY)
    _data->Location.y += dy;
  else
    _data->Location.y -= dy;

  if (directionSignZ)
    _data->Location.z += dz;
  else
    _data->Location.z -= dz;

  // if trafersal has overflowed ulong then the octree has been left
  if (directionSignX && _data->Location.x == 0)
    _data->Location.x = ULONG_MAX;
  else if (!directionSignX && _data->Location.x == ULONG_MAX)
    _data->Location.x = 0;

  if (directionSignY && _data->Location.y == 0)
    _data->Location.y = ULONG_MAX;
  else if (!directionSignY && _data->Location.y == ULONG_MAX)
    _data->Location.y = 0;

  if (directionSignZ && _data->Location.z == 0)
    _data->Location.z = ULONG_MAX;
  else if (!directionSignZ && _data->Location.z == ULONG_MAX)
    _data->Location.z = 0;

  setConeDepth(_data);
}

TraceData setupInitialTrace(int2 coord, TraceInput input) {
  // rotation around the z axis
  float u = input.FoV[0] * native_divide(native_divide((float)input.ScreenSize.x, 2) - (float)coord.x, (float)input.ScreenSize.x);
  // rotation around the y axis
  float v = input.FoV[1] * native_divide(native_divide((float)input.ScreenSize.y, 2) - (float)coord.y, (float)input.ScreenSize.y);
  float sinU = native_sin(u);
  float cosU = native_cos(u);
  float sinV = native_sin(v);
  float cosV = native_cos(v);
  float matRot0x = cosU * cosV;
  float matRot1x = (0 - sinU) * cosV;
  float matRot2x = sinV;

  TraceData traceData = {
      .Direction = (float3)(input.Facing[0] * matRot0x + input.Facing[3] * matRot1x + input.Facing[6] * matRot2x,
                            input.Facing[1] * matRot0x + input.Facing[4] * matRot1x + input.Facing[7] * matRot2x,
                            input.Facing[2] * matRot0x + input.Facing[5] * matRot1x + input.Facing[8] * matRot2x),
      .InvDirection =
          (float3)(native_divide(1, traceData.Direction.x), native_divide(1, traceData.Direction.y), native_divide(1, traceData.Direction.z)),
      .DirectionSignX = traceData.Direction.x >= 0,
      .DirectionSignY = traceData.Direction.y >= 0,
      .DirectionSignZ = traceData.Direction.z >= 0,
      .Origin = (float3)(input.Origin[0], input.Origin[1], input.Origin[2]),
      .Tick = input.Tick,
      .DoF = (float2)(input.DoF[0], input.DoF[1]),
      .TraceFoV = native_divide(input.FoV[0], input.ScreenSize.x),
      .MaxChildRequestId = input.MaxChildRequestId,
      .Weighting = 1,
      .Depth = 1,
  };
  return traceData;
}

TraceData setupTrace(ulong3 location, float3 normal, float3 direction, uchar depth, float fov, float weighting, TraceInput input) {
  TraceData traceData = {
      .Origin = (float3)(ulongToFloat(location.x), ulongToFloat(location.y), ulongToFloat(location.z)),
      .Direction = direction,
      .InvDirection = (float3)(native_divide(1, direction.x), native_divide(1, direction.y), native_divide(1, direction.z)),
      .DirectionSignX = direction.x >= 0,
      .DirectionSignY = direction.y >= 0,
      .DirectionSignZ = direction.z >= 0,
      .DoF = (float2)(0, 0),
      .TraceFoV = fov,
      .Tick = input.Tick,
      .ConeDepth = 0,
      .Location = location,
      .MaxChildRequestId = input.MaxChildRequestId,
      .Weighting = weighting,
      .Depth = 1,
  };
  traverseChunkNormal(depth, normal, &traceData);
  return traceData;
}

// Sets chunk and determines whether the ray hits the octree
bool traceIntoVolume(TraceData *_data) {
  bool x0 = _data->Origin.x < 0;
  bool x1 = _data->Origin.x > 1;
  bool xp = _data->Direction.x == 0;
  bool xd = _data->Direction.x >= 0;
  bool y0 = _data->Origin.y < 0;
  bool y1 = _data->Origin.y > 1;
  bool yp = _data->Direction.y == 0;
  bool yd = _data->Direction.y >= 0;
  bool z0 = _data->Origin.z < 0;
  bool z1 = _data->Origin.z > 1;
  bool zp = _data->Direction.z == 0;
  bool zd = _data->Direction.z >= 0;
  float locationX = _data->Origin.x;
  float locationY = _data->Origin.y;
  float locationZ = _data->Origin.z;
  float m = 0;
  float mx = 0;
  float my = 0;
  float mz = 0;
  int xyz = (x0 ? 0b100000 : 0) + (x1 ? 0b010000 : 0) + (y0 ? 0b001000 : 0) + (y1 ? 0b000100 : 0) + (z0 ? 0b000010 : 0) + (z1 ? 0b000001 : 0);
  if (xyz == 0) {
    _data->Location.x = floatToUlong(locationX);
    _data->Location.y = floatToUlong(locationY);
    _data->Location.z = floatToUlong(locationZ);
    return true;
  }
  // DIR is parallel to one axis and outside of that axis's box walls
  else if ((x0 | x1) && xp)
    return false;
  else if ((y0 | y1) && yp)
    return false;
  else if ((z0 | z1) && zp)
    return false;
  // DIR is divergent from one of the planes POS is outside of
  else if (x0 && !xd) // x0
    return false;
  else if (x1 && xd) // x1
    return false;
  else if (y0 && !yd) // y0
    return false;
  else if (y1 && yd) // y1
    return false;
  else if (z0 && !zd) // z0
    return false;
  else if (z1 && zd) // z1
    return false;

  switch (xyz) {
  case 0b000000:
    break;
  // Adjacent to one of the 6 planes
  case 0b100000: // x0
    m = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    locationX = 0;
    locationY = _data->Origin.y + (_data->Direction.y * m);
    locationZ = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 0b010000: // x1
    m = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    locationX = 1;
    locationY = _data->Origin.y + (_data->Direction.y * m);
    locationZ = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 0b001000: // y0
    m = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    locationX = _data->Origin.x + (_data->Direction.x * m);
    locationY = 0;
    locationZ = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 0b000100: // y1
    m = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    locationX = _data->Origin.x + (_data->Direction.x * m);
    locationY = 1;
    locationZ = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 0b000010: // z0
    m = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    locationX = _data->Origin.x + (_data->Direction.x * m);
    locationY = _data->Origin.y + (_data->Direction.y * m);
    locationZ = 0;
    break;
  case 0b000001: // z1
    m = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    locationX = _data->Origin.x + (_data->Direction.x * m);
    locationY = _data->Origin.y + (_data->Direction.y * m);
    locationZ = 1;
    break;
  // The 8 side arcs outside of the box between two of the faces on one axis and
  // near to two faces on the other two axies z face
  case 0b101000: // x0y0
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    if (mx >= my) {
      m = mx;
      locationX = 0;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 0;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    }
    break;
  case 0b011000: // x1y0
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    if (mx >= my) {
      m = mx;
      locationX = 1;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 0;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    }
    break;
  case 0b100100: // x0y1
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    if (mx >= my) {
      m = mx;
      locationX = 0;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 1;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    }
    break;
  case 0b010100: // x1y1
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    if (mx >= my) {
      m = mx;
      locationX = 1;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 1;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    }
    break;
  // y face
  case 0b100010: // x0z0
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b010010: // x1z0
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b100001: // x0z1
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b010001: // x1z1
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 1;
    }
    break;
  // x face
  case 0b001010: // y0z0
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 0;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b000110: // y1z0
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 1;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b001001: // y0z1
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 0;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b000101: // y1z1
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 1;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 1;
    }
    break;
  // The 8 corners
  case 0b101010: // x0y0z0
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 0;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b011010: // x1y0z0
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 0;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b100110: // x0y1z0
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 1;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b010110: // x1y1z0
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 1;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b101001: // x0y0z1
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 0;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b011001: // x1y0z1
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 0;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b100101: // x0y1z1
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 1;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b010101: // x1y1z1
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = 1;
      locationZ = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      locationX = _data->Origin.x + (_data->Direction.x * m);
      locationY = _data->Origin.y + (_data->Direction.y * m);
      locationZ = 1;
    }
    break;
  default:
    return false;
  }
  _data->Location.x = floatToUlong(locationX);
  _data->Location.y = floatToUlong(locationY);
  _data->Location.z = floatToUlong(locationZ);
  float c = coneSize(m, *_data);
  return !(locationX < -c || locationX > 1 + c || locationY < -c || locationY > 1 + c || locationZ < -c || locationZ > 1 + c);
}

Block average(uint address, Geometry geometry, TraceData data) {
  // Average like heck
  return getBlock(geometry, address);
}

bool traceBlock(Geometry geometry, RayData *ray, TraceData *data, uint *depthHeap) {
  uint localAddress;
  if (data->Depth == geometry.baseDepth + 2)
    localAddress = baseLocation(data->Depth, data->Location);
  else
    localAddress = getBlock(geometry, depthHeap[data->Depth - 1]).Child + chunkPosition(data->Depth - 1, data->Location);
  depthHeap[data->Depth] = localAddress;

  // Minimize updating usage
  updateUsage(geometry, localAddress >> 3, data->Tick);

  Block block = getBlock(geometry, localAddress);
  // Check if current block chunk contains geometry
  if (((block.Chunk >> (chunkPosition(data->Depth, data->Location) * 2)) & 2) != 2) {
    // current block chunk has no geometry, move to edge of chunk and go up a level if this is the edge
    ulong3 previousLocation = data->Location;
    traverseChunk(data->Depth, data);
    if (leaving(*data)) {
      *ray = resolveBackgroundRayData(*data);
      return true;
    }

    data->Depth = comparePositions(data->Depth, previousLocation, *data);
  } else {
    // C value is too diffuse to use
    if (data->ConeDepth < (geometry.baseDepth + 2)) {
      data->Depth = geometry.baseDepth + 2;
      *ray = resolveRayData(getBlock(geometry, depthHeap[data->Depth]), data);
      return true;
    }

    // ConeDepth value requires me to go up a level
    else if (data->ConeDepth < data->Depth) {
      data->Depth--;
    }

    // no child found, resolve colour of this voxel
    else if (block.Child == UINT_MAX) {
      requestChild(geometry, buildChildRequest(localAddress, data->Depth, data->Tick, 1, data->Location));
      *ray = resolveRayData(block, data);
      return true;
    }

    // cone data->Depth not met, navigate to child
    else if (data->ConeDepth > (data->Depth + 1)) {
      data->Depth++;
    }

    else {
      *ray = resolveRayData(average(localAddress, geometry, *data), data);
      return true;
    }
  }
  return false;
}

bool traceBase(Geometry geometry, RayData *_ray, TraceData *_data) {
  ushort chunk = getBase(geometry, _data->Depth, _data->Location);
  if (_data->Depth <= geometry.baseDepth && (chunk >> (chunkPosition(_data->Depth, _data->Location) * 2) & 2) != 2) {
    // current chunk has no geometry, move to edge of chunk and go up a level if this is the edge of the block
    ulong3 previousLocation = _data->Location;
    traverseChunk(_data->Depth, _data);
    if (leaving(*_data)) {
      *_ray = resolveBackgroundRayData(*_data);
      return true;
    }
    _data->Depth = comparePositions(_data->Depth, previousLocation, *_data);
  } else {
    if (_data->Depth < geometry.baseDepth)
      // Still traversing base chunks
      _data->Depth++;
    else
      // Traversing blocks
      _data->Depth = geometry.baseDepth + 2;
  }
  return false;
}

RayData traceVoxel(Geometry geometry, TraceData data) {
  bool finished = false;
  uint depthHeap[64];
  depthHeap[geometry.baseDepth + 1] = UINT_MAX;
  RayData ray;
  while (!finished) {
    if (data.Depth == (geometry.baseDepth + 1))
      data.Depth = geometry.baseDepth;
    else if (data.Depth > (geometry.baseDepth + 1))
      finished = traceBlock(geometry, &ray, &data, depthHeap);
    else
      finished = traceBase(geometry, &ray, &data);
  }
  return ray;
}

RayData traceMesh(Geometry geometry, TraceData data, RayData mask) { return mask; }

RayData traceParticle(Geometry geometry, TraceData data, RayData mask) { return mask; }

RayData traceLight(Geometry geometry, TraceData data, RayData mask) { return mask; }

RayData traceRay(Geometry geometry, TraceData data) {
  RayData ray = traceVoxel(geometry, data);
  // ray = traceMesh(geometry, data, ray);
  // ray = traceParticle(geometry, data, ray);
  // ray = traceLight(geometry, data, ray);
  return ray;
}

void accumulateRay(Geometry geometry, TraceData data, RayAccumulator *_accumulator) {
  RayData ray = traceRay(geometry, data);
  writeToAccumulator(ray, data.Weighting, _accumulator);
}

void spawnRays(Geometry geometry, RayData *_ray, RayAccumulator *_accumulator, float baseWeighting, TraceInput input) {
  float3 ringSeed;
  float3 ringVector;
  float fov = input.FovConstant;
  float oldfov = fov;
  float weighting = input.WeightingConstant;
  float ringWeighting = weighting;
  float spineRotation[12];
  float ringRotation[12];
  float spineAngle = 0;
  float ringAngle;
  uchar depth = _ray->Depth;

  float3 spineAxis = normalize(cross(_ray->Direction, _ray->Normal));

  float3 reflection = _ray->Direction - (2 * dot(_ray->Direction, _ray->Normal) * _ray->Normal);
  float dotProduct = dot(reflection, _ray->Normal);
  if (dotProduct < fov)
    ringWeighting = weighting * dotProduct / fov;

  accumulateRay(geometry, setupTrace(_ray->Location, _ray->Normal, reflection, depth, fov, ringWeighting * baseWeighting, input), _accumulator);

  while (spineAngle < M_PI_F) {
    fov = (input.FovMultiplier * (spineAngle + fov) + input.FovConstant) / (1 - input.FovMultiplier);
    spineAngle = (1 + input.FovMultiplier) * (spineAngle + oldfov) / (1 - input.FovMultiplier);
    oldfov = fov;
    weighting = spineAngle * input.WeightingMultiplier + input.WeightingConstant;
    ringWeighting = weighting;

    spineRotation[0] = native_cos(-spineAngle);
    spineRotation[1] = native_sin(-spineAngle);
    spineRotation[2] = 1.0f - spineRotation[0];
    spineRotation[3] = spineRotation[2] * spineAxis.x * spineAxis.x;
    spineRotation[4] = spineRotation[2] * spineAxis.x * spineAxis.y;
    spineRotation[5] = spineRotation[2] * spineAxis.x * spineAxis.z;
    spineRotation[6] = spineRotation[2] * spineAxis.y * spineAxis.y;
    spineRotation[7] = spineRotation[2] * spineAxis.y * spineAxis.z;
    spineRotation[8] = spineRotation[2] * spineAxis.z * spineAxis.z;
    spineRotation[9] = spineRotation[1] * spineAxis.x;
    spineRotation[10] = spineRotation[1] * spineAxis.y;
    spineRotation[11] = spineRotation[1] * spineAxis.z;

    ringSeed.x = (spineRotation[3] + spineRotation[0]) * reflection.x + (spineRotation[4] - spineRotation[11]) * reflection.y +
                 (spineRotation[5] + spineRotation[10]) * reflection.z;
    ringSeed.y = (spineRotation[4] + spineRotation[11]) * reflection.x + (spineRotation[6] + spineRotation[0]) * reflection.y +
                 (spineRotation[7] - spineRotation[9]) * reflection.z;
    ringSeed.z = (spineRotation[5] - spineRotation[10]) * reflection.x + (spineRotation[7] + spineRotation[9]) * reflection.y +
                 (spineRotation[8] + spineRotation[0]) * reflection.z;
    ringSeed = normalize(ringSeed);

    dotProduct = dot(ringSeed, _ray->Normal);
    if (dotProduct < 0)
      break;
    // fov dives into surface
    if (dotProduct < fov)
      ringWeighting = weighting * dotProduct / fov;

    accumulateRay(geometry, setupTrace(_ray->Location, _ray->Normal, ringSeed, depth, fov, ringWeighting * baseWeighting, input), _accumulator);

    for (int j = 1; j * fov < M_PI_2_F; j++) {
      ringWeighting = weighting;
      ringAngle = j * 2 * fov;
      ringRotation[0] = native_cos(-ringAngle);
      ringRotation[1] = native_sin(-ringAngle);
      ringRotation[2] = 1.0f - ringRotation[0];
      ringRotation[3] = ringRotation[2] * reflection.x * reflection.x;
      ringRotation[4] = ringRotation[2] * reflection.x * reflection.y;
      ringRotation[5] = ringRotation[2] * reflection.x * reflection.z;
      ringRotation[6] = ringRotation[2] * reflection.y * reflection.y;
      ringRotation[7] = ringRotation[2] * reflection.y * reflection.z;
      ringRotation[8] = ringRotation[2] * reflection.z * reflection.z;
      ringRotation[9] = ringRotation[1] * reflection.x;
      ringRotation[10] = ringRotation[1] * reflection.y;
      ringRotation[11] = ringRotation[1] * reflection.z;

      ringVector.x = (ringRotation[3] + ringRotation[0]) * ringSeed.x + (ringRotation[4] - ringRotation[11]) * ringSeed.y +
                     (ringRotation[5] + ringRotation[10]) * ringSeed.z;
      ringVector.y = (ringRotation[4] + ringRotation[11]) * ringSeed.x + (ringRotation[6] + ringRotation[0]) * ringSeed.y +
                     (ringRotation[7] - ringRotation[9]) * ringSeed.z;
      ringVector.z = (ringRotation[5] - ringRotation[10]) * ringSeed.x + (ringRotation[7] + ringRotation[9]) * ringSeed.y +
                     (ringRotation[8] + ringRotation[0]) * ringSeed.z;
      ringVector = normalize(ringVector);

      dotProduct = dot(ringVector, _ray->Normal);
      if (dotProduct < 0)
        break;
      if (dotProduct < fov)
        ringWeighting = weighting * dotProduct / fov;

      accumulateRay(geometry, setupTrace(_ray->Location, _ray->Normal, ringVector, depth, fov, ringWeighting * baseWeighting, input), _accumulator);

      ringVector.x = (ringRotation[3] + ringRotation[0]) * ringSeed.x + (ringRotation[4] + ringRotation[11]) * ringSeed.y +
                     (ringRotation[5] - ringRotation[10]) * ringSeed.z;
      ringVector.y = (ringRotation[4] - ringRotation[11]) * ringSeed.x + (ringRotation[6] + ringRotation[0]) * ringSeed.y +
                     (ringRotation[7] + ringRotation[9]) * ringSeed.z;
      ringVector.z = (ringRotation[5] + ringRotation[10]) * ringSeed.x + (ringRotation[7] - ringRotation[9]) * ringSeed.y +
                     (ringRotation[8] + ringRotation[0]) * ringSeed.z;
      ringVector = normalize(ringVector);

      accumulateRay(geometry, setupTrace(_ray->Location, _ray->Normal, ringVector, depth, fov, ringWeighting * baseWeighting, input), _accumulator);
    }
  }
}

// Writing
// Combine _data colour+opacity with background colour and write to output
RayData resolveBackgroundRayData(TraceData data) {
  RayData ray = {.RayLength = 100, .Direction = data.Direction, .Location = data.Location, .ConeDepth = data.TraceFoV, .Opacity = 0, .Depth = 1};
  float dotprod = dot(data.Direction, (float3)(0, 0, -1));
  if (dotprod > 0.8) {
    ray.ColourR = 255;
    ray.ColourG = 255;
    ray.ColourB = 255;
    ray.Luminosity = dotprod * 150.0f;
  } else {
    ray.ColourR = 135;
    ray.ColourG = 206;
    ray.ColourB = 235;
    ray.Luminosity = 10.0f;
  }
  return ray;
}

RayData resolveRayData(Block surfaceData, TraceData *_data) {
  RayData ray;
  ray.Normal = normalVector(surfaceData.NormalPitch, surfaceData.NormalYaw);
  ray.ColourR = surfaceData.ColourR;
  ray.ColourG = surfaceData.ColourG;
  ray.ColourB = surfaceData.ColourB;
  ray.Opacity = 255;
  ray.Luminosity = 0;
  ray.Location = _data->Location;
  float3 position = (float3)(ulongToFloat(_data->Location.x), ulongToFloat(_data->Location.y), ulongToFloat(_data->Location.z));
  ray.RayLength = length(position - _data->Origin);
  ray.Direction = _data->Direction;
  ray.ConeDepth = _data->ConeDepth;
  ray.Depth = _data->Depth;
  return ray;
}

void writeToAccumulator(RayData overlay, float weighting, RayAccumulator *_accumulator) {
  _accumulator->TotalWeighting += weighting;
  if (overlay.Luminosity == 0)
    return;
  _accumulator->ColourR += (overlay.ColourR / 255.0f) * weighting * overlay.Luminosity;
  _accumulator->ColourG += (overlay.ColourG / 255.0f) * weighting * overlay.Luminosity;
  _accumulator->ColourB += (overlay.ColourB / 255.0f) * weighting * overlay.Luminosity;
}

void draw(__write_only image2d_t outputImage, RayData ray, RayAccumulator accumulator, int2 coord) {
  if (accumulator.TotalWeighting > 0.01f)
    write_imagef(outputImage, coord,
                 (float4)(fmin(native_divide(ray.ColourR + (accumulator.ColourR / accumulator.TotalWeighting), 255.0f), 1),
                          fmin(native_divide(ray.ColourG + (accumulator.ColourG / accumulator.TotalWeighting), 255.0f), 1),
                          fmin(native_divide(ray.ColourB + (accumulator.ColourB / accumulator.TotalWeighting), 255.0f), 1), 1));
  else
    write_imagef(outputImage, coord,
                 (float4)(native_divide(ray.ColourR, 255.0f), native_divide(ray.ColourG, 255.0f), native_divide(ray.ColourB, 255.0f), 1));
}

// Tree Management

void helpDereference(global Block *blocks, global Usage *usage, global uint *parentSize, global bool *parentResidency, global Parent *parents,
                     global uint2 *dereferenceQueue, global int *dereferenceRemaining, global int *semaphor, ushort tick) {
  // All local threads get to play the dereferencing game
  getSemaphor(semaphor);
  int localRemaining = atomic_dec(dereferenceRemaining);
  uint2 address2;
  while (localRemaining >= 0) {
    address2 = dereferenceQueue[localRemaining];
    releaseSemaphor(semaphor);
    // if Tick is USHRT_MAX - 1 then it has multiple parents
    if (usage[address2.y >> 3].Tick == USHRT_MAX - 1) {
      uint parent = usage[address2.y >> 3].Parent;
      uint previousParent = parent;
      bool finished = false;
      bool first = true;
      bool last = false;
      while (!finished) {
        if (parents[parent].ParentAddress == address2.x) {
          // While loop locks parents[parent].NextElement to this thread
          uint nextElement = UINT_MAX - 1;
          while (nextElement == UINT_MAX - 1) {
            nextElement = atomic_xchg(&parents[parent].NextElement, UINT_MAX - 1);
          }

          // Last element in the list so previous element becomes the last
          // element
          if (nextElement == UINT_MAX) {
            parentResidency[parent] = false;
            atomic_xchg(&parents[previousParent].NextElement, UINT_MAX);
            atomic_xchg(&parents[parent].NextElement, UINT_MAX);
            first = usage[address2.y >> 3].Parent == previousParent;
          }
          // Move next element forwards one
          else {
            atomic_xchg(&parents[parent].ParentAddress, parents[nextElement].ParentAddress);
            atomic_xchg(&parents[parent].NextElement, parents[nextElement].NextElement);
            parentResidency[nextElement] = false;
            last = parents[parent].NextElement == UINT_MAX;
          }
          finished = true;
          break;
        } else if (parents[parent].NextElement == UINT_MAX) {
          finished = true;
        } else {
          previousParent = parent;
          parent = parents[parent].NextElement;
        }
        first = false;
      }
      // If the parent removed from the list was the first and last then the
      // block is no longer multi parent
      if (first && last) {
        parentResidency[parent] = false;
        usage[address2.y >> 3].Tick = tick;
        atomic_xchg(&usage[address2.y >> 3].Parent, parents[parent].ParentAddress);
      }
    } else
      usage[address2.y >> 3].Tick = 0;

    // This creates additional children which could be spread amongst the loops
    for (uint i = 0; i < 8; i++) {
      uint childAddress = blocks[address2.y + i].Child;
      if (childAddress != UINT_MAX && usage[childAddress >> 3].Tick < USHRT_MAX) {
        getSemaphor(semaphor);
        localRemaining = atomic_inc(dereferenceRemaining);
        dereferenceQueue[localRemaining] = (uint2)(address2.y, childAddress);
        releaseSemaphor(semaphor);
      }
    }
    getSemaphor(semaphor);
    localRemaining = atomic_dec(dereferenceRemaining);
  }
  releaseSemaphor(semaphor);
}

void dereference(global Block *blocks, global Usage *usage, global uint *parentSize, global bool *parentResidency, global Parent *parents,
                 global uint2 *dereferenceQueue, global int *dereferenceRemaining, global int *semaphor, uint startAddress, ushort tick) {
  // Build up the initial set of children to cull
  uint address = atomic_xchg(&blocks[startAddress].Child, UINT_MAX);
  int localRemaining = 0;
  if (address != UINT_MAX)
    for (uint i = 0; i < 8; i++) {
      uint childAddress = blocks[address + i].Child;
      if (childAddress != UINT_MAX && usage[childAddress >> 3].Tick < USHRT_MAX) {
        // Semaphors are used to prevent dereferenceQueue being overwritten
        getSemaphor(semaphor);
        localRemaining = atomic_inc(dereferenceRemaining);
        dereferenceQueue[localRemaining] = (uint2)(address, childAddress);
        releaseSemaphor(semaphor);
      }
    }
  helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, tick);
}

uint findAddress(global Block *blocks, global Usage *usage, global uint *childRequestId, global ChildRequest *childRequests, global uint *parentSize,
                 global bool *parentResidency, global Parent *parents, global uint2 *dereferenceQueue, global int *dereferenceRemaining,
                 global int *semaphor, global ulong *addresses, UpdateInputData inputData, uint address, uint depth) {
  ulong3 location = (ulong3)(addresses[address], addresses[address + 1], addresses[address + 2]);
  address = baseLocation(inputData.BaseDepth + 2, location);
  for (uchar i = inputData.BaseDepth + 2; i < depth; i++) {
    if (usage[address >> 3].Tick < USHRT_MAX - 1) {
      usage[address >> 3].Tick = inputData.Tick;
    }
    // Hit the bottom of the tree and not found it
    if (blocks[address].Child == UINT_MAX) {
      Geometry geometry;
      requestChild(geometry, buildChildRequest(address, i, inputData.Tick, depth - i, location));
      helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
      return UINT_MAX;
    }
    address = blocks[address].Child + chunkPosition(i, location);
  }
  return address;
}

//****************KERNELS******************

kernel void prune(global ushort *bases, global Block *blocks, global Usage *usage, global uint *childRequestId, global ChildRequest *childRequests,
                  global uint *parentSize, global bool *parentResidency, global Parent *parents, global uint2 *dereferenceQueue,
                  global int *dereferenceRemaining, global int *semaphor, global Pruning *pruning, global Block *pruningSurfaceData,
                  global ulong *pruningAddresses, UpdateInputData inputData) {
  uint x = get_global_id(0);
  Pruning myPruning = pruning[x];
  uint address = myPruning.Address;

  // Update base block chunk data
  if ((myPruning.Properties >> 4 & 1) == 1) {
    bases[address] = myPruning.Chunk;
    helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
    return;
  }

  // If depth is UCHAR_MAX then this is a reference to a specific value
  if (myPruning.Depth != UCHAR_MAX) {
    address = findAddress(blocks, usage, childRequestId, childRequests, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining,
                          semaphor, pruningAddresses, inputData, address, myPruning.Depth);
    if (address == UINT_MAX)
      return;
  } else {
    // Tick of 0 means that this has been dereferenced
    if (usage[address >> 3].Tick == 0) {
      helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
      return;
    } else if (usage[address >> 3].Tick < USHRT_MAX - 1) {
      usage[address >> 3].Tick = inputData.Tick;
    }
  }

  // CullChild
  if ((myPruning.Properties & 1) == 1) {
    dereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, address, inputData.Tick);
    blocks[address].Child = myPruning.ChildAddress;
  } else {
    helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
  }

  // AlterViolability & MakeInviolate
  if ((myPruning.Properties >> 1 & 3) == 3) {
    usage[address >> 3].Tick = USHRT_MAX;
  } else if ((myPruning.Properties >> 1 & 3) == 1) {
    usage[address >> 3].Tick = inputData.Tick;
  }

  // UpdateChunk
  if ((myPruning.Properties >> 3 & 1) == 1) {
    blocks[address] = pruningSurfaceData[x];
    blocks[address].Chunk = myPruning.Chunk;
  }
}

kernel void graft(global Block *blocks, global Usage *usage, global uint *childRequestId, global ChildRequest *childRequests, global uint *parentSize,
                  global bool *parentResidency, global Parent *parents, global uint2 *dereferenceQueue, global int *dereferenceRemaining,
                  global int *semaphor, global Grafting *grafting, global Block *graftingBlocks, global ulong *graftingAddresses,
                  global uint *holdingAddresses, global uint *addressPosition, UpdateInputData inputData) {
  uint id = get_global_id(0);
  uint workSize = get_global_size(0);
  uint iterator = (uint)native_divide((float)workSize, (float)(id * inputData.MemorySize));
  uint baseIterator = iterator;
  uint maxIterator = (uint)native_divide((float)((id + 1) * inputData.MemorySize), (float)workSize) - 1;
  uint workingTick;
  uint offset = inputData.Offset;
  // Accumulate graft array
  while (inputData.GraftSize < *addressPosition) {
    workingTick = usage[iterator].Tick;
    // Ensure that usage is not inviolable and is at least offset ticks ago
    if (workingTick == 0 ||
        (workingTick < USHRT_MAX - 1 && ((workingTick > inputData.Tick && (workingTick - USHRT_MAX - 2) < (inputData.Tick - offset)) ||
                                         (workingTick < inputData.Tick && workingTick < (inputData.Tick - offset))))) {
      uint myAddressPosition = atomic_inc(addressPosition);
      // Break out if address limit has already been reached
      if (myAddressPosition >= inputData.GraftSize) {
        helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
        break;
      }
      holdingAddresses[myAddressPosition] = iterator;
      dereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor,
                  usage[myAddressPosition].Parent, inputData.Tick);
      // Ensure that the address isn't picked up on a second pass
      usage[myAddressPosition].Tick = inputData.Tick;
    }

    if (iterator == maxIterator) {
      iterator = baseIterator;
      offset = offset >> 1;
    } else
      iterator++;
    helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
  }
  Grafting myGrafting = grafting[id];
  uint address = myGrafting.GraftAddress;
  // Seek out true address if the grafting address is just a set of coordinates
  if (myGrafting.Depth != UCHAR_MAX) {
    address = findAddress(blocks, usage, childRequestId, childRequests, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining,
                          semaphor, graftingAddresses, inputData, address, myGrafting.Depth);
    if (address == UINT_MAX)
      return;
    if (blocks[address].Child != UINT_MAX)
      dereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, address, inputData.Tick);
    else
      helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
  } else
    helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);

  uint3 depthHeap[64];
  uint blockAddress = holdingAddresses[myGrafting.GraftDataAddress] << 3;
  blocks[address].Child = blockAddress;
  //(Address in graft tree, address in block tree, superblock position)
  depthHeap[0] = (uint3)(myGrafting.GraftDataAddress, blockAddress, 0);
  uchar depth = 0;
  uint i = 0;
  while (depth >= 0) {
    while (depthHeap[depth].z < 8) {
      uint3 heapValue = depthHeap[depth];
      Block block = graftingBlocks[heapValue.x + heapValue.z];
      uint blockChild = block.Child;
      block.Child = UINT_MAX;
      blocks[heapValue.y + heapValue.z] = block;
      depthHeap[depth].z++;
      if (blockChild != UINT_MAX) {
        i++;
        blockAddress = holdingAddresses[i + myGrafting.GraftDataAddress] << 3;
        blocks[heapValue.y + heapValue.z].Child = blockAddress;
        usage[blockAddress].Parent = heapValue.y + heapValue.z;
        depth++;
        depthHeap[depth] = (uint3)(blockChild, blockAddress, 0);
        break;
      }
    }

    if (depthHeap[depth].z == 8)
      depth--;
  }
}

kernel void trace(global ushort *bases, global Block *blocks, global Usage *usage, global uint *childRequestId, global ChildRequest *childRequests,
                  __write_only image2d_t outputImage, TraceInput input) {
  if (input.FovConstant <= 0 || input.FovMultiplier >= 1 || input.FovMultiplier <= -1)
    return;

  int2 coord = (int2)((int)get_global_id(0), (int)get_global_id(1));
  TraceData data = setupInitialTrace(coord, input);
  RayAccumulator accumulator = {.TotalWeighting = 0, .ColourR = 0, .ColourG = 0, .ColourB = 0};

  // Move ray to bounding volume
  if (!traceIntoVolume(&data)) {
    // Bounding volume is missed entirely
    draw(outputImage, resolveBackgroundRayData(data), accumulator, coord);
    return;
  }
  Geometry geometry = {.bases = bases, .blocks = blocks, .usage = usage, .childRequestId = childRequestId, .childRequests = childRequests, .baseDepth = input.BaseDepth};

  // Perform initial trace
  RayData ray = traceRay(geometry, data);
  // Draw result if it hits skybox
  if (ray.Opacity == 0)
    draw(outputImage, ray, accumulator, coord);

  // Accumulate light
  spawnRays(geometry, &ray, &accumulator, 1, input);
  // Apply accumulated light to ray
  draw(outputImage, ray, accumulator, coord);
}
