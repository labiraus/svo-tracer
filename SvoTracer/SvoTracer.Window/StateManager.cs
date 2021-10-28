using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SvoTracer.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SvoTracer.Window
{
	public class StateManager
	{
		public TraceInputData TraceInput = new()
		{
			Origin = new(0.5f, 0.5f, -2f),
			Facing = new(0, (float)Math.PI / 2f, 0),
			FoV = new Vector2((float)Math.PI / 4f, (float)Math.PI / 4f),
			DoF = new Vector2(0, 0.169f),
			MaxOpacity = 200,
			MaxChildRequestId = 6000
		};
		public UpdateInputData UpdateInput = new()
		{
			MaxChildRequestId = 6000,
			Offset = uint.MaxValue / 4,
		};
		public ushort Tick { get; set; } = 0;
		MouseState previousMouseState;

		public StateManager(MouseState mouseState)
		{
			this.previousMouseState = mouseState;
		}

		public void ReadInput(MouseState mouseState, KeyboardState keyboardState)
		{
			if (mouseState.IsButtonDown(MouseButton.Left) && previousMouseState.IsButtonDown(MouseButton.Left))
			{
				var facing = TraceInput.Facing;
				facing.X -= (mouseState.X - previousMouseState.X) / 1000.0f;
				facing.Y += (mouseState.Y - previousMouseState.Y) / 1000.0f;

				if (facing.Y > Math.PI)
					facing.Y = (float)Math.PI;
				else if (facing.Y < -Math.PI)
					facing.Y = -(float)Math.PI;
				if (facing.X > Math.PI)
					facing.X -= (float)Math.PI * 2;
				else if (facing.X < -Math.PI)
					facing.X += (float)Math.PI * 2;
				TraceInput.Facing = facing;
			}

			previousMouseState = mouseState;
			var origin = TraceInput.Origin;
			if (keyboardState.IsKeyDown(Keys.Space))
				origin.Z -= 0.005f;
			if (keyboardState.IsKeyDown(Keys.C))
				origin.Z += 0.005f;
			if (keyboardState.IsKeyDown(Keys.W) && !keyboardState.IsKeyDown(Keys.S))
				origin.Y -= 0.005f;
			if (keyboardState.IsKeyDown(Keys.S) && !keyboardState.IsKeyDown(Keys.W))
				origin.Y += 0.005f;
			if (keyboardState.IsKeyDown(Keys.D) && !keyboardState.IsKeyDown(Keys.A))
				origin.X -= 0.005f;
			if (keyboardState.IsKeyDown(Keys.A) && !keyboardState.IsKeyDown(Keys.D))
				origin.X += 0.005f;
			TraceInput.Origin = origin;
		}

		public void UpdateScreenSize(Vector2i size)
		{
			TraceInput.ScreenSize = size;
			var fov = TraceInput.FoV;
			fov[0] = (float)size.X / (float)size.Y * (float)Math.PI / 4.0f;
			TraceInput.FoV = fov;
		}

		public void IncrementTick()
		{
			if (Tick < ushort.MaxValue - 2)
				Tick += 1;
			else
				Tick = 1;

			TraceInput.Tick = Tick;
			UpdateInput.Tick = Tick;
		}
	}
}
