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
  float Facing[3];
  // Horizonal/vertical FoV angle of the screen
  float FoV[2];
  // Depth of field made up of focal depth(the angle of the forced depth) and
  // focal point(how deep the minimum is)
  float DoF[2];
  // Screen size
  uint ScreenSize[2];
  uchar MaxOpacity;
  // Depth of inviolate memory(Specific to voxels)
  uchar N;
  ushort Tick;
  uint MaxChildRequestId;
} TraceInputData;

typedef struct {
  uchar N;
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
  int2 ScreenSize;
  float PixelFoV;
  // Max Opacity
  uchar MaxOpacity;
  float ColourR;
  float ColourB;
  float ColourG;
  float Opacity;
  // Depth of inviolate memory(Specific to voxels)
  uchar N;
  // Signs of the vector direction
  bool DirectionSignX;
  bool DirectionSignY;
  bool DirectionSignZ;
  ushort Tick;
  uint MaxChildRequestId;
} WorkingData;

// Converts a float between 0 and 1 into a ulong coordinate
ulong floatToULong(float x) {
  // float rounding errors mean that this is the closest you can get to 1 at 24
  // bits deep
  if (x >= 0.999999911f)
    return ULONG_MAX;
  else
    return (ulong)(fabs(x) * ULONG_MAX);
}

void GetSemaphor(global int *semaphor) {
  int occupied = atom_xchg(semaphor, 1);
  while (occupied > 0) {
    occupied = atom_xchg(semaphor, 1);
  }
}

void ReleaseSemaphor(global int *semaphor) {
  int prevVal = atom_xchg(semaphor, 0);
}

float uLongToFloat(ulong x) {
  return native_divide((float)x, (float)ULONG_MAX);
}

ulong roundUlong(ulong value, uchar depth, bool roundUp) {
  if (roundUp)
    return ((value & (ULONG_MAX - (ULONG_MAX >> (depth + 1)))) +
            (ULONG_MAX - (ULONG_MAX >> 1) >> depth)) -
           value;

  ulong output = value - ((value & (ULONG_MAX - (ULONG_MAX >> depth + 1))) - 1);

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

uchar chunk(uchar depth, ulong3 location) {
  return ((location.x >> (64 - depth - 1) & 1) +
          ((location.y >> (64 - depth - 1) & 1) << 1) +
          ((location.z >> (64 - depth - 1) & 1) << 2));
}

uint baseLocation(uchar depth, ulong3 location) {
  uint output = 0;
  for (uchar i = 0; i < depth; i++)
    output = (output << 3) + chunk(i, location);

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

float coneLevel(WorkingData *_data) {
  float cone =
      coneSize(fabs(fast_length((
                   float3)(_data->Origin.x - uLongToFloat(_data->Location.x),
                           _data->Origin.y - uLongToFloat(_data->Location.y),
                           _data->Origin.z - uLongToFloat(_data->Location.z)))),
               _data);
  return -half_log2(cone);
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
  write_imagef(outputImage, _data->Coord,
               (float4)(_data->ColourR, _data->ColourB, _data->ColourG, 1));
}

bool saveVoxelTrace(BlockData data, WorkingData *_data) {
  if (_data->Opacity < _data->MaxOpacity) {
    _data->ColourR = native_divide(data.ColourR, 255.0);
    _data->ColourB = native_divide(data.ColourB, 255.0);
    _data->ColourG = native_divide(data.ColourG, 255.0);

    _data->Opacity = _data->Opacity + data.Opacity;
  }
  return true;
}

BlockData average(uint address, global Block *blocks, float C,
                  WorkingData *_data) {
  // Average like heck
  return blocks[address].Data;
}

void requestChild(uint address, uchar depth, global uint *childRequestId,
                  global ChildRequest *childRequests, uint maxChildRequestId,
                  ushort tick, uchar treeSize, ulong3 location) {
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

bool leaving(WorkingData *_data) {
  return (!_data->DirectionSignX && _data->Location.x == 0) ||
         (!_data->DirectionSignY && _data->Location.y == 0) ||
         (!_data->DirectionSignZ && _data->Location.z == 0) ||
         (_data->DirectionSignX && _data->Location.x == ULONG_MAX) ||
         (_data->DirectionSignY && _data->Location.y == ULONG_MAX) ||
         (_data->DirectionSignZ && _data->Location.z == ULONG_MAX);
}

// Moves to the nearest neighboring chunk along the Direction vector
uchar traverseChunk(uchar depth, uchar position, WorkingData *_data) {
  ulong dx = roundUlong(_data->Location.x, depth, _data->DirectionSignX);
  ulong dy = roundUlong(_data->Location.y, depth, _data->DirectionSignY);
  ulong dz = roundUlong(_data->Location.z, depth, _data->DirectionSignZ);

  float ax = fabs(dx * _data->InvDirection.x);
  float ay = fabs(dy * _data->InvDirection.y);
  float az = fabs(dz * _data->InvDirection.z);
  bool success = true;

  if (ax <= ay && ax <= az) {
    float udx = uLongToFloat(dx);
    dy = floatToULong(_data->Direction.y * _data->InvDirection.x * udx);
    dz = floatToULong(_data->Direction.z * _data->InvDirection.x * udx);

    if ((_data->DirectionSignX && (position & 1) == 1) ||
        (!_data->DirectionSignX && (position & 1) == 0))
      success = false;
  } else if (ay <= ax && ay <= az) {
    float udy = uLongToFloat(dy);
    dx = floatToULong(_data->Direction.x * _data->InvDirection.y * udy);
    dz = floatToULong(_data->Direction.z * _data->InvDirection.y * udy);

    if ((_data->DirectionSignY && (position >> 1 & 1) == 1) ||
        (!_data->DirectionSignY && (position >> 1 & 1) == 0))
      success = false;
  } else {
    float udz = uLongToFloat(dz);
    dx = floatToULong(_data->Direction.x * _data->InvDirection.z * udz);
    dy = floatToULong(_data->Direction.y * _data->InvDirection.z * udz);

    if ((_data->DirectionSignZ && (position >> 2 & 1) == 1) ||
        (!_data->DirectionSignZ && (position >> 2 & 1) == 0))
      success = false;
  }

  if (_data->DirectionSignX)
    _data->Location.x = _data->Location.x + dx;
  else
    _data->Location.x = _data->Location.x - dx;

  if (_data->DirectionSignY)
    _data->Location.y = _data->Location.y + dy;
  else
    _data->Location.y = _data->Location.y - dy;

  if (_data->DirectionSignZ)
    _data->Location.z = _data->Location.z + dz;
  else
    _data->Location.z = _data->Location.z - dz;

  if (_data->DirectionSignX && _data->Location.x == 0) {
    _data->Location.x = ULONG_MAX;
    return false;
  } else if (!_data->DirectionSignX && _data->Location.x == ULONG_MAX) {
    _data->Location.x = 0;
    return false;
  } else if (_data->DirectionSignY && _data->Location.y == 0) {
    _data->Location.y = ULONG_MAX;
    return false;
  } else if (!_data->DirectionSignY && _data->Location.y == ULONG_MAX) {
    _data->Location.y = 0;
    return false;
  } else if (_data->DirectionSignZ && _data->Location.z == 0) {
    _data->Location.z = ULONG_MAX;
    return false;
  } else if (!_data->DirectionSignZ && _data->Location.z == ULONG_MAX) {
    _data->Location.z = 0;
    return false;
  }

  return success;
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
  int xyz = (x0 ? 32 : 0) + (x1 ? 16 : 0) + (y0 ? 8 : 0) + (y1 ? 4 : 0) +
            (z0 ? 2 : 0) + (z1 ? 1 : 0);
  if (xyz == 0) {
    _data->Location.x = floatToULong(location0);
    _data->Location.y = floatToULong(location1);
    _data->Location.z = floatToULong(location2);
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
  case 0:
    break;
  // Adjacent to one of the 6 planes
  case 32: // x0
    m = fabs((0 - _data->Origin.x) * _data->InvDirection.x);
    location0 = 0;
    location1 = _data->Origin.y + (_data->Direction.y * m);
    location2 = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 16: // x1
    m = fabs((1 - _data->Origin.x) * _data->InvDirection.x);
    location0 = 1;
    location1 = _data->Origin.y + (_data->Direction.y * m);
    location2 = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 8: // y0
    m = fabs((0 - _data->Origin.y) * _data->InvDirection.y);
    location0 = _data->Origin.x + (_data->Direction.x * m);
    location1 = 0;
    location2 = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 4: // y1
    m = fabs((1 - _data->Origin.y) * _data->InvDirection.y);
    location0 = _data->Origin.x + (_data->Direction.x * m);
    location1 = 1;
    location2 = _data->Origin.z + (_data->Direction.z * m);
    break;
  case 2: // z0
    m = fabs((0 - _data->Origin.z) * _data->InvDirection.z);
    location0 = _data->Origin.x + (_data->Direction.x * m);
    location1 = _data->Origin.y + (_data->Direction.y * m);
    location2 = 0;
    break;
  case 1: // z1
    m = fabs((1 - _data->Origin.z) * _data->InvDirection.z);
    location0 = _data->Origin.x + (_data->Direction.x * m);
    location1 = _data->Origin.y + (_data->Direction.y * m);
    location2 = 1;
    break;
  // The 8 side arcs outside of the box between two of the faces on one axis and
  // near to two faces on the other two axies z face
  case 40: // x0y0
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
  case 24: // x1y0
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
  case 36: // x0y1
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
  case 20: // x1y1
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
  case 34: // x0z0
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
  case 18: // x1z0
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
  case 33: // x0z1
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
  case 17: // x1z1
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
  case 10: // y0z0
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
  case 6: // y1z0
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
  case 9: // y0z1
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
  case 5: // y1z1
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
  case 42: // x0y0z0
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
  case 26: // x1y0z0
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
  case 38: // x0y1z0
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
  case 22: // x1y1z0
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
  case 41: // x0y0z1
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
  case 25: // x1y0z1
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
  case 37: // x0y1z1
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
      m = mx;
      location0 = _data->Origin.x + (_data->Direction.x * m);
      location1 = _data->Origin.y + (_data->Direction.y * m);
      location2 = 1;
    }
    break;
  case 21: // x1y1z1
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
  _data->Location.x = floatToULong(location0);
  _data->Location.y = floatToULong(location1);
  _data->Location.z = floatToULong(location2);
  float c = coneSize(m, _data);
  return !(location0 < -c || location0 > 1 + c || location1 < -c ||
           location1 > 1 + c || location2 < -c || location2 > 1 + c);
}

WorkingData *setup(int2 coord, TraceInputData _input) {
  WorkingData data;
  data.Coord = coord;
  data.ScreenSize = (int2)(_input.ScreenSize[0], _input.ScreenSize[1]);
  // Horizontal and vertical offset angles float h and v
  float h = _input.FoV[0] *
            native_divide((native_divide((float)_input.ScreenSize[0], 2) -
                           (float)coord.x),
                          (float)_input.ScreenSize[0]);
  float v = _input.FoV[1] *
            native_divide((native_divide((float)_input.ScreenSize[1], 2) -
                           (float)coord.y),
                          (float)_input.ScreenSize[1]);

  float su = native_sin(_input.Facing[2]);
  float cu = native_cos(_input.Facing[2]);
  float sv = native_sin(_input.Facing[1]);
  float cv = native_cos(_input.Facing[1]);
  float sw = native_sin(_input.Facing[0]);
  float cw = native_cos(_input.Facing[0]);
  // float su2 = 0;
  // float cu2 = 1;
  float sv2 = native_sin(v);
  float cv2 = native_cos(v);
  float sw2 = native_sin(h);
  float cw2 = native_cos(h);

  float AM11 = cv * cw;
  float AM12 = su * sv * cw - cu * sw;
  float AM13 = su * sw + cu * sv * cw;
  float AM21 = cv * sw;
  float AM22 = cu * cw + su * sv * sw;
  float AM23 = cu * sv * sw - su * cw;
  float AM31 = -sv;
  float AM32 = su * cv;
  float AM33 = cu * cv;

  float BM11 = cv2 * cw2;
  // float BM12 = su2 * sv2 * cw2 - cu2 * sw2;
  // float BM13 = su2 * sw2 + cu2 * sv2 * cw2;
  float BM21 = cv2 * sw2;
  // float BM22 = cu2 * cw2 + su2 * sv2 * sw2;
  // float BM23 = cu2 * sv2 * sw2 - su2 * cw2;
  float BM31 = -sv2;
  // float BM32 = su2 * cv2;
  // float BM33 = cu2 * cv2;

  float CM11 = AM11 * BM11 + AM12 * BM21 + AM13 * BM31;
  // float CM12 = AM11 * BM12 + AM12 * BM22 + AM13 * BM32;
  // float CM13 = AM11 * BM13 + AM12 * BM23 + AM13 * BM33;
  float CM21 = AM21 * BM11 + AM22 * BM21 + AM23 * BM31;
  // float CM22 = AM21 * BM12 + AM22 * BM22 + AM23 * BM32;
  // float CM23 = AM21 * BM13 + AM22 * BM23 + AM23 * BM33;
  float CM31 = AM31 * BM11 + AM32 * BM21 + AM33 * BM31;
  // float CM32 = AM31 * BM12 + AM32 * BM22 + AM33 * BM32;
  // float CM33 = AM31 * BM13 + AM32 * BM23 + AM33 * BM33;

  float yaw = atan2(CM21, CM11);
  float pitch = -asin(CM31);

  // Unit vector of direction float3 DIR
  float3 dir = (float3)(native_cos(yaw) * native_cos(pitch),
                        native_sin(yaw) * native_cos(pitch), native_sin(pitch));
  data.Direction = dir;
  float dirLength = length(data.Direction);
  data.Direction.x = native_divide(data.Direction.x, dirLength);
  data.Direction.y = native_divide(data.Direction.y, dirLength);
  data.Direction.z = native_divide(data.Direction.z, dirLength);
  data.InvDirection = (float3)(native_divide(1, data.Direction.x),
                               native_divide(1, data.Direction.y),
                               native_divide(1, data.Direction.z));
  data.DirectionSignX = data.Direction.x >= 0;
  data.DirectionSignY = data.Direction.y >= 0;
  data.DirectionSignZ = data.Direction.z >= 0;
  data.Origin = (float3)(_input.Origin[0], _input.Origin[1], _input.Origin[2]);
  data.N = _input.N;
  data.Tick = _input.Tick;
  data.MaxChildRequestId = _input.MaxChildRequestId;
  data.DoF = (float2)(_input.DoF[0], _input.DoF[1]);
  data.MaxOpacity = _input.MaxOpacity;
  data.PixelFoV = native_divide(_input.FoV[0], _input.ScreenSize[0]);
  data.Opacity = 0;
  data.ColourR = 0;
  data.ColourB = 0;
  data.ColourG = 0;
  return &data;
}

void helpDereference(global Block *blocks, global Usage *usage,
                     global uint *parentSize, global bool *parentResidency,
                     global Parent *parents, global uint2 *dereferenceQueue,
                     global int *dereferenceRemaining, global int *semaphor,
                     ushort tick) {
  // All local threads get to play the dereferencing game
  GetSemaphor(semaphor);
  int localRemaining = atomic_dec(dereferenceRemaining);
  uint2 address2;
  while (localRemaining >= 0) {
    address2 = dereferenceQueue[localRemaining];
    ReleaseSemaphor(semaphor);
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
            nextElement =
                atomic_xchg(&parents[parent].NextElement, UINT_MAX - 1);
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
            atomic_xchg(&parents[parent].ParentAddress,
                        parents[nextElement].ParentAddress);
            atomic_xchg(&parents[parent].NextElement,
                        parents[nextElement].NextElement);
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
        atomic_xchg(&usage[address2.y >> 3].Parent,
                    parents[parent].ParentAddress);
      }
    } else
      usage[address2.y >> 3].Tick = 0;

    // This creates additional children which could be spread amongst the loops
    for (uint i = 0; i < 8; i++) {
      uint childAddress = blocks[address2.y + i].Child;
      if (childAddress != UINT_MAX &&
          usage[childAddress >> 3].Tick < USHRT_MAX) {
        GetSemaphor(semaphor);
        localRemaining = atomic_inc(dereferenceRemaining);
        dereferenceQueue[localRemaining] = (uint2)(address2.y, childAddress);
        ReleaseSemaphor(semaphor);
      }
    }
    GetSemaphor(semaphor);
    localRemaining = atomic_dec(dereferenceRemaining);
  }
  ReleaseSemaphor(semaphor);
}

void dereference(global Block *blocks, global Usage *usage,
                 global uint *parentSize, global bool *parentResidency,
                 global Parent *parents, global uint2 *dereferenceQueue,
                 global int *dereferenceRemaining, global int *semaphor,
                 uint startAddress, ushort tick) {
  // Build up the initial set of children to cull
  uint address = atomic_xchg(&blocks[startAddress].Child, UINT_MAX);
  int localRemaining = 0;
  if (address != UINT_MAX)
    for (uint i = 0; i < 8; i++) {
      uint childAddress = blocks[address + i].Child;
      if (childAddress != UINT_MAX &&
          usage[childAddress >> 3].Tick < USHRT_MAX) {
        // Semaphors are used to prevent dereferenceQueue being overwritten
        GetSemaphor(semaphor);
        localRemaining = atomic_inc(dereferenceRemaining);
        dereferenceQueue[localRemaining] = (uint2)(address, childAddress);
        ReleaseSemaphor(semaphor);
      }
    }
  helpDereference(blocks, usage, parentSize, parentResidency, parents,
                  dereferenceQueue, dereferenceRemaining, semaphor, tick);
}

uint findAddress(global Block *blocks, global Usage *usage,
                 global uint *childRequestId,
                 global ChildRequest *childRequests, global uint *parentSize,
                 global bool *parentResidency, global Parent *parents,
                 global uint2 *dereferenceQueue,
                 global int *dereferenceRemaining, global int *semaphor,
                 global ulong *addresses, UpdateInputData inputData,
                 uint address, uint depth) {
  ulong3 location = (ulong3)(addresses[address], addresses[address + 1],
                             addresses[address + 2]);
  address = baseLocation(inputData.N + 2, location);
  for (uchar i = inputData.N + 2; i < depth; i++) {
    if (usage[address >> 3].Tick < USHRT_MAX - 1) {
      usage[address >> 3].Tick = inputData.Tick;
    }
    // Hit the bottom of the tree and not found it
    if (blocks[address].Child == UINT_MAX) {
      requestChild(address, i, childRequestId, childRequests,
                   inputData.MaxChildRequestId, inputData.Tick, depth - i,
                   location);
      helpDereference(blocks, usage, parentSize, parentResidency, parents,
                      dereferenceQueue, dereferenceRemaining, semaphor,
                      inputData.Tick);
      return UINT_MAX;
    }
    address = blocks[address].Child;
    address += chunk(i, location);
  }
  return address;
}

//*******************************KERNELS***********************************

__kernel void prune(global ushort *bases, global Block *blocks,
                    global Usage *usage, global uint *childRequestId,
                    global ChildRequest *childRequests, global uint *parentSize,
                    global bool *parentResidency, global Parent *parents,
                    global uint2 *dereferenceQueue,
                    global int *dereferenceRemaining, global int *semaphor,
                    global Pruning *pruning, global BlockData *pruningBlockData,
                    global ulong *pruningAddresses, UpdateInputData inputData) {
  uint x = get_global_id(0);
  Pruning myPruning = pruning[x];
  uint address = myPruning.Address;

  // Update base block chunk data
  if (myPruning.Properties >> 4 & 1 == 1) {
    bases[address] = myPruning.Chunk;
    helpDereference(blocks, usage, parentSize, parentResidency, parents,
                    dereferenceQueue, dereferenceRemaining, semaphor,
                    inputData.Tick);
    return;
  }

  // If depth is UCHAR_MAX then this is a reference to a specific value
  if (myPruning.Depth != UCHAR_MAX) {
    address = findAddress(
        blocks, usage, childRequestId, childRequests, parentSize,
        parentResidency, parents, dereferenceQueue, dereferenceRemaining,
        semaphor, pruningAddresses, inputData, address, myPruning.Depth);
    if (address = UINT_MAX)
      return;
  } else {
    // Tick of 0 means that this has been dereferenced
    if (usage[address >> 3].Tick = 0) {
      helpDereference(blocks, usage, parentSize, parentResidency, parents,
                      dereferenceQueue, dereferenceRemaining, semaphor,
                      inputData.Tick);
      return;
    } else if (usage[address >> 3].Tick < USHRT_MAX - 1) {
      usage[address >> 3].Tick = inputData.Tick;
    }
  }

  // CullChild
  if (myPruning.Properties & 1 == 1) {
    dereference(blocks, usage, parentSize, parentResidency, parents,
                dereferenceQueue, dereferenceRemaining, semaphor, address,
                inputData.Tick);
    blocks[address].Child = myPruning.ChildAddress;
  } else {
    helpDereference(blocks, usage, parentSize, parentResidency, parents,
                    dereferenceQueue, dereferenceRemaining, semaphor,
                    inputData.Tick);
  }

  // AlterViolability & MakeInviolate
  if (myPruning.Properties >> 1 & 3 == 3) {
    usage[address >> 3].Tick = USHRT_MAX;
  } else if (myPruning.Properties >> 1 & 3 == 1) {
    usage[address >> 3].Tick = inputData.Tick;
  }

  // UpdateChunk
  if (myPruning.Properties >> 3 & 1 == 1) {
    blocks[address].Data = pruningBlockData[x];
    blocks[address].Chunk = myPruning.Chunk;
  }
}

__kernel void graft(global Block *blocks, global Usage *usage,
                    global uint *childRequestId,
                    global ChildRequest *childRequests, global uint *parentSize,
                    global bool *parentResidency, global Parent *parents,
                    global uint2 *dereferenceQueue,
                    global int *dereferenceRemaining, global int *semaphor,
                    global Grafting *grafting, global Block *graftingBlocks,
                    global ulong *graftingAddresses,
                    global uint *holdingAddresses, global uint *addressPosition,
                    UpdateInputData inputData) {
  uint id = get_global_id(0);
  uint workSize = get_global_size(0);
  uint iterator =
      (uint)native_divide((float)workSize, (float)(id * inputData.MemorySize));
  uint baseIterator = iterator;
  uint maxIterator =
      (uint)native_divide((float)((id + 1) * inputData.MemorySize),
                          (float)workSize) -
      1;
  uint workingTick;
  uint offset = inputData.Offset;
  // Accumulate graft array
  while (inputData.GraftSize < addressPosition[0]) {
    workingTick = usage[iterator].Tick;
    // Ensure that usage is not inviolable and is at least offset ticks ago
    if (workingTick == 0 ||
        (workingTick < USHRT_MAX - 1 &&
         ((workingTick > inputData.Tick &&
           (workingTick - USHRT_MAX - 2) < (inputData.Tick - offset)) ||
          (workingTick < inputData.Tick &&
           workingTick < (inputData.Tick - offset))))) {
      uint myAddressPosition = atomic_inc(addressPosition);
      // Break out if address limit has already been reached
      if (myAddressPosition >= inputData.GraftSize) {
        helpDereference(blocks, usage, parentSize, parentResidency, parents,
                        dereferenceQueue, dereferenceRemaining, semaphor,
                        inputData.Tick);
        break;
      }
      holdingAddresses[myAddressPosition] = iterator;
      dereference(blocks, usage, parentSize, parentResidency, parents,
                  dereferenceQueue, dereferenceRemaining, semaphor,
                  usage[myAddressPosition].Parent, inputData.Tick);
      // Ensure that the address isn't picked up on a second pass
      usage[myAddressPosition].Tick = inputData.Tick;
    }

    if (iterator == maxIterator) {
      iterator = baseIterator;
      offset = offset >> 1;
    } else
      iterator++;
    helpDereference(blocks, usage, parentSize, parentResidency, parents,
                    dereferenceQueue, dereferenceRemaining, semaphor,
                    inputData.Tick);
  }
  Grafting myGrafting = grafting[id];
  uint address = myGrafting.GraftAddress;
  // Seek out true address if the grafting address is just a set of coordinates
  if (myGrafting.Depth != UCHAR_MAX) {
    address = findAddress(
        blocks, usage, childRequestId, childRequests, parentSize,
        parentResidency, parents, dereferenceQueue, dereferenceRemaining,
        semaphor, graftingAddresses, inputData, address, myGrafting.Depth);
    if (address = UINT_MAX)
      return;
    if (blocks[address].Child != UINT_MAX)
      dereference(blocks, usage, parentSize, parentResidency, parents,
                  dereferenceQueue, dereferenceRemaining, semaphor, address,
                  inputData.Tick);
    else
      helpDereference(blocks, usage, parentSize, parentResidency, parents,
                      dereferenceQueue, dereferenceRemaining, semaphor,
                      inputData.Tick);
  } else
    helpDereference(blocks, usage, parentSize, parentResidency, parents,
                    dereferenceQueue, dereferenceRemaining, semaphor,
                    inputData.Tick);

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

__kernel void traceVoxel(global ushort *bases, global Block *blocks,
                         global Usage *usage, global uint *childRequestId,
                         global ChildRequest *childRequests,
                         __write_only image2d_t outputImage,
                         TraceInputData _input) {
  uchar depth = 1;
  bool inside = true;
  uchar chunkPosition;
  float C = -1;
  uint offset;
  uint address = 0;
  uint x = get_global_id(0);
  uint y = get_global_id(1);
  int2 coord = (int2)(x, y);
  uint depthHeap[64];
  uchar baseChunk = 0;
  WorkingData *_data = setup(coord, _input);

  depthHeap[_data->N + 1] = UINT_MAX;
  if (startTrace(_data)) {
    while (depth > 0 && !leaving(_data)) {
      inside = true;
      while (inside && !leaving(_data)) {
        chunkPosition = chunk(depth, _data->Location);
        offset = powSum(depth - 1);
        address = baseLocation(depth, _data->Location);
        if ((bases[offset + address] >> (chunkPosition * 2) & 2) == 2) {
          if (depth == _data->N) {
            depth += 2;
            depthHeap[depth] = baseLocation(depth, _data->Location);
            baseChunk = chunkPosition;
            while (depth > (_data->N + 1) && !leaving(_data)) {
              C = -1;
              // Update usage
              uint usageAddress = depthHeap[depth];
              usageAddress = usageAddress >> 3;
              if (usage[usageAddress].Tick < USHRT_MAX - 1) {
                usage[usageAddress].Tick = _data->Tick;
              }
              inside = true;
              while (inside && !leaving(_data)) {
                chunkPosition = chunk(depth, _data->Location);
                uint localAddress = depthHeap[depth];

                if ((blocks[localAddress].Chunk >> (chunkPosition * 2) & 2) ==
                    2) {
                  if (C == -1)
                    C = coneLevel(_data);

                  depthHeap[depth + 1] =
                      blocks[localAddress].Child + chunkPosition;
                  // C value is too diffuse to use
                  if (C < (_data->N + 2)) {
                    depth = _data->N + 2;
                    if (saveVoxelTrace(average(localAddress, blocks, C, _data),
                                       _data)) {
                      writeData(outputImage, _data);
                      return;
                    }
                  }
                  // C value requires me to go up a level
                  else if (C < depth) {
                    inside = false;
                  }
                  // No additional data could be found at child depth
                  else if (blocks[localAddress].Child == UINT_MAX) {
                    requestChild(localAddress, depth, childRequestId,
                                 childRequests, _data->MaxChildRequestId,
                                 _data->Tick, 1, _data->Location);
                    if (saveVoxelTrace(blocks[localAddress].Data, _data)) {
                      writeData(outputImage, _data);
                      return;
                    }
                  }
                  // Navigate to child
                  else if (C > (depth + 1)) {
                    depth++;
                  }
                  // Resolve the colour of this voxel
                  else if (depth <= C && C <= (depth + 1)) {
                    if (saveVoxelTrace(blocks[localAddress].Data, _data)) {
                      writeData(outputImage, _data);
                      return;
                    }
                  }
                } else {
                  inside = traverseChunk(depth, chunkPosition, _data);
                }
              }
              if (depth == (_data->N + 2) &&
                  baseChunk == chunk(_data->N + 1, _data->Location)) {
                depthHeap[depth] = baseLocation(depth, _data->Location);
              } else {
                depth--;
              }
              chunkPosition = chunk(depth, _data->Location);
            }
            depth = _data->N;
          } else {
            depth++;
          }
        } else {
          inside = traverseChunk(depth, chunkPosition, _data);
        }
      }
      if (depth != 1) {
        depth--;
      }
    }
  }
  saveVoxelTrace(background(_data), _data);
  writeData(outputImage, _data);
  // calculate uv coordinates
//   float u = x / (float)_input.ScreenSize[0];
//   float v = y / (float)_input.ScreenSize[1];
//   u = u * 2.0f - 1.0f;
//   v = v * 2.0f - 1.0f;

//   // calculate simple sine wave pattern
//   float freq = 4.0f;
//   float w = sin(u * freq + _input.Tick) * cos(v * freq + _input.Tick) * 0.5f;
//   write_imagef(outputImage, coord, (float4)(1, w, 0, 1));
}

__kernel void traceMesh() {}

__kernel void traceParticle() {}

__kernel void traceLight() {}

__kernel void spawnRays() {}

__kernel void resolveImage() {}