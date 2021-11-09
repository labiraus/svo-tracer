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
		public TraceInputData TraceInput;
		public UpdateInputData UpdateInput = new()
		{
			MaxChildRequestId = 6000,
			Offset = uint.MaxValue / 4,
		};
		public Vector3 FacingEuler = new();
		public ushort Tick { get; set; } = 0;

		public StateManager(TraceInputData input)
		{
			TraceInput = input;
		}

		public void ReadInput(MouseState mouseState, KeyboardState keyboardState)
		{
			if (mouseState.IsButtonDown(MouseButton.Left))
			{
				FacingEuler.Y += (mouseState.Delta.Y) / 1000.0f;
				FacingEuler.Z -= (mouseState.Delta.X) / 1000.0f;

				if (FacingEuler.X > Math.PI)
					FacingEuler.X -= (float)Math.PI * 2;
				else if (FacingEuler.X < -Math.PI)
					FacingEuler.X += (float)Math.PI * 2;

				if (FacingEuler.Y > Math.PI)
					FacingEuler.Y = (float)Math.PI;
				else if (FacingEuler.Y < -Math.PI)
					FacingEuler.Y = -(float)Math.PI;

				if (FacingEuler.Z > Math.PI)
					FacingEuler.Z -= (float)Math.PI * 2;
				else if (FacingEuler.Z < -Math.PI)
					FacingEuler.Z += (float)Math.PI * 2;

				TraceInput.Facing = Matrix3.CreateRotationY(FacingEuler.Y) * Matrix3.CreateRotationZ(FacingEuler.Z);
			}


			var relativeMovement = Vector3.Zero;
			if (keyboardState.IsKeyDown(Keys.W) && !keyboardState.IsKeyDown(Keys.S))
				relativeMovement.X = 0.005f;
			if (keyboardState.IsKeyDown(Keys.S) && !keyboardState.IsKeyDown(Keys.W))
				relativeMovement.X = -0.005f;
			if (keyboardState.IsKeyDown(Keys.D) && !keyboardState.IsKeyDown(Keys.A))
				relativeMovement.Y = 0.005f;
			if (keyboardState.IsKeyDown(Keys.A) && !keyboardState.IsKeyDown(Keys.D))
				relativeMovement.Y = -0.005f;
			if (keyboardState.IsKeyDown(Keys.Space))
				relativeMovement.Z = -0.005f;
			if (keyboardState.IsKeyDown(Keys.C))
				relativeMovement.Z = 0.005f;

			if (relativeMovement != Vector3.Zero)
				TraceInput.Origin += Vector3.TransformRow(relativeMovement, TraceInput.Facing);
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
