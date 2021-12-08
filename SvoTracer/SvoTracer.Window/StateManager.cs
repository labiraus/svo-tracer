using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SvoTracer.Domain.Models;
using System;
using System.Diagnostics;

namespace SvoTracer.Window
{
	public class StateManager
	{
		private const float flySpeed = 0.001f;
		private const float turnSpeed = 20000.0f;
		public PrimeTraceData TraceInput;
		public UpdateInputData UpdateInput = new()
		{
			MaxChildRequestId = 6000,
			Offset = uint.MaxValue / 4,
		};
		public Vector3 FacingEuler = new();
		public Stopwatch timer = new();
		public ushort Tick { get; set; } = 0;

		public StateManager(PrimeTraceData input)
		{
			TraceInput = input;
		}

		public void ReadInput(MouseState mouseState, KeyboardState keyboardState)
		{
			timer.Stop();
			float turn = turnSpeed/ timer.ElapsedMilliseconds;
			float fly = timer.ElapsedMilliseconds * flySpeed;
			if (mouseState.IsButtonDown(MouseButton.Left))
			{
				FacingEuler.Y += (mouseState.Delta.Y) / turn;
				FacingEuler.Z -= (mouseState.Delta.X) / turn;

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
				relativeMovement.X = fly;
			if (keyboardState.IsKeyDown(Keys.S) && !keyboardState.IsKeyDown(Keys.W))
				relativeMovement.X = -fly;
			if (keyboardState.IsKeyDown(Keys.D) && !keyboardState.IsKeyDown(Keys.A))
				relativeMovement.Y = fly;
			if (keyboardState.IsKeyDown(Keys.A) && !keyboardState.IsKeyDown(Keys.D))
				relativeMovement.Y = -fly;
			if (keyboardState.IsKeyDown(Keys.Space))
				relativeMovement.Z = -fly;
			if (keyboardState.IsKeyDown(Keys.C))
				relativeMovement.Z = fly;

			if (relativeMovement != Vector3.Zero)
				TraceInput.Origin += Vector3.TransformRow(relativeMovement, TraceInput.Facing);
			timer.Reset();
			timer.Start();
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
