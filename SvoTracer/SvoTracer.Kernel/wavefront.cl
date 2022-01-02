typedef struct {
  uint Child;
  ushort Chunk;
  short NormalPitch;
  short NormalYaw;
  uchar ColourR;
  uchar ColourG;
  uchar ColourB;
  uchar Opacity;
  uchar Spare;
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
  uchar AmbientLightLevel;
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
  float FoV;
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
  uchar Weighting;
  uchar Depth;
  uint ID;
} TraceData;

typedef struct {
  global ushort *Bases;
  global Block *Blocks;
  global Usage *Usages;
  global uint *ChildRequestID;
  global ChildRequest *ChildRequests;
  global float3 *Origins;
  global float3 *Directions;
  global float *FoVs;
  global ulong3 *Locations;
  global uchar *Depths;
  global uchar *Weightings;
  global uint *ParentTraces;
  global uint *BaseTraces;
  global uint *BlockTraces;
  global uint *BlockTraceQueue;
  global uint *BlockTraceQueueID;
  global uint *BaseTraceQueue;
  global uint *BaseTraceQueueID;
  global uchar *ColourRs;
  global uchar *ColourGs;
  global uchar *ColourBs;
  global uchar *Luminosities;
  global float *RayLengths;
  global uint *FinalWeightings;
  global uint *BackgroundQueue;
  global uint *BackgroundQueueID;
  global uint *MaterialQueue;
  global uint *MaterialQueueID;
  global uint *AccumulatorID;
  const uchar BaseDepth;
  const uchar AmbientLightLevel;
} Buffers;

// Inline math
ulong floatToUlong(float x);
float ulongToFloat(ulong x);
ulong roundUlong(ulong value, uchar depth, bool roundUp);
uint powSum(uchar depth);
void getSemaphor(global int *semaphor);
void releaseSemaphor(global int *semaphor);
float3 normalVector(short pitch, short yaw);

// Memory
ushort getBase(Buffers buffers, uchar depth, ulong3 location);
Block getBlock(Buffers buffers, uint address);
uint getBlockAddress(Buffers buffers, uchar depth, ulong3 location);
void saveBaseTrace(Buffers buffers, uint traceID);
void saveBlockTrace(Buffers buffers, uint traceID);
void saveNextStep(Buffers, TraceData *_trace);
void saveBackground(Buffers, TraceData trace);
void saveMaterial(Buffers, TraceData trace);
void saveAccumulator(Buffers buffers, TraceData trace, uchar colourR, uchar colourG, uchar colourB, uint parentID);
void updateTrace(Buffers buffers, TraceData trace);
void requestChild(Buffers buffers, uint address, uchar depth, ushort tick, uchar treeSize, ulong3 location);
void updateUsage(Buffers buffers, uint address, ushort tick);

// Tree
float coneSize(float m, TraceData trace);
void setConeDepth(TraceData *_trace);

// Octree
uint baseLocation(uchar depth, ulong3 location);
uchar comparePositions(uchar depth, ulong3 previousLocation, TraceData trace);
uchar chunkPosition(uchar depth, ulong3 location);
bool leaving(TraceData trace);
void traverseChunk(uchar depth, TraceData *_trace);
void traverseChunkNormal(uchar depth, float3 normal, TraceData *_trace);
TraceData setupInitialTrace(TraceInput input);
TraceData resolveTrace(Buffers buffers, float2 DoF, uint id);
TraceData setupTrace(ulong3 location, float3 normal, float3 direction, uchar depth, float fov, uchar weighting, TraceInput input);
bool traceIntoVolume(TraceData *_trace);
Block average(uint address, Buffers buffers, TraceData trace);

// Tree Management
void helpDereference(global Block *Blocks, global Usage *Usages, global uint *parentSize, global bool *parentResidency, global Parent *parents,
                     global uint2 *dereferenceQueue, global int *dereferenceRemaining, global int *semaphor, ushort tick);
void dereference(global Block *Blocks, global Usage *Usages, global uint *parentSize, global bool *parentResidency, global Parent *parents,
                 global uint2 *dereferenceQueue, global int *dereferenceRemaining, global int *semaphor, uint startAddress, ushort tick);
uint findAddress(global Block *Blocks, global Usage *Usages, global uint *ChildRequestID, global ChildRequest *ChildRequests, global uint *parentSize,
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

ushort getBase(Buffers buffers, uchar depth, ulong3 location) {
  uint address = powSum(depth - 1) + baseLocation(depth, location);
  return buffers.Bases[address];
}

Block getBlock(Buffers buffers, uint address) { return buffers.Blocks[address]; }

uint getBlockAddress(Buffers buffers, uchar depth, ulong3 location) {
  uint address = baseLocation(buffers.BaseDepth + 2, location) + chunkPosition(buffers.BaseDepth + 2, location);
  Block block;
  for (uchar d = buffers.BaseDepth + 3; d <= depth; d++) {
    block = buffers.Blocks[address];
    if (block.Child == UINT_MAX)
      return UINT_MAX;
    address = block.Child + chunkPosition(d, location);
  }
  return address;
}

void saveBaseTrace(Buffers buffers, uint traceID) {
  uint id = atomic_inc(buffers.BaseTraceQueueID);
  buffers.BaseTraceQueue[id] = traceID;
}

void saveBlockTrace(Buffers buffers, uint traceID) {
  uint id = atomic_inc(buffers.BlockTraceQueueID);
  buffers.BlockTraceQueue[id] = traceID;
}

void saveNextStep(Buffers buffers, TraceData *_trace) {
  if (_trace->Depth == buffers.BaseDepth + 1) {
    _trace->Depth = buffers.BaseDepth;
    saveBaseTrace(buffers, _trace->ID);
  } else if (_trace->Depth > (buffers.BaseDepth + 1))
    saveBlockTrace(buffers, _trace->ID);
  else
    saveBaseTrace(buffers, _trace->ID);
}

void saveBackground(Buffers buffers, TraceData trace) {
  uint id = atomic_inc(buffers.BackgroundQueueID);
  buffers.BackgroundQueue[id] = trace.ID;
}

void saveMaterial(Buffers buffers, TraceData trace) {
  uint id = atomic_inc(buffers.MaterialQueueID);
  buffers.MaterialQueue[id] = trace.ID;
}

void saveAccumulator(Buffers buffers, TraceData trace, uchar colourR, uchar colourG, uchar colourB, uint parentID) {
  trace.ID = atomic_inc(buffers.AccumulatorID);
  buffers.ParentTraces[trace.ID] = parentID;
  buffers.Weightings[trace.ID] = trace.Weighting;

  if (trace.Weighting > 5) {
    saveBaseTrace(buffers, trace.ID);
    buffers.Luminosities[trace.ID] = 0;
    buffers.ColourRs[trace.ID] = colourR;
    buffers.ColourGs[trace.ID] = colourG;
    buffers.ColourBs[trace.ID] = colourB;
    buffers.Origins[trace.ID] = trace.Origin;
    buffers.Directions[trace.ID] = trace.Direction;
    buffers.FoVs[trace.ID] = trace.FoV;
    buffers.Locations[trace.ID] = trace.Location;
    buffers.Depths[trace.ID] = trace.Depth;
  }
}

void updateTrace(Buffers buffers, TraceData trace) {
  buffers.Locations[trace.ID] = trace.Location;
  buffers.Depths[trace.ID] = trace.Depth;
}

void requestChild(Buffers buffers, uint address, uchar depth, ushort tick, uchar treeSize, ulong3 location) {
  uint id = atomic_inc(buffers.ChildRequestID);
  ChildRequest request = {.Address = address,
                          .Tick = tick,
                          .Depth = depth,
                          .Location[0] = location.x,
                          .Location[1] = location.y,
                          .Location[2] = location.z,
                          .TreeSize = treeSize};
  buffers.ChildRequests[id] = request;
}

void updateUsage(Buffers buffers, uint address, ushort tick) {
  Usage usageVal = buffers.Usages[address];
  if (usageVal.Tick < USHRT_MAX - 1 && usageVal.Tick != tick)
    buffers.Usages[address].Tick = tick;
}

// Tree

// Calculates the cone size at a given depth from FoV and pixel diameter trace
// Largest cone size wins
float coneSize(float m, TraceData trace) {
  float eye = trace.FoV * m;
  float fov = fabs(trace.DoF.x * (trace.DoF.y - m));
  return max(eye, fov);
}

// Determine the maximum tree depth for a cone at this location
void setConeDepth(TraceData *_trace) {
  _trace->ConeDepth = -half_log2(
      coneSize(fabs(fast_length((float3)(_trace->Origin.x - ulongToFloat(_trace->Location.x), _trace->Origin.y - ulongToFloat(_trace->Location.y),
                                         _trace->Origin.z - ulongToFloat(_trace->Location.z)))),
               *_trace));
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

uchar comparePositions(uchar depth, ulong3 previousLocation, TraceData trace) {
  uchar newPosition = chunkPosition(depth, trace.Location);
  uchar previousPosition = chunkPosition(depth, previousLocation);
  while (((previousPosition & 1) == trace.DirectionSignX && (newPosition & 1) != trace.DirectionSignX) ||
         (((previousPosition >> 1) & 1) == trace.DirectionSignY && ((newPosition >> 1) & 1) != trace.DirectionSignY) ||
         (((previousPosition >> 2) & 1) == trace.DirectionSignZ && ((newPosition >> 2) & 1) != trace.DirectionSignZ)) {
    if (depth == 1)
      break;
    depth--;
    newPosition = chunkPosition(depth, trace.Location);
    previousPosition = chunkPosition(depth, previousLocation);
  }
  return depth;
}

uchar chunkPosition(uchar depth, ulong3 location) {
  return ((location.x >> (64 - depth - 1) & 1) + ((location.y >> (64 - depth - 1) & 1) << 1) + ((location.z >> (64 - depth - 1) & 1) << 2));
}

// Determines whether cone is leaving the octree
bool leaving(TraceData trace) {
  return (!trace.DirectionSignX && trace.Location.x == 0) || (!trace.DirectionSignY && trace.Location.y == 0) ||
         (!trace.DirectionSignZ && trace.Location.z == 0) || (trace.DirectionSignX && trace.Location.x == ULONG_MAX) ||
         (trace.DirectionSignY && trace.Location.y == ULONG_MAX) || (trace.DirectionSignZ && trace.Location.z == ULONG_MAX);
}

// Moves to the nearest neighboring chunk along the Direction vector
void traverseChunk(uchar depth, TraceData *_trace) {
  // determine distance from current location to x, y, z chunk boundary
  ulong dx = roundUlong(_trace->Location.x, depth, _trace->DirectionSignX);
  ulong dy = roundUlong(_trace->Location.y, depth, _trace->DirectionSignY);
  ulong dz = roundUlong(_trace->Location.z, depth, _trace->DirectionSignZ);

  // calulate the shortest of the three lengths
  float ax = fabs(dx * _trace->InvDirection.x);
  float ay = fabs(dy * _trace->InvDirection.y);
  float az = fabs(dz * _trace->InvDirection.z);

  if (ax <= ay && ax <= az) {
    float udx = ulongToFloat(dx);
    dy = floatToUlong(_trace->Direction.y * _trace->InvDirection.x * udx);
    dz = floatToUlong(_trace->Direction.z * _trace->InvDirection.x * udx);
  } else if (ay <= ax && ay <= az) {
    float udy = ulongToFloat(dy);
    dx = floatToUlong(_trace->Direction.x * _trace->InvDirection.y * udy);
    dz = floatToUlong(_trace->Direction.z * _trace->InvDirection.y * udy);
  } else {
    float udz = ulongToFloat(dz);
    dx = floatToUlong(_trace->Direction.x * _trace->InvDirection.z * udz);
    dy = floatToUlong(_trace->Direction.y * _trace->InvDirection.z * udz);
  }

  if (_trace->DirectionSignX)
    _trace->Location.x += dx;
  else
    _trace->Location.x -= dx;

  if (_trace->DirectionSignY)
    _trace->Location.y += dy;
  else
    _trace->Location.y -= dy;

  if (_trace->DirectionSignZ)
    _trace->Location.z += dz;
  else
    _trace->Location.z -= dz;

  // if trafersal has overflowed ulong then the octree has been left
  if (_trace->DirectionSignX && _trace->Location.x == 0)
    _trace->Location.x = ULONG_MAX;
  else if (!_trace->DirectionSignX && _trace->Location.x == ULONG_MAX)
    _trace->Location.x = 0;

  if (_trace->DirectionSignY && _trace->Location.y == 0)
    _trace->Location.y = ULONG_MAX;
  else if (!_trace->DirectionSignY && _trace->Location.y == ULONG_MAX)
    _trace->Location.y = 0;

  if (_trace->DirectionSignZ && _trace->Location.z == 0)
    _trace->Location.z = ULONG_MAX;
  else if (!_trace->DirectionSignZ && _trace->Location.z == ULONG_MAX)
    _trace->Location.z = 0;

  setConeDepth(_trace);
}

// Moves to the furthest neighboring chunk along the Direction vector
void traverseChunkNormal(uchar depth, float3 normal, TraceData *_trace) {
  bool directionSignX = normal.x >= 0;
  bool directionSignY = normal.y >= 0;
  bool directionSignZ = normal.z >= 0;
  float3 inv = native_divide(1, normal);
  // determine distance from current location to x, y, z chunk boundary
  ulong dx = roundUlong(_trace->Location.x, depth, directionSignX);
  ulong dy = roundUlong(_trace->Location.y, depth, directionSignY);
  ulong dz = roundUlong(_trace->Location.z, depth, directionSignZ);

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
    _trace->Location.x += dx;
  else
    _trace->Location.x -= dx;

  if (directionSignY)
    _trace->Location.y += dy;
  else
    _trace->Location.y -= dy;

  if (directionSignZ)
    _trace->Location.z += dz;
  else
    _trace->Location.z -= dz;

  // if trafersal has overflowed ulong then the octree has been left
  if (directionSignX && _trace->Location.x == 0)
    _trace->Location.x = ULONG_MAX;
  else if (!directionSignX && _trace->Location.x == ULONG_MAX)
    _trace->Location.x = 0;

  if (directionSignY && _trace->Location.y == 0)
    _trace->Location.y = ULONG_MAX;
  else if (!directionSignY && _trace->Location.y == ULONG_MAX)
    _trace->Location.y = 0;

  if (directionSignZ && _trace->Location.z == 0)
    _trace->Location.z = ULONG_MAX;
  else if (!directionSignZ && _trace->Location.z == ULONG_MAX)
    _trace->Location.z = 0;

  setConeDepth(_trace);
}

TraceData setupInitialTrace(TraceInput input) {
  int2 coord = (int2)((int)get_global_id(0), (int)get_global_id(1));
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
      .FoV = native_divide(input.FoV[0], input.ScreenSize.x),
      .MaxChildRequestId = input.MaxChildRequestId,
      .Weighting = 255,
      .Depth = 1,
      .ID = coord.x + coord.y * input.ScreenSize.x,
  };
  return traceData;
}

TraceData resolveTrace(Buffers buffers, float2 dof, uint id) {
  float3 direction = buffers.Directions[id];
  TraceData trace = {
      .Origin = buffers.Origins[id],
      .Direction = direction,
      .DoF = dof,
      .FoV = buffers.FoVs[id],
      .Location = buffers.Locations[id],
      .Weighting = buffers.Weightings[id],
      .Depth = buffers.Depths[id],
      .ID = id,
      .InvDirection = native_divide(1, direction),
      .DirectionSignX = direction.x >= 0,
      .DirectionSignY = direction.y >= 0,
      .DirectionSignZ = direction.z >= 0,
  };
  setConeDepth(&trace);
  return trace;
}

TraceData setupTrace(ulong3 location, float3 normal, float3 direction, uchar depth, float fov, uchar weighting, TraceInput input) {
  TraceData trace = {
      .Origin = (float3)(ulongToFloat(location.x), ulongToFloat(location.y), ulongToFloat(location.z)),
      .Direction = direction,
      .InvDirection = (float3)(native_divide(1, direction.x), native_divide(1, direction.y), native_divide(1, direction.z)),
      .DirectionSignX = direction.x >= 0,
      .DirectionSignY = direction.y >= 0,
      .DirectionSignZ = direction.z >= 0,
      .DoF = (float2)(0, 0),
      .FoV = fov,
      .Tick = input.Tick,
      .ConeDepth = 0,
      .Location = location,
      .MaxChildRequestId = input.MaxChildRequestId,
      .Weighting = weighting,
      .Depth = 1,
  };
  traverseChunkNormal(depth, normal, &trace);
  return trace;
}

// Sets chunk and determines whether the ray hits the octree
bool traceIntoVolume(TraceData *_trace) {
  bool x0 = _trace->Origin.x < 0;
  bool x1 = _trace->Origin.x > 1;
  bool xp = _trace->Direction.x == 0;
  bool xd = _trace->Direction.x >= 0;
  bool y0 = _trace->Origin.y < 0;
  bool y1 = _trace->Origin.y > 1;
  bool yp = _trace->Direction.y == 0;
  bool yd = _trace->Direction.y >= 0;
  bool z0 = _trace->Origin.z < 0;
  bool z1 = _trace->Origin.z > 1;
  bool zp = _trace->Direction.z == 0;
  bool zd = _trace->Direction.z >= 0;
  float locationX = _trace->Origin.x;
  float locationY = _trace->Origin.y;
  float locationZ = _trace->Origin.z;
  float m = 0;
  float mx = 0;
  float my = 0;
  float mz = 0;
  int xyz = (x0 ? 0b100000 : 0) + (x1 ? 0b010000 : 0) + (y0 ? 0b001000 : 0) + (y1 ? 0b000100 : 0) + (z0 ? 0b000010 : 0) + (z1 ? 0b000001 : 0);
  if (xyz == 0) {
    _trace->Location.x = floatToUlong(locationX);
    _trace->Location.y = floatToUlong(locationY);
    _trace->Location.z = floatToUlong(locationZ);
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
    m = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    locationX = 0;
    locationY = _trace->Origin.y + (_trace->Direction.y * m);
    locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    break;
  case 0b010000: // x1
    m = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    locationX = 1;
    locationY = _trace->Origin.y + (_trace->Direction.y * m);
    locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    break;
  case 0b001000: // y0
    m = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    locationX = _trace->Origin.x + (_trace->Direction.x * m);
    locationY = 0;
    locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    break;
  case 0b000100: // y1
    m = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    locationX = _trace->Origin.x + (_trace->Direction.x * m);
    locationY = 1;
    locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    break;
  case 0b000010: // z0
    m = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    locationX = _trace->Origin.x + (_trace->Direction.x * m);
    locationY = _trace->Origin.y + (_trace->Direction.y * m);
    locationZ = 0;
    break;
  case 0b000001: // z1
    m = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    locationX = _trace->Origin.x + (_trace->Direction.x * m);
    locationY = _trace->Origin.y + (_trace->Direction.y * m);
    locationZ = 1;
    break;
  // The 8 side arcs outside of the box between two of the faces on one axis and
  // near to two faces on the other two axies z face
  case 0b101000: // x0y0
    mx = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    if (mx >= my) {
      m = mx;
      locationX = 0;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 0;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    }
    break;
  case 0b011000: // x1y0
    mx = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    if (mx >= my) {
      m = mx;
      locationX = 1;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 0;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    }
    break;
  case 0b100100: // x0y1
    mx = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    if (mx >= my) {
      m = mx;
      locationX = 0;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 1;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    }
    break;
  case 0b010100: // x1y1
    mx = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    if (mx >= my) {
      m = mx;
      locationX = 1;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 1;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    }
    break;
  // y face
  case 0b100010: // x0z0
    mx = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    mz = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b010010: // x1z0
    mx = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    mz = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b100001: // x0z1
    mx = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    mz = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b010001: // x1z1
    mx = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    mz = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 1;
    }
    break;
  // x face
  case 0b001010: // y0z0
    my = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    if (my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 0;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b000110: // y1z0
    my = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    if (my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 1;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b001001: // y0z1
    my = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    if (my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 0;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b000101: // y1z1
    my = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    if (my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 1;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 1;
    }
    break;
  // The 8 corners
  case 0b101010: // x0y0z0
    mx = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 0;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b011010: // x1y0z0
    mx = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 0;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b100110: // x0y1z0
    mx = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 1;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b010110: // x1y1z0
    mx = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((0 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 1;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 0;
    }
    break;
  case 0b101001: // x0y0z1
    mx = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 0;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b011001: // x1y0z1
    mx = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((0 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 0;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b100101: // x0y1z1
    mx = fabs((0 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 0;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 1;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 1;
    }
    break;
  case 0b010101: // x1y1z1
    mx = fabs((1 - _trace->Origin.x) * _trace->InvDirection.x);
    my = fabs((1 - _trace->Origin.y) * _trace->InvDirection.y);
    mz = fabs((1 - _trace->Origin.z) * _trace->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      locationX = 1;
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = 1;
      locationZ = _trace->Origin.z + (_trace->Direction.z * m);
    } else {
      m = mz;
      locationX = _trace->Origin.x + (_trace->Direction.x * m);
      locationY = _trace->Origin.y + (_trace->Direction.y * m);
      locationZ = 1;
    }
    break;
  default:
    return false;
  }
  _trace->Location.x = floatToUlong(locationX);
  _trace->Location.y = floatToUlong(locationY);
  _trace->Location.z = floatToUlong(locationZ);
  float c = coneSize(m, *_trace);
  return !(locationX < -c || locationX > 1 + c || locationY < -c || locationY > 1 + c || locationZ < -c || locationZ > 1 + c);
}

Block average(uint address, Buffers buffers, TraceData trace) {
  // Average like heck
  return getBlock(buffers, address);
}

// Tree Management

void helpDereference(global Block *Blocks, global Usage *Usages, global uint *parentSize, global bool *parentResidency, global Parent *parents,
                     global uint2 *dereferenceQueue, global int *dereferenceRemaining, global int *semaphor, ushort tick) {
  // All local threads get to play the dereferencing game
  getSemaphor(semaphor);
  int localRemaining = atomic_dec(dereferenceRemaining);
  uint2 address2;
  while (localRemaining >= 0) {
    address2 = dereferenceQueue[localRemaining];
    releaseSemaphor(semaphor);
    // if Tick is USHRT_MAX - 1 then it has multiple parents
    if (Usages[address2.y >> 3].Tick == USHRT_MAX - 1) {
      uint parent = Usages[address2.y >> 3].Parent;
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
            first = Usages[address2.y >> 3].Parent == previousParent;
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
        Usages[address2.y >> 3].Tick = tick;
        atomic_xchg(&Usages[address2.y >> 3].Parent, parents[parent].ParentAddress);
      }
    } else
      Usages[address2.y >> 3].Tick = 0;

    // This creates additional children which could be spread amongst the loops
    for (uint i = 0; i < 8; i++) {
      uint childAddress = Blocks[address2.y + i].Child;
      if (childAddress != UINT_MAX && Usages[childAddress >> 3].Tick < USHRT_MAX) {
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

void dereference(global Block *Blocks, global Usage *Usages, global uint *parentSize, global bool *parentResidency, global Parent *parents,
                 global uint2 *dereferenceQueue, global int *dereferenceRemaining, global int *semaphor, uint startAddress, ushort tick) {
  // Build up the initial set of children to cull
  uint address = atomic_xchg(&Blocks[startAddress].Child, UINT_MAX);
  int localRemaining = 0;
  if (address != UINT_MAX)
    for (uint i = 0; i < 8; i++) {
      uint childAddress = Blocks[address + i].Child;
      if (childAddress != UINT_MAX && Usages[childAddress >> 3].Tick < USHRT_MAX) {
        // Semaphors are used to prevent dereferenceQueue being overwritten
        getSemaphor(semaphor);
        localRemaining = atomic_inc(dereferenceRemaining);
        dereferenceQueue[localRemaining] = (uint2)(address, childAddress);
        releaseSemaphor(semaphor);
      }
    }
  helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, tick);
}

uint findAddress(global Block *Blocks, global Usage *Usages, global uint *ChildRequestID, global ChildRequest *ChildRequests, global uint *parentSize,
                 global bool *parentResidency, global Parent *parents, global uint2 *dereferenceQueue, global int *dereferenceRemaining,
                 global int *semaphor, global ulong *addresses, UpdateInputData inputData, uint address, uint depth) {
  ulong3 location = (ulong3)(addresses[address], addresses[address + 1], addresses[address + 2]);
  address = baseLocation(inputData.BaseDepth + 2, location);
  for (uchar i = inputData.BaseDepth + 2; i < depth; i++) {
    if (Usages[address >> 3].Tick < USHRT_MAX - 1) {
      Usages[address >> 3].Tick = inputData.Tick;
    }
    // Hit the bottom of the tree and not found it
    if (Blocks[address].Child == UINT_MAX) {
      Buffers buffers;
      requestChild(buffers, address, i, inputData.Tick, depth - i, location);
      helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
      return UINT_MAX;
    }
    address = Blocks[address].Child + chunkPosition(i, location);
  }
  return address;
}

//****************KERNELS******************

kernel void prune(global ushort *Bases, global Block *Blocks, global Usage *Usages, global uint *ChildRequestID, global ChildRequest *ChildRequests,
                  global uint *parentSize, global bool *parentResidency, global Parent *parents, global uint2 *dereferenceQueue,
                  global int *dereferenceRemaining, global int *semaphor, global Pruning *pruning, global Block *pruningSurfaceData,
                  global ulong *pruningAddresses, UpdateInputData inputData) {
  uint x = get_global_id(0);
  Pruning myPruning = pruning[x];
  uint address = myPruning.Address;

  // Update base block chunk trace
  if ((myPruning.Properties >> 4 & 1) == 1) {
    Bases[address] = myPruning.Chunk;
    helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
    return;
  }

  // If depth is UCHAR_MAX then this is a reference to a specific value
  if (myPruning.Depth != UCHAR_MAX) {
    address = findAddress(Blocks, Usages, ChildRequestID, ChildRequests, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining,
                          semaphor, pruningAddresses, inputData, address, myPruning.Depth);
    if (address == UINT_MAX)
      return;
  } else {
    // Tick of 0 means that this has been dereferenced
    if (Usages[address >> 3].Tick == 0) {
      helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
      return;
    } else if (Usages[address >> 3].Tick < USHRT_MAX - 1) {
      Usages[address >> 3].Tick = inputData.Tick;
    }
  }

  // CullChild
  if ((myPruning.Properties & 1) == 1) {
    dereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, address, inputData.Tick);
    Blocks[address].Child = myPruning.ChildAddress;
  } else {
    helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
  }

  // AlterViolability & MakeInviolate
  if ((myPruning.Properties >> 1 & 3) == 3) {
    Usages[address >> 3].Tick = USHRT_MAX;
  } else if ((myPruning.Properties >> 1 & 3) == 1) {
    Usages[address >> 3].Tick = inputData.Tick;
  }

  // UpdateChunk
  if ((myPruning.Properties >> 3 & 1) == 1) {
    Blocks[address] = pruningSurfaceData[x];
    Blocks[address].Chunk = myPruning.Chunk;
  }
}

kernel void graft(global Block *Blocks, global Usage *Usages, global uint *ChildRequestID, global ChildRequest *ChildRequests,
                  global uint *parentSize, global bool *parentResidency, global Parent *parents, global uint2 *dereferenceQueue,
                  global int *dereferenceRemaining, global int *semaphor, global Grafting *grafting, global Block *graftingBlocks,
                  global ulong *graftingAddresses, global uint *holdingAddresses, global uint *addressPosition, UpdateInputData inputData) {
  uint id = get_global_id(0);
  uint workSize = get_global_size(0);
  uint iterator = (uint)native_divide((float)workSize, (float)(id * inputData.MemorySize));
  uint baseIterator = iterator;
  uint maxIterator = (uint)native_divide((float)((id + 1) * inputData.MemorySize), (float)workSize) - 1;
  uint workingTick;
  uint offset = inputData.Offset;
  // Accumulate graft array
  while (inputData.GraftSize < *addressPosition) {
    workingTick = Usages[iterator].Tick;
    // Ensure that Usages is not inviolable and is at least offset ticks ago
    if (workingTick == 0 ||
        (workingTick < USHRT_MAX - 1 && ((workingTick > inputData.Tick && (workingTick - USHRT_MAX - 2) < (inputData.Tick - offset)) ||
                                         (workingTick < inputData.Tick && workingTick < (inputData.Tick - offset))))) {
      uint myAddressPosition = atomic_inc(addressPosition);
      // Break out if address limit has already been reached
      if (myAddressPosition >= inputData.GraftSize) {
        helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
        break;
      }
      holdingAddresses[myAddressPosition] = iterator;
      dereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor,
                  Usages[myAddressPosition].Parent, inputData.Tick);
      // Ensure that the address isn't picked up on a second pass
      Usages[myAddressPosition].Tick = inputData.Tick;
    }

    if (iterator == maxIterator) {
      iterator = baseIterator;
      offset = offset >> 1;
    } else
      iterator++;
    helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
  }
  Grafting myGrafting = grafting[id];
  uint address = myGrafting.GraftAddress;
  // Seek out true address if the grafting address is just a set of coordinates
  if (myGrafting.Depth != UCHAR_MAX) {
    address = findAddress(Blocks, Usages, ChildRequestID, ChildRequests, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining,
                          semaphor, graftingAddresses, inputData, address, myGrafting.Depth);
    if (address == UINT_MAX)
      return;
    if (Blocks[address].Child != UINT_MAX)
      dereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, address, inputData.Tick);
    else
      helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
  } else
    helpDereference(Blocks, Usages, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);

  uint3 depthHeap[64];
  uint blockAddress = holdingAddresses[myGrafting.GraftDataAddress] << 3;
  Blocks[address].Child = blockAddress;
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
      Blocks[heapValue.y + heapValue.z] = block;
      depthHeap[depth].z++;
      if (blockChild != UINT_MAX) {
        i++;
        blockAddress = holdingAddresses[i + myGrafting.GraftDataAddress] << 3;
        Blocks[heapValue.y + heapValue.z].Child = blockAddress;
        Usages[blockAddress].Parent = heapValue.y + heapValue.z;
        depth++;
        depthHeap[depth] = (uint3)(blockChild, blockAddress, 0);
        break;
      }
    }

    if (depthHeap[depth].z == 8)
      depth--;
  }
}

kernel void init(global float3 *Origins, global float3 *Directions, global float *FoVs, global ulong3 *Locations, global uchar *Depths,
                 global uint *BaseTraceQueue, global uint *BaseTraceQueueID, global uint *BackgroundQueue, global uint *BackgroundQueueID,
                 global uint *ParentTraces, global uchar *Weightings, global uint *FinalColourRs, global uint *FinalColourGs,
                 global uint *FinalColourBs, global uint *FinalWeightings, TraceInput input) {
  Buffers buffers = {.Origins = Origins,
                     .Directions = Directions,
                     .FoVs = FoVs,
                     .Locations = Locations,
                     .Depths = Depths,
                     .BaseTraceQueue = BaseTraceQueue,
                     .BaseTraceQueueID = BaseTraceQueueID,
                     .BackgroundQueue = BackgroundQueue,
                     .BackgroundQueueID = BackgroundQueueID,
                     .Weightings = Weightings,
                     .ParentTraces = ParentTraces,
                     .BaseDepth = input.BaseDepth};
  TraceData trace = setupInitialTrace(input);
  // Move ray to bounding volume
  if (traceIntoVolume(&trace)) {
    saveBaseTrace(buffers, trace.ID);
    Locations[trace.ID] = trace.Location;
    Depths[trace.ID] = 1;
  } else
    saveBackground(buffers, trace);

  Origins[trace.ID] = trace.Origin;
  Directions[trace.ID] = trace.Direction;
  FoVs[trace.ID] = trace.FoV;
  ParentTraces[trace.ID] = trace.ID;
  Weightings[trace.ID] = trace.Weighting;
  FinalColourRs[trace.ID] = 0;
  FinalColourGs[trace.ID] = 0;
  FinalColourBs[trace.ID] = 0;
  FinalWeightings[trace.ID] = 0;
}

kernel void runBaseTrace(global ushort *Bases, global float3 *Origins, global float3 *Directions, global float *FoVs, global ulong3 *Locations,
                         global uchar *Weightings, global uchar *Depths, global uint *BaseTraces, global uint *BlockTraceQueue,
                         global uint *BlockTraceQueueID, global uint *BaseTraceQueue, global uint *BaseTraceQueueID, global uint *BackgroundQueue,
                         global uint *BackgroundQueueID, TraceInput input) {
  Buffers buffers = {.Bases = Bases,
                     .Origins = Origins,
                     .Directions = Directions,
                     .FoVs = FoVs,
                     .Locations = Locations,
                     .Weightings = Weightings,
                     .Depths = Depths,
                     .BaseTraces = BaseTraces,
                     .BlockTraceQueue = BlockTraceQueue,
                     .BlockTraceQueueID = BlockTraceQueueID,
                     .BaseTraceQueue = BaseTraceQueue,
                     .BaseTraceQueueID = BaseTraceQueueID,
                     .BackgroundQueue = BackgroundQueue,
                     .BackgroundQueueID = BackgroundQueueID,
                     .BaseDepth = input.BaseDepth};

  TraceData trace = resolveTrace(buffers, (float2)(input.DoF[0], input.DoF[1]), buffers.BaseTraces[get_global_id(0)]);
  ulong3 previousLocation = trace.Location;
  if (trace.Depth <= buffers.BaseDepth &&
      (getBase(buffers, trace.Depth, trace.Location) >> (chunkPosition(trace.Depth, trace.Location) * 2) & 2) != 2) {
    // current chunk has no buffers, move to edge of chunk and go up a level if this is the edge of the block
    traverseChunk(trace.Depth, &trace);
    if (leaving(trace)) {
      trace.Depth = 0;
      saveBackground(buffers, trace);
    } else {
      trace.Depth = comparePositions(trace.Depth, previousLocation, trace);
      saveBaseTrace(buffers, trace.ID);
    }
  } else {
    if (trace.Depth < buffers.BaseDepth)
      // Still traversing base chunks
      trace.Depth++;
    else
      // Traversing blocks
      trace.Depth = buffers.BaseDepth + 2;
    saveNextStep(buffers, &trace);
  }

  updateTrace(buffers, trace);
}

kernel void runBlockTrace(global Block *Blocks, global Usage *Usages, global uint *ChildRequestID, global ChildRequest *ChildRequests,
                          global float3 *Origins, global float3 *Directions, global float *FoVs, global ulong3 *Locations, global uchar *Weightings,
                          global uchar *Depths, global uint *BlockTraces, global uint *BlockTraceQueue, global uint *BlockTraceQueueID,
                          global uint *BaseTraceQueue, global uint *BaseTraceQueueID, global uint *BackgroundQueue, global uint *BackgroundQueueID,
                          global uint *MaterialQueue, global uint *MaterialQueueID, global uint *ParentTraces, TraceInput input) {
  Buffers buffers = {.Blocks = Blocks,
                     .Usages = Usages,
                     .ChildRequestID = ChildRequestID,
                     .ChildRequests = ChildRequests,
                     .Origins = Origins,
                     .Directions = Directions,
                     .FoVs = FoVs,
                     .Locations = Locations,
                     .Weightings = Weightings,
                     .Depths = Depths,
                     .BlockTraces = BlockTraces,
                     .BlockTraceQueue = BlockTraceQueue,
                     .BlockTraceQueueID = BlockTraceQueueID,
                     .BaseTraceQueue = BaseTraceQueue,
                     .BaseTraceQueueID = BaseTraceQueueID,
                     .BackgroundQueue = BackgroundQueue,
                     .BackgroundQueueID = BackgroundQueueID,
                     .MaterialQueue = MaterialQueue,
                     .MaterialQueueID = MaterialQueueID,
                     .ParentTraces = ParentTraces,
                     .BaseDepth = input.BaseDepth};
  TraceData trace = resolveTrace(buffers, (float2)(input.DoF[0], input.DoF[1]), BlockTraces[get_global_id(0)]);
  uint localAddress = getBlockAddress(buffers, trace.Depth, trace.Location);
  if (localAddress == UINT_MAX)
    return; // dud address needs handling!!
  Block block = getBlock(buffers, localAddress);

  updateUsage(buffers, localAddress >> 3, input.Tick);

  bool complete = false;
  ulong3 previousLocation = trace.Location;

  // Check if current block chunk contains buffers
  if (((block.Chunk >> (chunkPosition(trace.Depth, trace.Location) * 2)) & 2) != 2) {
    // current block chunk has no buffers, move to edge of chunk and go up a level if this is the edge
    traverseChunk(trace.Depth, &trace);
    if (leaving(trace)) {
      trace.Depth = 0;
      complete = true;
    } else
      trace.Depth = comparePositions(trace.Depth, previousLocation, trace);

  } else {
    // C value is too diffuse to use
    if (trace.ConeDepth < (buffers.BaseDepth + 2)) {
      trace.Depth = buffers.BaseDepth + 2;
      complete = true;
    }

    // ConeDepth value requires me to go up a level
    else if (trace.ConeDepth < trace.Depth)
      trace.Depth--;

    // no child found, resolve colour of this voxel
    else if (block.Child == UINT_MAX) {
      requestChild(buffers, localAddress, trace.Depth, trace.Tick, 1, trace.Location);
      complete = true;
    }

    // cone trace.Depth not met, navigate to child
    else if (trace.ConeDepth > (trace.Depth + 1))
      trace.Depth++;

    else
      complete = true;
  }

  if (!complete)
    saveNextStep(buffers, &trace);
  else if (trace.Depth == 0)
    saveBackground(buffers, trace);
  else
    saveMaterial(buffers, trace);

  updateTrace(buffers, trace);
}

kernel void evaluateMaterial(global Block *Blocks, global Usage *Usages, global float3 *Origins, global float3 *Directions, global float *FoVs,
                             global ulong3 *Locations, global uchar *Depths, global uchar *Weightings, global uchar *ColourRs, global uchar *ColourGs,
                             global uchar *ColourBs, global float *RayLengths, global uchar *Luminosities, global uint *MaterialQueue,
                             global uint *ParentTraces, global float3 *RootDirections, global ulong3 *RootLocations, global uchar *RootDepths,
                             global uchar *RootWeightings, global uint *RootParentTraces, global uint *BaseTraceQueue, global uint *BaseTraceQueueID,
                             global uint *FinalWeightings, global uint *AccumulatorID, TraceInput input) {
  if (input.FovConstant <= 0 || input.FovMultiplier >= 1 || input.FovMultiplier <= -1)
    return;
  Buffers buffers = {.Blocks = Blocks,
                     .Usages = Usages,
                     .Origins = Origins,
                     .Directions = Directions,
                     .FoVs = FoVs,
                     .Locations = Locations,
                     .Depths = Depths,
                     .Weightings = Weightings,
                     .ColourRs = ColourRs,
                     .ColourGs = ColourGs,
                     .ColourBs = ColourBs,
                     .RayLengths = RayLengths,
                     .Luminosities = Luminosities,
                     .MaterialQueue = MaterialQueue,
                     .ParentTraces = ParentTraces,
                     .BaseTraceQueue = BaseTraceQueue,
                     .BaseTraceQueueID = BaseTraceQueueID,
                     .FinalWeightings = FinalWeightings,
                     .AccumulatorID = AccumulatorID,
                     .BaseDepth = input.BaseDepth};

  uint traceID = buffers.MaterialQueue[get_global_id(0)];
  uint parentID = RootParentTraces[traceID];
  float3 direction = RootDirections[traceID];
  ulong3 location = RootLocations[traceID];
  uchar depth = RootDepths[traceID];
  uchar baseWeighting = RootWeightings[traceID];
  uint address = getBlockAddress(buffers, depth, location);
  if (address == UINT_MAX)
    return;
  Block block = getBlock(buffers, address);
  updateUsage(buffers, address >> 3, input.Tick);
  // What happens on the first pass when the colour isn't set?
  // also the average of yellow and white isn't this...

  uchar colourR = block.ColourR;
  uchar colourG = block.ColourG;
  uchar colourB = block.ColourB;

  float3 normal = normalVector(block.NormalPitch, block.NormalYaw);
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
  if (weighting * baseWeighting < 6) {
    FinalWeightings[traceID] = baseWeighting;
    return;
  }

  float3 spineAxis = normalize(cross(direction, normal));

  float3 reflection = direction - (2 * dot(direction, normal) * normal);
  float dotProduct = dot(reflection, normal);
  if (dotProduct < fov)
    ringWeighting = weighting * dotProduct / fov;

  saveAccumulator(buffers, setupTrace(location, normal, reflection, depth, fov, (uchar)(ringWeighting * baseWeighting), input), colourR, colourG,
                  colourB, parentID);

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

    dotProduct = dot(ringSeed, normal);
    if (dotProduct < 0)
      break;
    // fov dives into surface
    if (dotProduct < fov)
      ringWeighting = weighting * dotProduct / fov;

    saveAccumulator(buffers, setupTrace(location, normal, ringSeed, depth, fov, (uchar)(ringWeighting * baseWeighting), input), colourR, colourG,
                    colourB, parentID);

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

      dotProduct = dot(ringVector, normal);
      if (dotProduct < 0)
        break;
      if (dotProduct < fov)
        ringWeighting = weighting * dotProduct / fov;

      saveAccumulator(buffers, setupTrace(location, normal, ringVector, depth, fov, (uchar)(ringWeighting * baseWeighting), input), colourR, colourG,
                      colourB, parentID);

      ringVector.x = (ringRotation[3] + ringRotation[0]) * ringSeed.x + (ringRotation[4] + ringRotation[11]) * ringSeed.y +
                     (ringRotation[5] - ringRotation[10]) * ringSeed.z;
      ringVector.y = (ringRotation[4] - ringRotation[11]) * ringSeed.x + (ringRotation[6] + ringRotation[0]) * ringSeed.y +
                     (ringRotation[7] + ringRotation[9]) * ringSeed.z;
      ringVector.z = (ringRotation[5] + ringRotation[10]) * ringSeed.x + (ringRotation[7] - ringRotation[9]) * ringSeed.y +
                     (ringRotation[8] + ringRotation[0]) * ringSeed.z;
      ringVector = normalize(ringVector);

      saveAccumulator(buffers, setupTrace(location, normal, ringVector, depth, fov, (uchar)(ringWeighting * baseWeighting), input), colourR, colourG,
                      colourB, parentID);
    }
  }
}

kernel void evaluateBackground(global uint *BackgroundQueue, global float3 *Directions, global uint *ParentTraces, global uchar *ColourRs,
                               global uchar *ColourGs, global uchar *ColourBs, global uchar *Weightings, global uint *FinalColourRs,
                               global uint *FinalColourGs, global uint *FinalColourBs, global uint *FinalWeightings, TraceInput input) {
  Buffers buffers = {.Weightings = Weightings,
                     .ParentTraces = ParentTraces,
                     .ColourRs = ColourRs,
                     .ColourGs = ColourGs,
                     .ColourBs = ColourBs,
                     .AmbientLightLevel = input.AmbientLightLevel};
  uint traceID = BackgroundQueue[get_global_id(0)];
  uint parentID = ParentTraces[traceID];
  float dotprod = dot(Directions[traceID], (float3)(0, 0, -1));
  uchar colourR;
  uchar colourG;
  uchar colourB;
  uchar luminosity;

  if (dotprod > 0.8) {
    colourR = 255;
    colourG = 255;
    colourB = 255;
    luminosity = (uchar)(dotprod * 200.0f);
  } else {
    colourR = 135;
    colourG = 206;
    colourB = 235;
    luminosity = 100;
  }

  uchar weighting = Weightings[traceID];
  if (weighting < 255) {
    uint colourBoundary = 0;
    if (luminosity > input.AmbientLightLevel)
      colourBoundary = 255;
    // lightQuotient is the fraction that you'd increase colour by under white light, 0 = ambient light level
    float lightQuotient = native_divide((float)abs_diff(luminosity, buffers.AmbientLightLevel), (luminosity + buffers.AmbientLightLevel) * 255.0f);

    uchar baseColourR = buffers.ColourRs[traceID];
    uchar baseColourG = buffers.ColourGs[traceID];
    uchar baseColourB = buffers.ColourBs[traceID];

    colourR = (baseColourR + lightQuotient * colourR * (colourBoundary - baseColourR));
    colourG = (baseColourG + lightQuotient * colourG * (colourBoundary - baseColourG));
    colourB = (baseColourB + lightQuotient * colourB * (colourBoundary - baseColourB));
    // colourR = 150;
    // colourG = 100;
    // colourB = 250;
  }
  atomic_add(&FinalWeightings[parentID], 255);

  if (luminosity > 0) {
    atomic_add(&FinalColourRs[parentID], colourR * weighting);
    atomic_add(&FinalColourGs[parentID], colourG * weighting);
    atomic_add(&FinalColourBs[parentID], colourB * weighting);
  }
}

kernel void resolveRemainders(global uint *MaterialQueue, global uint *FinalWeightings, global uchar *Weightings, global uint *ParentTraces,
                              TraceInput input) {
  uint traceID = MaterialQueue[get_global_id(0)];
  uint parentID = ParentTraces[traceID];
  uchar weighting = Weightings[traceID];

  atomic_add(&FinalWeightings[parentID], (uint)weighting);
}

kernel void drawTrace(global uint *FinalColourRs, global uint *FinalColourGs, global uint *FinalColourBs, global uint *FinalWeightings,
                      __write_only image2d_t outputImage, TraceInput input) {
  uint traceID = get_global_id(0) + get_global_id(1) * input.ScreenSize.x;
  int2 coord = (int2)((int)get_global_id(0), (int)get_global_id(1));
  uint weighting = FinalWeightings[traceID];
  if (weighting == 0) {
    write_imagef(outputImage, coord, (float4)(0.75f, 0, 0, 1));
  } else {
    float finalWeighting = (float)weighting * 255.0f;
    // Drawn colour is final colour / final weighting * 255
    write_imagef(outputImage, coord,
                 (float4)(fmin(native_divide((float)FinalColourRs[traceID], finalWeighting), 1),
                          fmin(native_divide((float)FinalColourGs[traceID], finalWeighting), 1),
                          fmin(native_divide((float)FinalColourBs[traceID], finalWeighting), 1), 1));
  }
}
