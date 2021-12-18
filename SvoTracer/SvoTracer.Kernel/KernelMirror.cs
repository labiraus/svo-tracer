using OpenTK.Mathematics;
using SvoTracer.Domain.Models;
using System;

namespace SvoTracer.Kernel
{
	public class KernelMirror
	{
		public static uint x = 0;
		public static uint y = 0;
		static T atomic_xchg<T>(ref T a, T b)
		{
			var old = a;
			a = b;
			return old;
		}
		static int atomic_inc(ref int a) => a++;
		static uint atomic_inc(ref uint a) => a++;
		static int atomic_dec(ref int a) => a--;
		static uint atomic_dec(ref uint a) => a--;
		static uint get_global_id(int input)
		{
			switch (input)
			{
				case 0:
					return x;
				case 1:
					return y;
			}
			return 0;
		}
		static uint get_global_size(int _)
		{
			return 16;
		}
		static float native_divide(float x, float y)
		{
			return x / y;
		}
		static float native_sin(float theta)
		{
			return (float)Math.Sin((double)theta);
		}
		static float native_cos(float theta)
		{
			return (float)Math.Cos((double)theta);
		}

		static float dot(Vector3 vec1, Vector3 vec2)
		{
			return Vector3.Dot(vec1, vec2);
		}

		/// <summary>
		/// Converts a float between 0 and 1 into a ulong coordinate
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		static ulong floatToULong(float x)
		{
			//float rounding errors mean that this is the closest you can get to 1 at 24 bits deep
			if (x >= 0.999999911F)
				return ulong.MaxValue;
			else
				return (ulong)(Math.Abs(x) * ulong.MaxValue);
		}

		static float ulongToFloat(ulong x)
		{
			return native_divide((float)x, (float)ulong.MaxValue);
		}

		static void getSemaphor(ref int semaphor)
		{
			int occupied = atomic_xchg(ref semaphor, 1);
			while (occupied > 0)
			{
				occupied = atomic_xchg(ref semaphor, 1);
			}
		}

		static void releaseSemaphor(ref int semaphor)
		{
			int prevVal = atomic_xchg(ref semaphor, 0);
		}

		static ulong roundUlong(ulong value, byte depth, bool roundUp)
		{
			if (roundUp)
				return ((value & (ulong.MaxValue - (ulong.MaxValue >> depth + 1))) + (ulong.MaxValue - (ulong.MaxValue >> 1) >> depth)) - value;

			ulong output = value - ((value & (ulong.MaxValue - (ulong.MaxValue >> depth + 1))) - 1);

			if (output >= value)
				return value;

			return output;


		}

		static uint powSum(byte depth)
		{
			uint output = 0;
			for (int i = 1; i <= depth; i++)
				output += (uint)(1 << (3 * i));
			return output;
		}

		static Vector3 normalVector(short pitch, short yaw)
		{
			// yaw * 2pi/ushort max
			float fYaw = yaw * (float)Math.PI / 32767.5f;
			float fPitch = pitch * (float)Math.PI / 65535.0f;
			float sinYaw = native_sin(fYaw);
			float cosYaw = native_cos(fYaw);
			float sinPitch = native_sin(fPitch);
			float cosPitch = native_cos(fPitch);

			return new Vector3(cosYaw * cosPitch, sinYaw * cosPitch, sinPitch);
		}


		static byte chunkPosition(byte depth, Location location)
		{
			return (byte)((location.X >> (64 - depth - 1) & 1) +
				((location.Y >> (64 - depth - 1) & 1) << 1) +
				((location.Z >> (64 - depth - 1) & 1) << 2));
		}

		/// <summary>
		/// Deduces array offset of a base's location given its depth
		/// Bases are stored as a dense octree down to depth BaseDepth
		/// </summary>
		/// <param name="depth"></param>
		/// <param name="location"></param>
		/// <returns></returns>
		static uint baseLocation(byte depth, Location location)
		{
			uint output = 0;
			for (byte i = 0; i < depth; i++)
				output = (output << 3) + chunkPosition(i, location);

			return output;
		}

		/// <summary>
		/// Calculates the cone size at a given depth from FoV and pixel diameter data. Largest cone size wins
		/// </summary>
		/// <param name="data"></param>
		/// <param name="m"></param>
		/// <returns></returns>
		static float coneSize(float m, ref WorkingData _data)
		{
			var fov = Math.Abs(_data.DoF[0] * (_data.DoF[1] - m));
			var eye = _data.PixelFoV * m;
			if (eye < fov)
				return fov;
			else
				return eye;
		}

		/// <summary>
		/// Determine the maximum tree depth for a cone at this location
		/// </summary>
		/// <param name="_data"></param>
		/// <returns></returns>
		static void setConeDepth(ref WorkingData _data)
		{
			_data.ConeDepth = -(float)Math.Log(coneSize(Math.Abs(new Vector3(_data.Origin.X - ulongToFloat(_data.Location.X),
				_data.Origin.Y - ulongToFloat(_data.Location.Y),
				_data.Origin.Z - ulongToFloat(_data.Location.Z)).Length), ref _data), 2);
		}

		static Block background(ref WorkingData _data)
		{
			return new Block()
			{
				ColourR = 0,
				ColourB = 0,
				ColourG = 0,
				Opacity = byte.MaxValue,
			};
		}

		static void writeData(string image, ref WorkingData _data)
		{
			Console.Write(_data.ColourR + _data.ColourG + _data.ColourB > 0 ? 1 : 0);
		}

		/// <summary>
		/// Combine _data colour + opacity with block data
		/// </summary>
		/// <param name="block"></param>
		/// <param name="_data"></param>
		/// <returns>Whether max opacity has been reached</returns>
		static bool saveVoxelTrace(Block blockData, ref WorkingData _data)
		{
			Vector3 normal = normalVector(blockData.NormalPitch, blockData.NormalYaw);
			// _data->ColourR = normal.x;
			// _data->ColourB = normal.y;
			// _data->ColourG = normal.z;
			Vector3 reflection = _data.Direction - (2 * dot(_data.Direction, normal) * normal);
			reflection += new Vector3(1, 1, 1);
			reflection *= 255;
			_data.ColourR = native_divide(blockData.ColourR + reflection.X, 510.0f);
			_data.ColourB = native_divide(blockData.ColourB + reflection.Y, 510.0f);
			_data.ColourG = native_divide(blockData.ColourG + reflection.Z, 510.0f);
			_data.Opacity = (byte)((int)_data.Opacity + blockData.Opacity);
			return _data.Opacity >= _data.MaxOpacity;
		}

		/// <summary>
		/// Combine _data colour + opacity with background colour and write to output
		/// </summary>
		/// <param name="image"></param>
		/// <param name="_data"></param>
		static void writeBackgroundData(string image, ref WorkingData _data)
		{
			//saveVoxelTrace(background(ref _data), ref _data);
			writeData(image, ref _data);
		}

		static Block average(uint address, Block[] blocks, ref WorkingData _data)
		{
			//Average like heck            
			return blocks[address];
		}

		static void requestChild(uint address, byte depth, ref uint childRequestId,
				  ChildRequest[] childRequests, uint maxChildRequestId,
				  ushort tick, byte treeSize, Location location)
		{
			uint currentId = atomic_inc(ref childRequestId);
			if (currentId >= maxChildRequestId)
				return;
			ChildRequest request;
			request.Address = address;
			request.Tick = tick;
			request.Depth = depth;
			request.Location.X = location.X;
			request.Location.Y = location.Y;
			request.Location.Z = location.Z;
			request.TreeSize = treeSize;
			childRequests[currentId] = request;
		}

		static bool leaving(ref WorkingData _data)
		{
			return (!_data.DirectionSignX && _data.Location.X == 0) ||
				(!_data.DirectionSignY && _data.Location.Y == 0) ||
				(!_data.DirectionSignZ && _data.Location.Z == 0) ||
				(_data.DirectionSignX && _data.Location.X == ulong.MaxValue) ||
				(_data.DirectionSignY && _data.Location.Y == ulong.MaxValue) ||
				(_data.DirectionSignZ && _data.Location.Z == ulong.MaxValue);
		}

		static byte comparePositions(byte depth, Location previousLocation, ref WorkingData _data)
		{
			byte newPosition = chunkPosition(depth, _data.Location);
			byte previousPosition = chunkPosition(depth, previousLocation);
			while (((previousPosition & 1) == (_data.DirectionSignX ? 1 : 0) && (newPosition & 1) != (_data.DirectionSignX ? 1 : 0)) ||
				   (((previousPosition >> 1) & 1) == (_data.DirectionSignY ? 1 : 0) && ((newPosition >> 1) & 1) != (_data.DirectionSignY ? 1 : 0)) ||
				   (((previousPosition >> 2) & 1) == (_data.DirectionSignZ ? 1 : 0) && ((newPosition >> 2) & 1) != (_data.DirectionSignZ ? 1 : 0)))
			{
				if (depth == 1)
					break;
				depth--;
				newPosition = chunkPosition(depth, _data.Location);
				previousPosition = chunkPosition(depth, previousLocation);
			}
			return depth;
		}

		/// <summary>
		/// Moves to the nearest neighboring chunk along the Direction vector
		/// </summary>
		/// <param name="data"></param>
		/// <param name="depth"></param>
		/// <param name="position"></param>
		/// <returns>New chunk position or 8 for outside of block</returns>
		static void traverseChunk(byte depth, ref WorkingData _data)
		{
			ulong dx = roundUlong(_data.Location.X, depth, _data.DirectionSignX);
			ulong dy = roundUlong(_data.Location.Y, depth, _data.DirectionSignY);
			ulong dz = roundUlong(_data.Location.Z, depth, _data.DirectionSignZ);

			float ax = Math.Abs(dx * _data.InvDirection.X);
			float ay = Math.Abs(dy * _data.InvDirection.Y);
			float az = Math.Abs(dz * _data.InvDirection.Z);

			if (ax <= ay && ax <= az)
			{
				float udx = ulongToFloat(dx);
				dy = floatToULong(_data.Direction.Y * _data.InvDirection.X * udx);
				dz = floatToULong(_data.Direction.Z * _data.InvDirection.X * udx);
			}
			else if (ay <= ax && ay <= az)
			{
				float udy = ulongToFloat(dy);
				dx = floatToULong(_data.Direction.X * _data.InvDirection.Y * udy);
				dz = floatToULong(_data.Direction.Z * _data.InvDirection.Y * udy);
			}
			else
			{
				float udz = ulongToFloat(dz);
				dx = floatToULong(_data.Direction.X * _data.InvDirection.Z * udz);
				dy = floatToULong(_data.Direction.Y * _data.InvDirection.Z * udz);
			}

			if (_data.DirectionSignX)
				_data.Location.X += dx;
			else
				_data.Location.X -= dx;

			if (_data.DirectionSignY)
				_data.Location.Y += dy;
			else
				_data.Location.Y -= dy;

			if (_data.DirectionSignZ)
				_data.Location.Z += dz;
			else
				_data.Location.Z -= dz;

			// if trafersal has overflowed ulong then the octree has been left
			if (_data.DirectionSignX && _data.Location.X == 0)
				_data.Location.X = ulong.MaxValue;
			else if (!_data.DirectionSignX && _data.Location.X == ulong.MaxValue)
				_data.Location.X = 0;

			if (_data.DirectionSignY && _data.Location.Y == 0)
				_data.Location.Y = ulong.MaxValue;
			else if (!_data.DirectionSignY && _data.Location.Y == ulong.MaxValue)
				_data.Location.Y = 0;

			if (_data.DirectionSignZ && _data.Location.Z == 0)
				_data.Location.Z = ulong.MaxValue;
			else if (!_data.DirectionSignZ && _data.Location.Z == ulong.MaxValue)
				_data.Location.Z = 0;

			setConeDepth(ref _data);
		}

		/// <summary>
		/// Sets location and determines whether the ray hits the octree
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		static bool startTrace(ref WorkingData _data)
		{
			bool x0 = _data.Origin.X < 0;
			bool x1 = _data.Origin.X > 1;
			bool xp = _data.Direction.X == 0;
			bool xd = _data.Direction.X > 0;
			bool y0 = _data.Origin.Y < 0;
			bool y1 = _data.Origin.Y > 1;
			bool yp = _data.Direction.Y == 0;
			bool yd = _data.Direction.Y > 0;
			bool z0 = _data.Origin.Z < 0;
			bool z1 = _data.Origin.Z > 1;
			bool zp = _data.Direction.Z == 0;
			bool zd = _data.Direction.Z > 0;
			float location0 = _data.Origin.X;
			float location1 = _data.Origin.Y;
			float location2 = _data.Origin.Z;
			float m = 0;
			float mx = 0;
			float my = 0;
			float mz = 0;
			int xyz =
				(x0 ? 0b100000 : 0b000000) + (x1 ? 0b010000 : 0b000000) +
				(y0 ? 0b001000 : 0b000000) + (y1 ? 0b000100 : 0b000000) +
				(z0 ? 0b000010 : 0b000000) + (z1 ? 0b000001 : 0b000000);

			if (xyz == 0b000000)
			{
				_data.Location = new Location(floatToULong(location0), floatToULong(location1), floatToULong(location2));
				return true;
			}
			//DIR is parallel to one axis and outside of that axis's box walls
			else if ((x0 | x1) & xp)
				return false;
			else if ((y0 | y1) & yp)
				return false;
			else if ((z0 | z1) & zp)
				return false;

			//DIR is divergent from one of the planes POS is outside of
			else if (x0 & !xd) // x0
				return false;
			else if (x1 & xd) // x1
				return false;
			else if (y0 & !yd) // y0
				return false;
			else if (y1 & yd) // y1
				return false;
			else if (z0 & !zd) // z0
				return false;
			else if (z1 & zd) // z1
				return false;

			switch (xyz)
			{
				case 0b000000:
					break;

				//Adjacent to one of the 6 planes
				case 0b100000: // x0
					m = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					location0 = 0;
					location1 = _data.Origin.Y + (_data.Direction.Y * m);
					location2 = _data.Origin.Z + (_data.Direction.Z * m);
					break;
				case 0b010000: // x1
					m = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					location0 = 1;
					location1 = _data.Origin.Y + (_data.Direction.Y * m);
					location2 = _data.Origin.Z + (_data.Direction.Z * m);
					break;
				case 0b001000: // y0
					m = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					location0 = _data.Origin.X + (_data.Direction.X * m);
					location1 = 0;
					location2 = _data.Origin.Z + (_data.Direction.Z * m);
					break;
				case 0b000100: // y1
					m = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					location0 = _data.Origin.X + (_data.Direction.X * m);
					location1 = 1;
					location2 = _data.Origin.Z + (_data.Direction.Z * m);
					break;
				case 0b000010: // z0
					m = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					location0 = _data.Origin.X + (_data.Direction.X * m);
					location1 = _data.Origin.Y + (_data.Direction.Y * m);
					location2 = 0;
					break;
				case 0b000001: // z1
					m = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					location0 = _data.Origin.X + (_data.Direction.X * m);
					location1 = _data.Origin.Y + (_data.Direction.Y * m);
					location2 = 1;
					break;

				//The 8 side arcs outside of the box between two of the faces on one axis and near to two faces on the other two axies
				//z face
				case 0b101000: // x0y0
					mx = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					if (mx >= my)
					{
						m = mx;
						location0 = 0;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 0;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					break;
				case 0b011000: // x1y0
					mx = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					if (mx >= my)
					{
						m = mx;
						location0 = 1;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 0;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					break;
				case 0b100100: // x0y1
					mx = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					if (mx >= my)
					{
						m = mx;
						location0 = 0;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 1;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					break;
				case 0b010100: // x1y1
					mx = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					if (mx >= my)
					{
						m = mx;
						location0 = 1;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 1;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					break;
				//y face
				case 0b100010: // x0z0
					mx = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					mz = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= mz)
					{
						m = mx;
						location0 = 0;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 0;
					}
					break;
				case 0b010010: // x1z0
					mx = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					mz = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= mz)
					{
						m = mx;
						location0 = 1;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 0;
					}
					break;
				case 0b100001: // x0z1
					mx = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					mz = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= mz)
					{
						m = mx;
						location0 = 0;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 1;
					}
					break;
				case 0b010001: // x1z1
					mx = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					mz = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= mz)
					{
						m = mx;
						location0 = 1;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 1;
					}
					break;
				//x face
				case 0b001010: // y0z0
					my = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					if (my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 0;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 0;
					}
					break;
				case 0b000110: // y1z0
					my = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					if (my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 1;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 0;
					}
					break;
				case 0b001001: // y0z1
					my = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					if (my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 0;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 1;
					}
					break;
				case 0b000101: // y1z1
					my = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					if (my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 1;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 1;
					}
					break;

				//The 8 corners
				case 0b101010: // x0y0z0
					mx = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= my & mx >= mz)
					{
						m = mx;
						location0 = 0;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else if (my >= mx & my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 0;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 0;
					}
					break;
				case 0b011010: // x1y0z0
					mx = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= my & mx >= mz)
					{
						m = mx;
						location0 = 1;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else if (my >= mx & my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 0;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 0;
					}
					break;
				case 0b100110: // x0y1z0
					mx = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= my & mx >= mz)
					{
						m = mx;
						location0 = 0;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else if (my >= mx & my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 1;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 0;
					}
					break;
				case 0b010110: // x1y1z0
					mx = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((0 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= my & mx >= mz)
					{
						m = mx;
						location0 = 1;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else if (my >= mx & my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 1;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 0;
					}
					break;
				case 0b101001: // x0y0z1
					mx = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= my & mx >= mz)
					{
						m = mx;
						location0 = 0;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else if (my >= mx & my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 0;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 1;
					}
					break;
				case 0b011001: // x1y0z1
					mx = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((0 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= my & mx >= mz)
					{
						m = mx;
						location0 = 1;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else if (my >= mx & my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 0;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 1;
					}
					break;
				case 0b100101: // x0y1z1
					mx = Math.Abs((0 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= my & mx >= mz)
					{
						m = mx;
						location0 = 0;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else if (my >= mx & my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 1;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mx;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 1;
					}
					break;
				case 0b010101: // x1y1z1
					mx = Math.Abs((1 - _data.Origin.X) * _data.InvDirection.X);
					my = Math.Abs((1 - _data.Origin.Y) * _data.InvDirection.Y);
					mz = Math.Abs((1 - _data.Origin.Z) * _data.InvDirection.Z);
					if (mx >= my & mx >= mz)
					{
						m = mx;
						location0 = 1;
						location1 = _data.Origin.Y + (_data.Direction.Y * mx);
						location2 = _data.Origin.Z + (_data.Direction.Z * mx);
					}
					else if (my >= mx & my >= mz)
					{
						m = my;
						location0 = _data.Origin.X + (_data.Direction.X * my);
						location1 = 1;
						location2 = _data.Origin.Z + (_data.Direction.Z * my);
					}
					else
					{
						m = mz;
						location0 = _data.Origin.X + (_data.Direction.X * mz);
						location1 = _data.Origin.Y + (_data.Direction.Y * mz);
						location2 = 1;
					}
					break;
				default:
					return false;
			}

			_data.Location = new Location(floatToULong(location0), floatToULong(location1), floatToULong(location2));
			float c = coneSize(m, ref _data);

			return !(location0 < -c || location0 > 1 + c || location1 < -c || location1 > 1 + c || location2 < -c || location2 > 1 + c);
		}

		/// <summary>
		/// Calculates the starting angle
		/// </summary>
		/// <param name="coord">position - thread ID</param>
		/// <param name="ANG">Direction faced</param>
		/// <param name="EYE">Horizonal/vertical FoV angle</param>
		/// <param name="SCR">Screen size</param>
		static WorkingData setup(Vector2i coord, ref TraceInput _input)
		{
			WorkingData data = new();
			data.Coord = new Vector2i(coord[0], coord[1]);
			data.ScreenSize = new Vector2i(_input.ScreenSize[0], _input.ScreenSize[1]);
			//Horizontal and vertical offset angles float h and v
			float u = _input.FoV[0] *
					  native_divide((native_divide((float)_input.ScreenSize[0], 2) - (float)coord.X), (float)_input.ScreenSize[0]);
			float v = _input.FoV[1] *
					  native_divide((native_divide((float)_input.ScreenSize[1], 2) - (float)coord.Y), (float)_input.ScreenSize[1]);
			float sinU = native_sin(u);
			float cosU = native_cos(u);
			float sinV = native_sin(v);
			float cosV = native_cos(v);

			float matRot0x = cosU * cosV;
			float matRot1x = (0 - sinU) * cosV;
			float matRot2x = sinV;
			var dir = new Vector3(_input.Facing.M11 * matRot0x + _input.Facing.M21 * matRot1x + _input.Facing.M31 * matRot2x,
								  _input.Facing.M12 * matRot0x + _input.Facing.M22 * matRot1x + _input.Facing.M32 * matRot2x,
								  _input.Facing.M13 * matRot0x + _input.Facing.M23 * matRot1x + _input.Facing.M33 * matRot2x);
			data.Direction = dir.Normalized();
			data.InvDirection = new Vector3(1 / data.Direction.X, 1 / data.Direction.Y, 1 / data.Direction.Z);
			data.DirectionSignX = data.Direction.X >= 0;
			data.DirectionSignY = data.Direction.Y >= 0;
			data.DirectionSignZ = data.Direction.Z >= 0;
			data.Origin = _input.Origin;
			data.BaseDepth = _input.BaseDepth;
			data.DoF = _input.DoF;
			data.MaxOpacity = _input.MaxOpacity;
			data.PixelFoV = _input.FoV[0] / _input.ScreenSize[0];
			return data;
		}

		static void helpDereference(Block[] blocks, Usage[] usage, uint[] parentSize, bool[] parentResidency,
							 Parent[] parents, uint2[] dereferenceQueue,
							 ref int dereferenceRemaining, ref int semaphor,
							 ushort tick)
		{
			// All local threads get to play the dereferencing game
			getSemaphor(ref semaphor);
			int localRemaining = atomic_dec(ref dereferenceRemaining);
			uint2 address2 = new();
			while (localRemaining >= 0)
			{
				address2 = dereferenceQueue[localRemaining];
				releaseSemaphor(ref semaphor);
				// if Tick is ushort.Max - 1 then it has multiple parents
				if (usage[address2.y >> 3].Tick == ushort.MaxValue - 1)
				{
					uint parent = usage[address2.y >> 3].Parent;
					uint previousParent = parent;
					bool finished = false;
					bool first = true;
					bool last = false;
					while (!finished)
					{
						if (parents[parent].ParentAddress == address2.x)
						{
							// While loop locks parents[parent].NextElement to this thread
							uint nextElement = uint.MaxValue - 1;
							while (nextElement == uint.MaxValue - 1)
							{
								nextElement =
									atomic_xchg(ref parents[parent].NextElement, uint.MaxValue - 1);
							}

							// Last element in the list so previous element becomes the last
							// element
							if (nextElement == uint.MaxValue)
							{
								parentResidency[parent] = false;
								atomic_xchg(ref parents[previousParent].NextElement, uint.MaxValue);
								atomic_xchg(ref parents[parent].NextElement, uint.MaxValue);
								first = usage[address2.y >> 3].Parent == previousParent;
							}
							// Move next element forwards one
							else
							{
								atomic_xchg(ref parents[parent].ParentAddress,
											parents[nextElement].ParentAddress);
								atomic_xchg(ref parents[parent].NextElement,
											parents[nextElement].NextElement);
								parentResidency[nextElement] = false;
								last = parents[parent].NextElement == uint.MaxValue;
							}
							finished = true;
							break;
						}
						else if (parents[parent].NextElement == uint.MaxValue)
						{
							finished = true;
						}
						else
						{
							previousParent = parent;
							parent = parents[parent].NextElement;
						}
						first = false;
					}
					// If the parent removed from the list was the first and last then the
					// block is no longer multi parent
					if (first && last)
					{
						parentResidency[parent] = false;
						usage[address2.y >> 3].Tick = tick;
						atomic_xchg(ref usage[address2.y >> 3].Parent,
									parents[parent].ParentAddress);
					}
				}
				else
					usage[address2.y >> 3].Tick = 0;

				// This creates additional children which could be spread amongst the loops
				for (uint i = 0; i < 8; i++)
				{
					uint childAddress = blocks[address2.y + i].Child;
					if (childAddress != uint.MaxValue &&
						usage[childAddress >> 3].Tick < ushort.MaxValue)
					{
						getSemaphor(ref semaphor);
						localRemaining = atomic_inc(ref dereferenceRemaining);
						dereferenceQueue[localRemaining] = new uint2(address2.y, childAddress);
						releaseSemaphor(ref semaphor);
					}
				}
				getSemaphor(ref semaphor);
				localRemaining = atomic_dec(ref dereferenceRemaining);
			}
			releaseSemaphor(ref semaphor);
		}

		static void dereference(Block[] blocks, Usage[] usage,
						 uint[] parentSize, bool[] parentResidency,
						 Parent[] parents, uint2[] dereferenceQueue,
						 ref int dereferenceRemaining, ref int semaphor,
						 uint startAddress, ushort tick)
		{
			// Build up the initial set of children to cull
			uint address = atomic_xchg(ref blocks[startAddress].Child, uint.MaxValue);
			int localRemaining = 0;
			if (address != uint.MaxValue)
				for (uint i = 0; i < 8; i++)
				{
					uint childAddress = blocks[address + i].Child;
					if (childAddress != uint.MaxValue &&
						usage[childAddress >> 3].Tick < ushort.MaxValue)
					{
						// Semaphors are used to prevent dereferenceQueue being overwritten
						getSemaphor(ref semaphor);
						localRemaining = atomic_inc(ref dereferenceRemaining);
						dereferenceQueue[localRemaining] = new uint2(address, childAddress);
						releaseSemaphor(ref semaphor);
					}
				}
			helpDereference(blocks, usage, parentSize, parentResidency, parents,
							dereferenceQueue, ref dereferenceRemaining, ref semaphor, tick);
		}

		static uint findAddress(Block[] blocks, Usage[] usage,
						 ref uint childRequestId,
						 ChildRequest[] childRequests, uint[] parentSize,
						 bool[] parentResidency, Parent[] parents,
						 uint2[] dereferenceQueue,
						 ref int dereferenceRemaining, ref int semaphor,
						 ulong[] addresses, UpdateInputData inputData,
						 uint address, uint depth)
		{
			Location location = new Location(addresses[address], addresses[address + 1],
									   addresses[address + 2]);
			address = baseLocation((byte)(inputData.BaseDepth + 2), location);
			for (byte i = (byte)(inputData.BaseDepth + 2); i < depth; i++)
			{
				if (usage[address >> 3].Tick < ushort.MaxValue - 1)
				{
					usage[address >> 3].Tick = inputData.Tick;
				}
				// Hit the bottom of the tree and not found it
				if (blocks[address].Child == uint.MaxValue)
				{
					requestChild(address, i, ref childRequestId, childRequests,
								 inputData.MaxChildRequestId, inputData.Tick, (byte)(depth - i),
								 location);
					helpDereference(blocks, usage, parentSize, parentResidency, parents,
									dereferenceQueue, ref dereferenceRemaining, ref semaphor,
									inputData.Tick);
					return uint.MaxValue;
				}
				address = blocks[address].Child;
				address += chunkPosition(i, location);
			}
			return address;
		}

		public static void Prune(ushort[] bases, Block[] blocks,
							Usage[] usage, ref uint childRequestId,
							ChildRequest[] childRequests, uint[] parentSize,
							bool[] parentResidency, Parent[] parents,
							uint2[] dereferenceQueue,
							ref int dereferenceRemaining, ref int semaphor,
							Pruning[] pruning, Block[] pruningBlockData,
							ulong[] pruningAddresses, UpdateInputData inputData)
		{
			uint x = get_global_id(0);
			Pruning myPruning = pruning[x];
			uint address = myPruning.Address;

			// Update base block chunk data
			if ((byte)(myPruning.Properties >> 4 & 1) == 1)
			{
				bases[address] = myPruning.Chunk;
				helpDereference(blocks, usage, parentSize, parentResidency, parents,
								dereferenceQueue, ref dereferenceRemaining, ref semaphor,
								inputData.Tick);
				return;
			}

			// If depth is byte.MaxValue then this is a reference to a specific value
			if (myPruning.Depth != byte.MaxValue)
			{
				address = findAddress(
					blocks, usage, ref childRequestId, childRequests, parentSize,
					parentResidency, parents, dereferenceQueue, ref dereferenceRemaining,
					ref semaphor, pruningAddresses, inputData, address, myPruning.Depth);
				if (address == uint.MaxValue)
					return;
			}
			else
			{
				// Tick of 0 means that this has been dereferenced
				if (usage[address >> 3].Tick == 0)
				{
					helpDereference(blocks, usage, parentSize, parentResidency, parents,
									dereferenceQueue, ref dereferenceRemaining, ref semaphor,
									inputData.Tick);
					return;
				}
				else if (usage[address >> 3].Tick < ushort.MaxValue - 1)
				{
					usage[address >> 3].Tick = inputData.Tick;
				}
			}

			// CullChild
			if ((byte)(myPruning.Properties & 1) == 1)
			{
				dereference(blocks, usage, parentSize, parentResidency, parents,
							dereferenceQueue, ref dereferenceRemaining, ref semaphor, address,
							inputData.Tick);
				blocks[address].Child = myPruning.ChildAddress;
			}
			else
			{
				helpDereference(blocks, usage, parentSize, parentResidency, parents,
								dereferenceQueue, ref dereferenceRemaining, ref semaphor,
								inputData.Tick);
			}

			// AlterViolability & MakeInviolate
			if ((byte)(myPruning.Properties >> 1 & 3) == 3)
			{
				usage[address >> 3].Tick = ushort.MaxValue;
			}
			else if ((byte)(myPruning.Properties >> 1 & 3) == 1)
			{
				usage[address >> 3].Tick = inputData.Tick;
			}

			// UpdateChunk
			if ((byte)(myPruning.Properties >> 3 & 1) == 1)
			{
				blocks[address]= pruningBlockData[x];
				blocks[address].Chunk = myPruning.Chunk;
			}
		}

		public static void Graft(Block[] blocks, Usage[] usage,
							ref uint childRequestId,
							ChildRequest[] childRequests, uint[] parentSize,
							bool[] parentResidency, Parent[] parents,
							uint2[] dereferenceQueue,
							ref int dereferenceRemaining, ref int semaphor,
							Grafting[] grafting, Block[] graftingBlocks,
							ulong[] graftingAddresses,
							uint[] holdingAddresses, ref uint addressPosition,
							UpdateInputData inputData)
		{
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
			while (inputData.GraftSize < addressPosition)
			{
				workingTick = usage[iterator].Tick;
				// Ensure that usage is not inviolable and is at least offset ticks ago
				if (workingTick == 0 ||
					(workingTick < ushort.MaxValue - 1 &&
					 ((workingTick > inputData.Tick &&
					   (workingTick - ushort.MaxValue - 2) < (inputData.Tick - offset)) ||
					  (workingTick < inputData.Tick &&
					   workingTick < (inputData.Tick - offset)))))
				{
					uint myAddressPosition = atomic_inc(ref addressPosition);
					// Break out if address limit has already been reached
					if (myAddressPosition >= inputData.GraftSize)
					{
						helpDereference(blocks, usage, parentSize, parentResidency, parents,
										dereferenceQueue, ref dereferenceRemaining, ref semaphor,
										inputData.Tick);
						break;
					}
					holdingAddresses[myAddressPosition] = iterator;
					dereference(blocks, usage, parentSize, parentResidency, parents,
								dereferenceQueue, ref dereferenceRemaining, ref semaphor,
								usage[myAddressPosition].Parent, inputData.Tick);
					// Ensure that the address isn't picked up on a second pass
					usage[myAddressPosition].Tick = inputData.Tick;
				}

				if (iterator == maxIterator)
				{
					iterator = baseIterator;
					offset = offset >> 1;
				}
				else
					iterator++;
				helpDereference(blocks, usage, parentSize, parentResidency, parents,
								dereferenceQueue, ref dereferenceRemaining, ref semaphor,
								inputData.Tick);
			}
			Grafting myGrafting = grafting[id];
			uint address = myGrafting.GraftAddress;
			// Seek out true address if the grafting address is just a set of coordinates
			if (myGrafting.Depth != byte.MaxValue)
			{
				address = findAddress(
					blocks, usage, ref childRequestId, childRequests, parentSize,
					parentResidency, parents, dereferenceQueue, ref dereferenceRemaining,
					ref semaphor, graftingAddresses, inputData, address, myGrafting.Depth);
				if (address == uint.MaxValue)
					return;
				if (blocks[address].Child != uint.MaxValue)
					dereference(blocks, usage, parentSize, parentResidency, parents,
								dereferenceQueue, ref dereferenceRemaining, ref semaphor, address,
								inputData.Tick);
				else
					helpDereference(blocks, usage, parentSize, parentResidency, parents,
									dereferenceQueue, ref dereferenceRemaining, ref semaphor,
									inputData.Tick);
			}
			else
				helpDereference(blocks, usage, parentSize, parentResidency, parents,
								dereferenceQueue, ref dereferenceRemaining, ref semaphor,
								inputData.Tick);

			uint3[] depthHeap = new uint3[64];
			uint blockAddress = holdingAddresses[myGrafting.GraftDataAddress] << 3;
			blocks[address].Child = blockAddress;
			//(Address in graft tree, address in block tree, superblock position)
			depthHeap[0] = new uint3(myGrafting.GraftDataAddress, blockAddress, 0);
			byte depth = 0;
			uint i = 0;
			while (depth >= 0)
			{
				while (depthHeap[depth].z < 8)
				{
					uint3 heapValue = depthHeap[depth];
					Block block = graftingBlocks[heapValue.x + heapValue.z];
					uint blockChild = block.Child;
					block.Child = uint.MaxValue;
					blocks[heapValue.y + heapValue.z] = block;
					depthHeap[depth].z++;
					if (blockChild != uint.MaxValue)
					{
						i++;
						blockAddress = holdingAddresses[i + myGrafting.GraftDataAddress] << 3;
						blocks[heapValue.y + heapValue.z].Child = blockAddress;
						usage[blockAddress].Parent = heapValue.y + heapValue.z;
						depth++;
						depthHeap[depth] = new uint3(blockChild, blockAddress, 0);
						break;
					}
				}

				if (depthHeap[depth].z == 8)
					depth--;
			}
		}
		public static void VoxelTrace(ushort[] bases, Block[] blocks,
						 Usage[] usage, ref uint childRequestId,
						 ChildRequest[] childRequests,
						 string outputImage,
						 TraceInput _input)
		{
			byte depth = 1;
			uint localAddress;
			uint x = get_global_id(0);
			uint y = get_global_id(1);
			Location previousLocation;
			Vector2i coord = new Vector2i((int)x, (int)y);
			// Used to navigate back up the tree
			uint[] depthHeap = new uint[64];
			WorkingData _data = setup(coord, ref _input);

			depthHeap[_data.BaseDepth + 1] = uint.MaxValue;
			if (!startTrace(ref _data))
			{
				writeBackgroundData(outputImage, ref _data);
				return;
			}

			// iterate over base chunks
			while (true)
			{
				// check if current base chunk contains geometry
				localAddress = powSum((byte)(depth - 1)) + baseLocation(depth, _data.Location);
				if (depth <= _data.BaseDepth && (bases[localAddress] >> (chunkPosition(depth, _data.Location) * 2) & 2) != 2)
				{
					// current chunk has no geometry, move to edge of chunk and go up a level if this is the edge of the block
					previousLocation = _data.Location;
					traverseChunk(depth, ref _data);
					if (leaving(ref _data))
					{
						writeBackgroundData(outputImage, ref _data);
						return;
					}
					depth = comparePositions(depth, previousLocation, ref _data);
				}
				else
				{
					if (depth < _data.BaseDepth)
						// still traversing base chunks
						depth++;
					else
					{
						// now traversing blocks
						if (depth == _data.BaseDepth)
							depth = (byte)(_data.BaseDepth + 2);

						// Loop over a block and its children
						while (depth > (_data.BaseDepth + 1))
						{
							if (depth == _data.BaseDepth + 2)
								localAddress = baseLocation(depth, _data.Location);
							else
								localAddress = blocks[depthHeap[depth - 1]].Child + chunkPosition((byte)(depth - 1), _data.Location);
							depthHeap[depth] = localAddress;

							// Update usage
							if (usage[localAddress >> 3].Tick < ushort.MaxValue - 1)
								usage[localAddress >> 3].Tick = _data.Tick;

							// Check if current block chunk contains geometry
							if (((blocks[localAddress].Chunk >> (chunkPosition(depth, _data.Location) * 2)) & 2) != 2)
							{
								previousLocation = _data.Location;
								traverseChunk(depth, ref _data);
								if (leaving(ref _data))
								{
									writeBackgroundData(outputImage, ref _data);
									return;
								}
								depth = comparePositions(depth, previousLocation, ref _data);
							}
							else
							{
								// C value is too diffuse to use
								if (_data.ConeDepth < (_data.BaseDepth + 2))
								{
									depth = (byte)(_data.BaseDepth + 2);
									if (saveVoxelTrace(blocks[depthHeap[depth]], ref _data))
									{
										writeData(outputImage, ref _data);
										return;
									}
								}

								// C value requires me to go up a level
								else if (_data.ConeDepth < depth)
									break;

								// No additional data could be found at child depth
								else if (blocks[localAddress].Child == uint.MaxValue)
								{
									requestChild(localAddress, depth, ref childRequestId,
												 childRequests, _data.MaxChildRequestId,
												 _data.Tick, 1, _data.Location);
									if (saveVoxelTrace(blocks[localAddress], ref _data))
									{
										writeData(outputImage, ref _data);
										return;
									}
								}

								// Navigate to child
								else if (_data.ConeDepth > (depth + 1))
									depth++;

								// Resolve the colour of this voxel
								else
								{
									if (saveVoxelTrace(average(localAddress, blocks, ref _data), ref _data))
									{
										writeData(outputImage, ref _data);
										return;
									}
								}
							}
						}
						depth = _data.BaseDepth;
					}
				}
			}
		}
	}
}
