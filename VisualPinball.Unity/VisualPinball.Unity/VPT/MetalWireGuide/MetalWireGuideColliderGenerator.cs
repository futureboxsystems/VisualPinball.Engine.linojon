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

using System.Collections.Generic;
using Unity.Collections;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.MetalWireGuide;

namespace VisualPinball.Unity
{
	public class MetalWireGuideColliderGenerator
	{
		private readonly IApiColliderGenerator _api;
		private readonly MetalWireGuideMeshGenerator _meshGenerator;

		public MetalWireGuideColliderGenerator(MetalWireGuideApi metalWireGuideApi, MetalWireGuideMeshGenerator meshGenerator)
		{
			_api = metalWireGuideApi;
			_meshGenerator = meshGenerator;
		}

		internal void GenerateColliders(float playfieldHeight, float hitHeight, float bendradius, int detailLevel, ref ColliderReference colliders, float margin)
		{
			var mesh = _meshGenerator.GetTransformedMesh(playfieldHeight, hitHeight, detailLevel, bendradius, 6, true, margin); //!! adapt hacky code in the function if changing the "6" here
			var addedEdges = EdgeSet.Get(Allocator.TempJob);

			// add collision triangles and edges
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rg0 = mesh.Vertices[mesh.Indices[i]].ToUnityFloat3();
				var rg1 = mesh.Vertices[mesh.Indices[i + 2]].ToUnityFloat3();
				var rg2 = mesh.Vertices[mesh.Indices[i + 1]].ToUnityFloat3();

				colliders.Add(new TriangleCollider(rg0, rg1, rg2, _api.GetColliderInfo()));

				GenerateHitEdge(mesh, ref addedEdges, mesh.Indices[i], mesh.Indices[i + 2], ref colliders);
				GenerateHitEdge(mesh, ref addedEdges, mesh.Indices[i + 2], mesh.Indices[i + 1], ref colliders);
				GenerateHitEdge(mesh, ref addedEdges, mesh.Indices[i + 1], mesh.Indices[i], ref colliders);
			}

			// add collision vertices
			foreach (var mv in mesh.Vertices) {
				colliders.Add(new PointCollider(mv.ToUnityFloat3(), _api.GetColliderInfo()));
			}

			addedEdges.Dispose();
		}

		private void GenerateHitEdge(Mesh mesh, ref EdgeSet addedEdges, int i, int j,
			ref ColliderReference colliders)
		{
			if (addedEdges.ShouldAddHitEdge(i, j)) {
				var v1 = mesh.Vertices[i].ToUnityFloat3();
				var v2 = mesh.Vertices[j].ToUnityFloat3();
				colliders.Add(new Line3DCollider(v1, v2, _api.GetColliderInfo()));
			}
		}
	}
}
