// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;

namespace Xenko.VirtualReality
{
    public abstract class VRDevice : IDisposable
    {
        public GameBase Game { get; internal set; }

        protected VRDevice()
        {
            BodyScaling = 1.0f;
        }

        public abstract Size2 ActualRenderFrameSize { get; }

        public abstract float RenderFrameScaling { get; set; }

        public abstract DeviceState State { get; }

        public abstract Vector3 HeadPosition { get; }

        public abstract Quaternion HeadRotation { get; }

        public abstract Vector3 HeadLinearVelocity { get; }

        public abstract Vector3 HeadAngularVelocity { get; }

        public abstract TouchController LeftHand { get; }

        public abstract TouchController RightHand { get; }

        public abstract ulong PoseCount { get; }

        public VRApi VRApi { get; protected set; }

        /// <summary>
        /// Allows you to scale your whole body, effectively it will change the size of the player in respect to the world, turning it into a giant or a tiny ant.
        /// </summary>
        /// <remarks>This will reduce the near clip plane of the cameras, it might induce depth issues.</remarks>
        public float BodyScaling { get; set; }

        public abstract bool CanInitialize { get; }

        public abstract void Enable(GraphicsDevice device, GraphicsDeviceManager graphicsDeviceManager, VRDeviceSystem.MIRROR_OPTION requireMirror);

        public virtual void Recenter()
        {
        }

        public virtual void SetTrackingSpace(TrackingSpace space)
        {
        }

        public abstract void ReadEyeParameters(Eyes eye, float near, float far, ref Vector3 cameraPosition, ref Matrix cameraRotation, bool ignoreHeadRotation, bool ignoreHeadPosition, out Matrix view, out Matrix projection);

        public abstract void Commit(CommandList commandList, Texture renderFrame);

        public virtual void Flush() { }

        public virtual void Dispose()
        {           
        }

        public abstract void Update(GameTime gameTime);

        public abstract void UpdatePositions(GameTime gameTime);

        public abstract void Draw(GameTime gameTime);
    }
}
