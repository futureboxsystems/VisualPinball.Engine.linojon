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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.VisualPinball.Unity.Game;
using VisualPinballUnity;
using Debug = UnityEngine.Debug;

namespace VisualPinball.Unity
{
	public class PhysicsEngine : MonoBehaviour
	{
		[NonSerialized] private NativeArray<PhysicsState> _physicsState;
		[NonSerialized] private NativeOctree<int> _octree;
		[NonSerialized] private NativeList<BallData> _balls;
		[NonSerialized] private NativeQueue<EventData> _eventQueue;
		[NonSerialized] private BlobAssetReference<ColliderBlob> _colliders;

		[NonSerialized] private readonly Dictionary<int, PhysicsBall> _ballLookup = new();

		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);
		
		private void Start()
		{
			// init state
			_physicsState = new NativeArray<PhysicsState>(1, Allocator.Persistent);
			_physicsState[0] = new PhysicsState(NowUsec, GetComponent<Player>());

			// create static octree
			var sw = Stopwatch.StartNew();
			var colliderItems = GetComponentsInChildren<ICollidableComponent>();

			Debug.Log($"Found {colliderItems.Length} collider items.");
			var managedColliders = new List<ICollider>();
			foreach (var colliderItem in colliderItems) {
				colliderItem.GetColliders(managedColliders);
			}
			
			// allocate colliders
			var allocateColliderJob = new ColliderAllocationJob(managedColliders);
			allocateColliderJob.Run();
			_colliders = allocateColliderJob.BlobAsset[0];
			allocateColliderJob.Dispose();
			
			// create octree
			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			var playfieldBounds = GetComponentInChildren<PlayfieldComponent>().Bounds;
			_octree = new NativeOctree<int>(playfieldBounds, 32, 10, Allocator.Persistent);
			
			sw.Restart();
			var populateJob = new PopulatePhysicsJob {
				Colliders = _colliders,
				Octree = _octree, 
			};
			populateJob.Run();
			_octree = populateJob.Octree;
			Debug.Log($"Octree of {_colliders.Value.Colliders.Length} constructed (colliders: {elapsedMs}ms, tree: {sw.Elapsed.TotalMilliseconds}ms).");
			
			// get balls
			var balls = GetComponentsInChildren<PhysicsBall>();
			_balls = new NativeList<BallData>(balls.Length, Allocator.Persistent);
			foreach (var ball in balls) {
				_balls.Add(ball.Data);
				_ballLookup[ball.Id] = ball;
			}
			
			_eventQueue = new NativeQueue<EventData>(Allocator.Persistent);
		}

		private void Update()
		{
			var events = _eventQueue.AsParallelWriter();
			var updatePhysics = new UpdatePhysicsJob {
				InitialTimeUsec = NowUsec,
				PhysicsState = _physicsState,
				Octree = _octree,
				Colliders = _colliders,
				Balls = _balls,
				Events = events,
			};
			
			updatePhysics.Run();

			_balls = updatePhysics.Balls;
			_physicsState = updatePhysics.PhysicsState;

			foreach (var ballData in _balls) {
				var ball = _ballLookup[ballData.Id];
				BallMovementPhysics.Move(ballData, _ballLookup[ball.Id].transform);
			}
		}
		
		private void OnDestroy()
		{
			_physicsState.Dispose();
			_eventQueue.Dispose();
			_balls.Dispose();
			_colliders.Dispose();
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	internal struct PopulatePhysicsJob : IJob
	{
		[ReadOnly]
		public BlobAssetReference<ColliderBlob> Colliders;
		public NativeOctree<int> Octree;
		
		public void Execute()
		{
			for (var i = 0; i < Colliders.Value.Colliders.Length; i++) {
				Octree.Insert(Colliders.GetId(i), Colliders.GetAabb(i));
			}
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	internal struct UpdatePhysicsJob : IJob
	{
		[ReadOnly] 
		public ulong InitialTimeUsec;
		public NativeArray<PhysicsState> PhysicsState;
		public NativeOctree<int> Octree;
		public BlobAssetReference<ColliderBlob> Colliders;
		public NativeList<BallData> Balls;
		public NativeQueue<EventData>.ParallelWriter Events;

		public void Execute()
		{
			var n = 0;
			var state = PhysicsState[0];
			var cycle = new PhysicsCycle(Allocator.Temp);
			
			
			while (state.CurPhysicsFrameTime < InitialTimeUsec)
			{
				var timeMsec = (uint)((state.CurPhysicsFrameTime - state.StartTimeUsec) / 1000);
				var physicsDiffTime = (float)((state.NextPhysicsFrameTime - state.CurPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime));

				// update velocities
				for (var i = 0; i < Balls.Length; i++) {
					var ball = Balls[i];
					BallVelocityPhysics.UpdateVelocities(state.Gravity, ref ball);
					Balls[i] = ball;
				}
				
				// simulate cycle
				cycle.Simulate(physicsDiffTime, ref state, in Octree, ref Colliders, ref Balls, ref Events);
				
				state.CurPhysicsFrameTime = state.NextPhysicsFrameTime;
				state.NextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;

				n++;
			}

			PhysicsState[0] = state;
			cycle.Dispose();
		}
	}
}
