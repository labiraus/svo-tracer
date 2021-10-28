using OpenTK.Mathematics;
using SvoTracer.Domain.Models;
using System;

namespace SvoTracer.Kernel
{
	public class KernelMirror
	{

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

		static float uLongToFloat(ulong x)
		{
			return (float)x / (float)ulong.MaxValue;
		}

		static ulong roundUlong(ulong value, byte depth, bool roundUp)
		{
			if (roundUp)
				return ((value & (ulong.MaxValue - (ulong.MaxValue >> (depth + 1)))) + (ulong.MaxValue - (ulong.MaxValue >> 1) >> depth)) - value;

			ulong output = value - ((value & (ulong.MaxValue - (ulong.MaxValue >> depth + 1))) - 1);

			if (output >= value)
				return value;

			return output;


		}

		public static uint powSum(byte depth)
		{
			uint output = 0;
			for (int i = 1; i <= depth; i++)
				output += (uint)(1 << (3 * i));
			return output;
		}

		static byte chunk(byte depth, ref WorkingData _data)
		{
			return (byte)((_data.Location.X >> (64 - depth - 1) & 1) +
				((_data.Location.Y >> (64 - depth - 1) & 1) << 1) +
				((_data.Location.Z >> (64 - depth - 1) & 1) << 2));
		}

		static uint baseLocation(byte depth, ref WorkingData _data)
		{
			uint output = 0;
			for (byte i = 0; i < depth; i++)
				output = (output << 3) + chunk(i, ref _data);

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

		static float coneLevel(ref WorkingData _data)
		{
			return 11;
			return -(float)Math.Log(coneSize(Math.Abs(new Vector3(_data.Origin.X - uLongToFloat(_data.Location.X),
				_data.Origin.Y - uLongToFloat(_data.Location.Y),
				_data.Origin.Z - uLongToFloat(_data.Location.Z)).Length), ref _data), 2);
		}

		static Block background(ref WorkingData _data)
		{
			return new Block()
			{
				Child = uint.MaxValue,
				Chunk = 0,
				Data = new BlockData()
				{
					NormalPitch = 0,
					NormalYaw = 0,
					ColourR = 0,
					ColourB = 0,
					ColourG = 0,
					Opacity = byte.MaxValue,
					Properties = 0
				}
			};
		}

		static void writeData(string image, ref WorkingData _data)
		{
			Console.Write(_data.Colour[0] > 0 ? 1 : 0);
		}

		/// <summary>
		/// Needs skybox and somewhere to set colour
		/// </summary>
		/// <param name="_tree"></param>
		/// <param name="data"></param>
		/// <param name="address"></param>
		static bool spawnRays(Block block, ref WorkingData _data)
		{
			_data.Colour[0] = block.Data.ColourR;
			_data.Colour[1] = block.Data.ColourG;
			_data.Colour[2] = block.Data.ColourB;
			return true;
		}

		static Block average(uint address, Block[] blocks, float C, ref WorkingData _data)
		{
			//Average like heck            
			return blocks[address];
		}

		static void requestChild(uint address, byte depth, ref WorkingData workingData)
		{

		}

		static void updateUsage(uint address)
		{

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

		/// <summary>
		/// Moves to the nearest neighboring chunk along the Direction vector
		/// </summary>
		/// <param name="data"></param>
		/// <param name="depth"></param>
		/// <param name="position"></param>
		/// <returns>New chunk position or 8 for outside of block</returns>
		static bool traverseChunk(byte depth, byte position, ref WorkingData _data)
		{
			ulong dx = roundUlong(_data.Location.X, depth, _data.DirectionSignX);
			ulong dy = roundUlong(_data.Location.Y, depth, _data.DirectionSignY);
			ulong dz = roundUlong(_data.Location.Z, depth, _data.DirectionSignZ);

			float ax = Math.Abs(dx * _data.InvDirection.X);
			float ay = Math.Abs(dy * _data.InvDirection.Y);
			float az = Math.Abs(dz * _data.InvDirection.Z);
			bool success = true;

			if (ax <= ay && ax <= az)
			{
				float udx = uLongToFloat(dx);
				dy = floatToULong(_data.Direction.Y * _data.InvDirection.X * udx);
				dz = floatToULong(_data.Direction.Z * _data.InvDirection.X * udx);

				if ((_data.DirectionSignX && (position & 1) == 1) || (!_data.DirectionSignX && (position & 1) == 0))
					success = false;
			}
			else if (ay <= ax && ay <= az)
			{
				float udy = uLongToFloat(dy);
				dx = floatToULong(_data.Direction.X * _data.InvDirection.Y * udy);
				dz = floatToULong(_data.Direction.Z * _data.InvDirection.Y * udy);

				if ((_data.DirectionSignY && (position >> 1 & 1) == 1) || (!_data.DirectionSignY && (position >> 1 & 1) == 0))
					success = false;
			}
			else
			{
				float udz = uLongToFloat(dz);
				dx = floatToULong(_data.Direction.X * _data.InvDirection.Z * udz);
				dy = floatToULong(_data.Direction.Y * _data.InvDirection.Z * udz);

				if ((_data.DirectionSignZ && (position >> 2 & 1) == 1) || (!_data.DirectionSignZ && (position >> 2 & 1) == 0))
					success = false;
			}
			var location = _data.Location;
			if (_data.DirectionSignX)
				location.X = location.X + dx;
			else
				location.X = location.X - dx;

			if (_data.DirectionSignY)
				location.Y = location.Y + dy;
			else
				location.Y = location.Y - dy;

			if (_data.DirectionSignZ)
				location.Z = location.Z + dz;
			else
				location.Z = location.Z - dz;


			if (_data.DirectionSignX && location.X == 0)
			{
				location.X = ulong.MaxValue;
				return false;
			}
			else if (!_data.DirectionSignX && location.X == ulong.MaxValue)
			{
				location.X = 0;
				return false;
			}
			else if (_data.DirectionSignY && location.Y == 0)
			{
				location.Y = ulong.MaxValue;
				return false;
			}
			else if (!_data.DirectionSignY && location.Y == ulong.MaxValue)
			{
				location.Y = 0;
				return false;
			}
			else if (_data.DirectionSignZ && location.Z == 0)
			{
				location.Z = ulong.MaxValue;
				return false;
			}
			else if (!_data.DirectionSignZ && location.Z == ulong.MaxValue)
			{
				location.Z = 0;
				return false;
			}
			_data.Location = location;
			return success;
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
		/// <param name="coord">Global position - thread ID</param>
		/// <param name="ANG">Direction faced</param>
		/// <param name="EYE">Horizonal/vertical FoV angle</param>
		/// <param name="SCR">Screen size</param>
		static WorkingData setup(uint[] coord, TraceInputData _input)
		{
			WorkingData data = new WorkingData();
			data.Coord = new Vector2i((int)coord[0], (int)coord[1]);
			//Horizontal and vertical offset angles float h and v
			var h = _input.FoV[0];
			h *= (float)((_input.ScreenSize[0] / 2) - coord[0]);
			h /= (float)_input.ScreenSize[0];
			var v = _input.FoV[1];
			v *= (float)((_input.ScreenSize[1] / 2) - coord[1]);
			v /= (float)_input.ScreenSize[1];

			float su = (float)Math.Sin(_input.Facing.Z);
			float cu = (float)Math.Cos(_input.Facing.Z);
			float sv = (float)Math.Sin(_input.Facing.Y);
			float cv = (float)Math.Cos(_input.Facing.Y);
			float sw = (float)Math.Sin(_input.Facing.X);
			float cw = (float)Math.Cos(_input.Facing.X);
			//float su2 = 0;
			//float cu2 = 1;
			float sv2 = (float)Math.Sin(v);
			float cv2 = (float)Math.Cos(v);
			float sw2 = (float)Math.Sin(h);
			float cw2 = (float)Math.Cos(h);

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
			//float BM12 = su2 * sv2 * cw2 - cu2 * sw2;
			//float BM13 = su2 * sw2 + cu2 * sv2 * cw2;
			float BM21 = cv2 * sw2;
			//float BM22 = cu2 * cw2 + su2 * sv2 * sw2;
			//float BM23 = cu2 * sv2 * sw2 - su2 * cw2;
			float BM31 = -sv2;
			//float BM32 = su2 * cv2;
			//float BM33 = cu2 * cv2;

			float CM11 = AM11 * BM11 + AM12 * BM21 + AM13 * BM31;
			//float CM12 = AM11 * BM12 + AM12 * BM22 + AM13 * BM32;
			//float CM13 = AM11 * BM13 + AM12 * BM23 + AM13 * BM33;
			float CM21 = AM21 * BM11 + AM22 * BM21 + AM23 * BM31;
			//float CM22 = AM21 * BM12 + AM22 * BM22 + AM23 * BM32;
			//float CM23 = AM21 * BM13 + AM22 * BM23 + AM23 * BM33;
			float CM31 = AM31 * BM11 + AM32 * BM21 + AM33 * BM31;
			//float CM32 = AM31 * BM12 + AM32 * BM22 + AM33 * BM32;
			//float CM33 = AM31 * BM13 + AM32 * BM23 + AM33 * BM33;

			float yaw = (float)Math.Atan2(CM21, CM11);
			float pitch = -(float)Math.Asin(CM31);

			//Unit vector of direction float3 DIR
			data.Direction = new Vector3(
				(float)Math.Cos(yaw) * (float)Math.Cos(pitch),
				(float)Math.Sin(yaw) * (float)Math.Cos(pitch),
				(float)Math.Sin(pitch));

			data.InvDirection = new Vector3(1 / data.Direction.X, 1 / data.Direction.Y, 1 / data.Direction.Z);
			data.DirectionSignX = data.Direction.X >= 0;
			data.DirectionSignY = data.Direction.Y >= 0;
			data.DirectionSignZ = data.Direction.Z >= 0;
			data.Origin = _input.Origin;
			data.N = _input.N;
			data.DoF = _input.DoF;
			data.MaxOpacity = _input.MaxOpacity;
			data.PixelFoV = _input.FoV[0] / _input.ScreenSize[0];
			return data;
		}

		public static void VoxelTrace(string outputImage, TraceInputData _input, ushort[] bases, Block[] blocks, uint[] coord)
		{
			byte depth = 1;
			bool inside = true;
			byte chunkPosition;
			float C = -1;
			uint offset;
			uint address = 0;
			//uint x = get_global_id(0);
			//uint y = get_global_id(1);
			//uint[] coord = new uint[] { x, y };
			uint[] depthHeap = new uint[64];
			byte baseChunk = 0;
			WorkingData _data = setup(coord, _input);
			if (startTrace(ref _data))
			{
				while (depth > 0 && !leaving(ref _data))
				{
					inside = true;
					while (inside && !leaving(ref _data))
					{
						chunkPosition = chunk(depth, ref _data);
						offset = powSum((byte)(depth - 1));
						address = baseLocation(depth, ref _data);
						if ((bases[offset + address] >> (chunkPosition * 2) & 2) == 2)
						{
							if (depth == _data.N)
							{
								depth += 2;
								depthHeap[depth] = baseLocation(depth, ref _data);
								baseChunk = chunkPosition;
								while (depth > _data.N + 1 && !leaving(ref _data))
								{
									C = -1;
									updateUsage(depthHeap[depth]);
									inside = true;
									while (inside && !leaving(ref _data))
									{
										chunkPosition = chunk(depth, ref _data);
										uint localAddress = depthHeap[depth];

										if ((blocks[localAddress].Chunk >> (chunkPosition * 2) & 2) == 2)
										{
											if (C == -1)
												C = coneLevel(ref _data);

											depthHeap[depth + 1] = blocks[localAddress].Child + chunkPosition;
											//C value is too diffuse to use
											if (C < _data.N + 2)
											{
												depth = (byte)(_data.N + 2);
												if (spawnRays(average(localAddress, blocks, C, ref _data), ref _data))
												{
													writeData(outputImage, ref _data);
													return;
												}
											}
											//C value requires me to go up a level
											else if (C < depth)
											{
												inside = false;
											}
											//No additional data could be found at child depth
											else if (blocks[localAddress].Child == uint.MaxValue)
											{
												requestChild(localAddress, depth, ref _data);
												if (spawnRays(blocks[localAddress], ref _data))
												{
													writeData(outputImage, ref _data);
													return;
												}
											}
											//Navigate to child
											else if (C > (depth + 1))
											{
												depth++;
											}
											//Resolve the colour of this voxel
											else if (depth <= C && C <= (depth + 1))
											{
												if (spawnRays(blocks[localAddress], ref _data))
												{
													writeData(outputImage, ref _data);
													return;
												}
											}
										}
										else
										{
											inside = traverseChunk(depth, chunkPosition, ref _data);
										}
									}
									//If navigating to the N+1 level, first check that you aren't within the same base chunk
									if (depth == _data.N + 2 && baseChunk == chunk((byte)(_data.N + 1), ref _data))
									{
										depthHeap[depth] = baseLocation(depth, ref _data);
									}
									else
									{
										depth--;
									}
									chunkPosition = chunk(depth, ref _data);
								}
								depth = _data.N;
							}
							else
							{
								depth++;
							}
						}
						else
						{
							inside = traverseChunk(depth, chunkPosition, ref _data);
						}
					}
					if (depth != 1)
					{
						depth--;
					}
				}
			}
			spawnRays(background(ref _data), ref _data);
			writeData(outputImage, ref _data);
		}
	}
}
