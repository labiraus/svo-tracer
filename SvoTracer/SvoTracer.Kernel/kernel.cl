typedef struct {
  short NormalPitch;
  short NormalYaw;
  uchar ColourR;
  uchar ColourB;
  uchar ColourG;
  uchar Opacity;
  ushort Properties;
} BlockData;

typedef struct {
  uint Child;
  ushort Chunk;
  BlockData Data;
} Block;

typedef struct {
  uchar ColourR;
  uchar ColourB;
  uchar ColourG;
  uchar Opacity;
  uchar Specularity;
  uchar Gloss;
  uchar Dielectricity;
  uchar Refractivity;
} Colour;

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
} TraceInputData;

typedef struct {
  uchar BaseDepth;
  ushort Tick;
  uint MaxChildRequestId;
  uint MemorySize;
  uint Offset;
  uint GraftSize;
} UpdateInputData;

typedef struct {
  // Current chunk
  ulong3 Location;
  // Position
  float3 Origin;
  // Vector direction
  float3 Direction;
  // Inverse vector direction
  float3 InvDirection;
  // Depth of field made up of focal depth(the angle of the forced depth) and
  // focal point(how deep the minimum is)
  float2 DoF;
  int2 Coord;
  float PixelFoV;
  // Max Opacity
  uchar MaxOpacity;
  float ColourR;
  float ColourB;
  float ColourG;
  float Opacity;
  // Depth of inviolate memory(Specific to voxels)
  uchar BaseDepth;
  // Signs of the vector direction
  bool DirectionSignX;
  bool DirectionSignY;
  bool DirectionSignZ;
  ushort Tick;
  uint MaxChildRequestId;
  float ConeDepth;
} WorkingData;

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

void getSemaphor(global int *semaphor) {
  int occupied = atomic_xchg(semaphor, 1);
  while (occupied > 0) {
    occupied = atomic_xchg(semaphor, 1);
  }
}

void releaseSemaphor(global int *semaphor) { int prevVal = atomic_xchg(semaphor, 0); }

ulong roundUlong(ulong value, uchar depth, bool roundUp) {
  if (roundUp)
    return ((value & (ULONG_MAX - (ULONG_MAX >> (depth + 1)))) + ((ULONG_MAX - (ULONG_MAX >> 1)) >> depth)) - value;

  ulong output = value - ((value & (ULONG_MAX - (ULONG_MAX >> (depth + 1)))) - 1);

  if (output >= value)
    return value;

  return output;
}

uint powSum(uchar depth) {
  uint output = 0;
  for (int i = 1; i <= depth; i++)
    output += (1 << (3 * i));
  return output;
}

float3 normalVector(short pitch, short yaw) {
  // yaw * 2pi/short max
  float fYaw = yaw * M_PI_F / 16383.75f;
  float fPitch = pitch * M_PI_F / 32767.5f;
  float sinYaw = native_sin(fYaw);
  float cosYaw = native_cos(fYaw);
  float sinPitch = native_sin(fPitch);
  float cosPitch = native_cos(fPitch);

  return (float3)(cosYaw * cosPitch, sinYaw * cosPitch, sinPitch);
}

uchar chunkPosition(uchar depth, ulong3 location) {
  return ((location.x >> (64 - depth - 1) & 1) + ((location.y >> (64 - depth - 1) & 1) << 1) + ((location.z >> (64 - depth - 1) & 1) << 2));
}

// Deduces array offset of a base's location given its depth
// Bases are stored as a dense octree down to depth BaseDepth
uint baseLocation(uchar depth, ulong3 location) {
  uint output = 0;
  for (uchar i = 0; i < depth; i++)
    output = (output << 3) + chunkPosition(i, location);

  return output;
}

// Calculates the cone size at a given depth from FoV and pixel diameter data->
// Largest cone size wins
float coneSize(float m, WorkingData *_data) {
  float fov = fabs(_data->DoF.x * (_data->DoF.y - m));
  float eye = _data->PixelFoV * m;
  if (eye < fov)
    return fov;
  else
    return eye;
}

// Determine the maximum tree depth for a cone at this location
void setConeDepth(WorkingData *_data) {
  float cone =
      coneSize(fabs(fast_length((float3)(_data->Origin.x - ulongToFloat(_data->Location.x), _data->Origin.y - ulongToFloat(_data->Location.y),
                                         _data->Origin.z - ulongToFloat(_data->Location.z)))),
               _data);
  _data->ConeDepth = -half_log2(cone);
}

BlockData background(WorkingData *_data) {
  BlockData output;
  output.ColourR = 0;
  output.ColourB = 0;
  output.ColourG = 0;
  output.Opacity = 255;
  return output;
}

void writeData(__write_only image2d_t outputImage, WorkingData *_data) {
  write_imagef(outputImage, _data->Coord, (float4)(_data->ColourR, _data->ColourG, _data->ColourB, 1));
}

// Combine _data colour+opacity with background colour
bool saveVoxelTrace(BlockData blockData, WorkingData *_data) {
  if (_data->Opacity < _data->MaxOpacity) {

    float3 normal = normalVector(blockData.NormalPitch, blockData.NormalYaw);
    float3 reflection = _data->Direction - (2 * dot(_data->Direction, normal) * normal);
    float shade = (dot(reflection, (float3)(1, 0, 0)) + 1) / 2.0f;
    _data->ColourR = shade;
    _data->ColourB = shade;
    _data->ColourG = shade;
    // reflection += (float3)(1, 1, 1);
    // reflection *= 255;
    // _data->ColourR = native_divide(blockData.ColourR + reflection.x, 510.0f);
    // _data->ColourB = native_divide(blockData.ColourB + reflection.y, 510.0f);
    // _data->ColourG = native_divide(blockData.ColourG + reflection.z, 510.0f);
    _data->Opacity = _data->Opacity + blockData.Opacity;
  }
  return true;
}

// Combine _data colour+opacity with background colour and write to output
void writeBackgroundData(__write_only image2d_t outputImage, WorkingData *_data) {
  // saveVoxelTrace(background(_data), _data);
  writeData(outputImage, _data);
}

BlockData average(uint address, global Block *blocks, WorkingData *_data) {
  // Average like heck
  return blocks[address].Data;
}

void requestChild(uint address, uchar depth, global uint *childRequestId, global ChildRequest *childRequests, uint maxChildRequestId, ushort tick,
                  uchar treeSize, ulong3 location) {
  uint currentId = atomic_inc(childRequestId);
  if (currentId >= maxChildRequestId)
    return;
  ChildRequest request;
  request.Address = address;
  request.Tick = tick;
  request.Depth = depth;
  request.Location[0] = location.x;
  request.Location[1] = location.y;
  request.Location[2] = location.z;
  request.TreeSize = treeSize;
  childRequests[currentId] = request;
}

// Determines whether cone is leaving the octree
bool leaving(WorkingData *_data) {
  return (!_data->DirectionSignX && _data->Location.x == 0) || (!_data->DirectionSignY && _data->Location.y == 0) ||
         (!_data->DirectionSignZ && _data->Location.z == 0) || (_data->DirectionSignX && _data->Location.x == ULONG_MAX) ||
         (_data->DirectionSignY && _data->Location.y == ULONG_MAX) || (_data->DirectionSignZ && _data->Location.z == ULONG_MAX);
}

// Moves to the nearest neighboring chunk along the Direction vector
void traverseChunk(uchar depth, WorkingData *_data) {
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

uchar comparePositions(uchar depth, ulong3 previousLocation, WorkingData *_data) {
  uchar newPosition = chunkPosition(depth, _data->Location);
  uchar previousPosition = chunkPosition(depth, previousLocation);
  while (((previousPosition & 1) == _data->DirectionSignX && (newPosition & 1) != _data->DirectionSignX) ||
         (((previousPosition >> 1) & 1) == _data->DirectionSignY && ((newPosition >> 1) & 1) != _data->DirectionSignY) ||
         (((previousPosition >> 2) & 1) == _data->DirectionSignZ && ((newPosition >> 2) & 1) != _data->DirectionSignZ)) {
    if (depth == 1)
      break;
    depth--;
    newPosition = chunkPosition(depth, _data->Location);
    previousPosition = chunkPosition(depth, previousLocation);
  }
  return depth;
}

// Sets chunk and determines whether the ray hits the octree
bool startTrace(WorkingData *_data) {
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
  float location0 = _data->Origin.x;
  float location1 = _data->Origin.y;
  float location2 = _data->Origin.z;
  float m = 0;
  float mx = 0;
  float my = 0;
  float mz = 0;
  int xyz = (x0 ? 0b100000 : 0) + (x1 ? 0b010000 : 0) + (y0 ? 0b001000 : 0) + (y1 ? 0b000100 : 0) + (z0 ? 0b000010 : 0) + (z1 ? 0b000001 : 0);
  if (xyz == 0) {
    _data->Location.x = floatToUlong(location0);
    _data->Location.y = floatToUlong(location1);
    _data->Location.z = floatToUlong(location2);
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
    location0 = 0;
    location1 = _data->Origin.y + (_data->Direction.y * m);
    location2 = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 0b010000: // x1
    m = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    location0 = 1;
    location1 = _data->Origin.y + (_data->Direction.y * m);
    location2 = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 0b001000: // y0
    m = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    location0 = _data->Origin.x + (_data->Direction.x * m);
    location1 = 0;
    location2 = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 0b000100: // y1
    m = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    location0 = _data->Origin.x + (_data->Direction.x * m);
    location1 = 1;
    location2 = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 0b000010: // z0
    m = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    location0 = _data->Origin.x + (_data->Direction.x * m);
    location1 = _data->Origin.y + (_data->Direction.y * m);
    location2 = 0;
    break;
  case 0b000001: // z1
    m = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    location0 = _data->Origin.x + (_data->Direction.x * m);
    location1 = _data->Origin.y + (_data->Direction.y * m);
    location2 = 1;
    break;
  // The 8 side arcs outside of the box between two of the faces on one axis and
  // near to two faces on the other two axies z face
  case 0b101000: // x0y0
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    if (mx >= my) {
      m = mx;
      location0 = 0;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 0;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    }
    break;
  case 0b011000: // x1y0
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    if (mx >= my) {
      m = mx;
      location0 = 1;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 0;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    }
    break;
  case 0b100100: // x0y1
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    if (mx >= my) {
      m = mx;
      location0 = 0;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 1;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    }
    break;
  case 0b010100: // x1y1
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    if (mx >= my) {
      m = mx;
      location0 = 1;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 1;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    }
    break;
  // y face
  case 0b100010: // x0z0
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      location0 = 0;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 0;
    }
    break;
  case 0b010010: // x1z0
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      location0 = 1;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 0;
    }
    break;
  case 0b100001: // x0z1
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      location0 = 0;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  case 0b010001: // x1z1
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= mz) {
      m = mx;
      location0 = 1;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  // x face
  case 0b001010: // y0z0
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 0;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 0;
    }
    break;
  case 0b000110: // y1z0
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 1;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 0;
    }
    break;
  case 0b001001: // y0z1
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 0;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  case 0b000101: // y1z1
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 1;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  // The 8 corners
  case 0b101010: // x0y0z0
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      location0 = 0;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 0;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 0;
    }
    break;
  case 0b011010: // x1y0z0
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      location0 = 1;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 0;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 0;
    }
    break;
  case 0b100110: // x0y1z0
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      location0 = 0;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 1;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 0;
    }
    break;
  case 0b010110: // x1y1z0
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      location0 = 1;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 1;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 0;
    }
    break;
  case 0b101001: // x0y0z1
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      location0 = 0;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 0;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  case 0b011001: // x1y0z1
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      location0 = 1;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 0;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  case 0b100101: // x0y1z1
    mx = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      location0 = 0;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 1;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  case 0b010101: // x1y1z1
    mx = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    my = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    mz = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    if (mx >= my & mx >= mz) {
      m = mx;
      location0 = 1;
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else if (my >= mx & my >= mz) {
      m = my;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = 1;
      location2 = _data->Origin.z + (_data->Direction.z * m);
    } else {
      m = mz;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  default:
    return false;
  }
  _data->Location.x = floatToUlong(location0);
  _data->Location.y = floatToUlong(location1);
  _data->Location.z = floatToUlong(location2);
  float c = coneSize(m, _data);
  return !(location0 < -c || location0 > 1 + c || location1 < -c || location1 > 1 + c || location2 < -c || location2 > 1 + c);
}

WorkingData setup(int2 coord, TraceInputData _input) {
  WorkingData data;
  data.Coord = coord;
  // rotation around the z axis
  float u = _input.FoV[0] * native_divide((native_divide((float)_input.ScreenSize.x, 2) - (float)coord.x), (float)_input.ScreenSize.x);
  // rotation around the y axis
  float v = _input.FoV[1] * native_divide((native_divide((float)_input.ScreenSize.y, 2) - (float)coord.y), (float)_input.ScreenSize.y);
  float sinU = native_sin(u);
  float cosU = native_cos(u);
  float sinV = native_sin(v);
  float cosV = native_cos(v);
  // _input.Facing * RotationMatrixZ(u) * RotationMatrixY(v) * unitVectorX

  // // rot y
  // float3 matY0;
  // float3 matY1;
  // float3 matY2;
  // matY0.x = cosV;
  // matY0.y = 0;
  // matY0.z = 0 - sinV;
  // matY1.x = 0;
  // matY1.y = 1;
  // matY1.z = 0;
  // matY2.x = sinV;
  // matY2.y = 0;
  // matY2.z = cosV;

  // // rot x
  // float3 matZ0;
  // float3 matZ1;
  // float3 matZ2;
  // matZ0.x = cosU;
  // matZ0.y = sinU;
  // matZ0.z = 0;
  // matZ1.x = 0 - sinU;
  // matZ1.y = cosU;
  // matZ1.z = 0;
  // matZ2.x = 0;
  // matZ2.y = 0;
  // matZ2.z = 1;

  // float3 matRot0;
  // float3 matRot1;
  // float3 matRot2;
  // matRot0.x = matZ0.x * matY0.x + matZ0.y * matY1.x + matZ0.z * matY2.x;
  // matRot0.y = matZ0.x * matY0.y + matZ0.y * matY1.y + matZ0.z * matY2.y;
  // matRot0.z = matZ0.x * matY0.z + matZ0.y * matY1.z + matZ0.z * matY2.z;
  // matRot1.x = matZ1.x * matY0.x + matZ1.y * matY1.x + matZ1.z * matY2.x;
  // matRot1.y = matZ1.x * matY0.y + matZ1.y * matY1.y + matZ1.z * matY2.y;
  // matRot1.z = matZ1.x * matY0.z + matZ1.y * matY1.z + matZ1.z * matY2.z;
  // matRot2.x = matZ2.x * matY0.x + matZ2.y * matY1.x + matZ2.z * matY2.x;
  // matRot2.y = matZ2.x * matY0.y + matZ2.y * matY1.y + matZ2.z * matY2.y;
  // matRot2.z = matZ2.x * matY0.z + matZ2.y * matY1.z + matZ2.z * matY2.z;

  // float3 matFinal0;
  // float3 matFinal1;
  // float3 matFinal2;
  // matFinal0.x = _input.Facing[0] * matRot0.x + _input.Facing[3] * matRot1.x + _input.Facing[6] * matRot2.x;
  // matFinal0.y = _input.Facing[0] * matRot0.y + _input.Facing[3] * matRot1.y + _input.Facing[6] * matRot2.y;
  // matFinal0.z = _input.Facing[0] * matRot0.z + _input.Facing[3] * matRot1.z + _input.Facing[6] * matRot2.z;
  // matFinal1.x = _input.Facing[1] * matRot0.x + _input.Facing[4] * matRot1.x + _input.Facing[7] * matRot2.x;
  // matFinal1.y = _input.Facing[1] * matRot0.y + _input.Facing[4] * matRot1.y + _input.Facing[7] * matRot2.y;
  // matFinal1.z = _input.Facing[1] * matRot0.z + _input.Facing[4] * matRot1.z + _input.Facing[7] * matRot2.z;
  // matFinal2.x = _input.Facing[2] * matRot0.x + _input.Facing[5] * matRot1.x + _input.Facing[8] * matRot2.x;
  // matFinal2.y = _input.Facing[2] * matRot0.y + _input.Facing[5] * matRot1.y + _input.Facing[8] * matRot2.y;
  // matFinal2.z = _input.Facing[2] * matRot0.z + _input.Facing[5] * matRot1.z + _input.Facing[8] * matRot2.z;

  // float x = 1;
  // float y = 0;
  // float z = 0;

  // float3 dir = (float3)(matFinal0.x * x + matFinal0.y * y + matFinal0.z * z, matFinal1.x * x + matFinal1.y * y + matFinal1.z * z,
  //                       matFinal2.x * x + matFinal2.y * y + matFinal2.z * z);

  float matRot0x = cosU * cosV;
  float matRot1x = (0 - sinU) * cosV;
  float matRot2x = sinV;
  data.Direction = (float3)(_input.Facing[0] * matRot0x + _input.Facing[3] * matRot1x + _input.Facing[6] * matRot2x,
                            _input.Facing[1] * matRot0x + _input.Facing[4] * matRot1x + _input.Facing[7] * matRot2x,
                            _input.Facing[2] * matRot0x + _input.Facing[5] * matRot1x + _input.Facing[8] * matRot2x);
  data.InvDirection = (float3)(native_divide(1, data.Direction.x), native_divide(1, data.Direction.y), native_divide(1, data.Direction.z));
  data.DirectionSignX = data.Direction.x >= 0;
  data.DirectionSignY = data.Direction.y >= 0;
  data.DirectionSignZ = data.Direction.z >= 0;
  data.Origin = (float3)(_input.Origin[0], _input.Origin[1], _input.Origin[2]);
  data.BaseDepth = _input.BaseDepth;
  data.Tick = _input.Tick;
  data.MaxChildRequestId = _input.MaxChildRequestId;
  data.DoF = (float2)(_input.DoF[0], _input.DoF[1]);
  data.MaxOpacity = _input.MaxOpacity;
  data.PixelFoV = native_divide(_input.FoV[0], _input.ScreenSize.x);
  data.Opacity = 0;
  data.ColourR = 0;
  data.ColourB = 0;
  data.ColourG = 0;
  return data;
}

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
      requestChild(address, i, childRequestId, childRequests, inputData.MaxChildRequestId, inputData.Tick, depth - i, location);
      helpDereference(blocks, usage, parentSize, parentResidency, parents, dereferenceQueue, dereferenceRemaining, semaphor, inputData.Tick);
      return UINT_MAX;
    }
    address = blocks[address].Child + chunkPosition(i, location);
  }
  return address;
}

//*******************************KERNELS***********************************

kernel void prune(global ushort *bases, global Block *blocks, global Usage *usage, global uint *childRequestId, global ChildRequest *childRequests,
                  global uint *parentSize, global bool *parentResidency, global Parent *parents, global uint2 *dereferenceQueue,
                  global int *dereferenceRemaining, global int *semaphor, global Pruning *pruning, global BlockData *pruningBlockData,
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
    blocks[address].Data = pruningBlockData[x];
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

kernel void traceVoxel(global ushort *bases, global Block *blocks, global Usage *usage, global uint *childRequestId,
                       global ChildRequest *childRequests, __write_only image2d_t outputImage, TraceInputData _input) {
  uchar depth = 1;
  uint localAddress;
  ulong3 previousLocation;
  uint x = get_global_id(0);
  uint y = get_global_id(1);
  int2 coord = (int2)(x, y);
  WorkingData data = setup(coord, _input);
  WorkingData *_data = &data;

  if (!startTrace(_data)) {
    writeBackgroundData(outputImage, _data);
    return;
  }
  setConeDepth(_data);

  // block location at each layer of depth
  uint depthHeap[64];
  depthHeap[_data->BaseDepth + 1] = UINT_MAX;

  // iterate over base chunks
  while (true) {
    // check base chunks to see if current location contains geometry and traverse if it doesn't
    localAddress = powSum(depth - 1) + baseLocation(depth, _data->Location);
    if (depth <= _data->BaseDepth && (bases[localAddress] >> (chunkPosition(depth, _data->Location) * 2) & 2) != 2) {
      // current chunk has no geometry, move to edge of chunk and go up a level if this is the edge of the block
      previousLocation = _data->Location;
      traverseChunk(depth, _data);
      if (leaving(_data)) {
        writeBackgroundData(outputImage, _data);
        return;
      }
      depth = comparePositions(depth, previousLocation, _data);
    } else {
      if (depth < _data->BaseDepth) {
        // Still traversing base chunks
        depth++;
      } else {
        // Traversing blocks
        depth = _data->BaseDepth + 2;

        // iterate over blocks
        while (depth > (_data->BaseDepth + 1)) {
          if (depth == _data->BaseDepth + 2)
            localAddress = baseLocation(depth, _data->Location);
          else
            localAddress = blocks[depthHeap[depth - 1]].Child + chunkPosition(depth - 1, _data->Location);
          depthHeap[depth] = localAddress;

          // Update usage
          if (usage[localAddress >> 3].Tick < USHRT_MAX - 1)
            usage[localAddress >> 3].Tick = _data->Tick;

          // Check if current block chunk contains geometry
          if (((blocks[localAddress].Chunk >> (chunkPosition(depth, _data->Location) * 2)) & 2) != 2) {
            // current block chunk has no geometry, move to edge of chunk and go up a level if this is the edge
            previousLocation = _data->Location;
            traverseChunk(depth, _data);
            if (leaving(_data)) {
              writeBackgroundData(outputImage, _data);
              return;
            }
            depth = comparePositions(depth, previousLocation, _data);
          } else {
            // C value is too diffuse to use
            if (_data->ConeDepth < (_data->BaseDepth + 2)) {
              depth = _data->BaseDepth + 2;
              if (saveVoxelTrace(blocks[depthHeap[depth]].Data, _data)) {
                writeData(outputImage, _data);
                return;
              }
            }

            // ConeDepth value requires me to go up a level
            else if (_data->ConeDepth < depth) {
              depth--;
            }

            // no child found, resolve colour of this voxel
            else if (blocks[localAddress].Child == UINT_MAX) {
              requestChild(localAddress, depth, childRequestId, childRequests, _data->MaxChildRequestId, _data->Tick, 1, _data->Location);
              if (saveVoxelTrace(blocks[localAddress].Data, _data)) {
                writeData(outputImage, _data);
                return;
              }
            }

            // cone depth not met, navigate to child
            else if (_data->ConeDepth > (depth + 1)) {
              depth++;
            }

            else {
              if (saveVoxelTrace(average(localAddress, blocks, _data), _data)) {
                writeData(outputImage, _data);
                return;
              }
            }
          }
        }
        depth = _data->BaseDepth;
      }
    }
  }
}

kernel void traceMesh() {}

kernel void traceParticle() {}

kernel void traceLight() {}

kernel void spawnRays() {}

kernel void resolveImage() {}