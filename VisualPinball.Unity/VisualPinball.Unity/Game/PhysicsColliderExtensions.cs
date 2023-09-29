﻿// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace VisualPinball.Unity
{
	public static partial class PhysicsColliderExtensions
	{
		internal static int GetId(this BlobAssetReference<ColliderBlob> colliders, int index) 
			=> colliders.Value.Colliders[index].Value.Id;

		internal static ColliderType GetType(this BlobAssetReference<ColliderBlob> colliders, int index) 
			=> colliders.Value.Colliders[index].Value.Type;

		internal static float GetFriction(this BlobAssetReference<ColliderBlob> colliders, int index) 
			=> colliders.Value.Colliders[index].Value.Material.Friction;

		internal static Aabb GetAabb(this BlobAssetReference<ColliderBlob> colliders, int index) 
			=> colliders.Value.Colliders[index].Value.Bounds().Aabb;

		internal static unsafe ref PlaneCollider GetPlaneCollider(this ref BlobAssetReference<ColliderBlob> colliders, int index)
		{
			ref var coll = ref colliders.Value.Colliders[index].Value;
			fixed (Collider* cPtr = &coll) {
				var planeCollider = (PlaneCollider*) cPtr;
				return ref UnsafeUtility.AsRef<PlaneCollider>(planeCollider);
			}
		}
		
		internal static unsafe ref LineCollider GetLineCollider(this ref BlobAssetReference<ColliderBlob> colliders, int index)
		{
			ref var coll = ref colliders.Value.Colliders[index].Value;
			fixed (Collider* cPtr = &coll) {
				var lineCollider = (LineCollider*) cPtr;
				return ref UnsafeUtility.AsRef<LineCollider>(lineCollider);
			}
		}
	}
}