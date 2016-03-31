using UnityEngine;

using System.Collections;
using System.Collections.Generic;

namespace mattatz.MeshSlicing {

	public class BoundingBoxSlicing {

		// slicing algorithm described in GPU Gems 39
		// http://http.developer.nvidia.com/GPUGems/gpugems_ch39.html
        public static List<SlicingData> Slice (float slicing, Transform view, Bounds bounds) {
			slicing = Mathf.Clamp(slicing, 0.001f, 0.5f);

            var viewMatrix = MathHelper.MakeViewMatrix(view.right, view.up, view.forward, view.position);

            int closest, farthest;

            // bounding box corners
			Vector3[] corners = MeshHelper.GetBoundingBoxCorners(bounds);
            MeshHelper.FindMinMaxCorners(corners, viewMatrix, out closest, out farthest);

            var origin = corners[closest];
            var to = corners[farthest];

            Vector3 dir = view.forward;
            float distance = Vector3.Project((to - origin), dir.normalized).magnitude;

			List<SlicingData> slices = new List<SlicingData>();

            for(float t = 0f, n = 1f + slicing; t < n; t += slicing) {
                Vector3 p = origin + dir * distance * t;

                var points = FindPlaneCorners(closest, dir.normalized, distance * t, corners, GetNeighbors(), new List<Vector3>());
                points = MathHelper.ClockWise(p, viewMatrix, points);
				slices.Add(new SlicingData(viewMatrix, dir, points));
            }

			return slices;
        }

        /*
         * slice bounding box
         */
        static List<Vector3> FindPlaneCorners (int from, Vector3 dir, float distance, Vector3[] corners, List<List<int>> neighbors, List<Vector3> points) {
            var v0 = corners[from];

            neighbors.ForEach(neighbor => {
                neighbor.Remove(from);
            });

            List<KeyValuePair<int, float>> next = new List<KeyValuePair<int, float>>();

            int n = neighbors[from].Count;
            for(int i = 0; i < n; i++) {
                if(neighbors[from].Count <= 0) break;

                int index = neighbors[from][i];

                var v1 = corners[index];
                var edge = v1 - v0;

                var projection = Vector3.Project(edge, dir);

                var d = projection.magnitude;
                if(d > distance) {
                    // solve X for : Vector3.Project(X * edge.normalize, dir.normalize) = dir * distance;
                    float dot0 = Vector3.Dot(edge.normalized, dir);
                    float dot1 = Vector3.Dot(dir, dir);
                    var len = (dir * distance).x / ((dot0 / dot1) * dir).x;
                    points.Add(v0 + edge.normalized * len);
                } else {
                    next.Add(new KeyValuePair<int, float>(index, distance - d));
                }

            }

            neighbors[from].Clear();

            next.ForEach(pair => {
                neighbors.ForEach(neighbor => {
                    neighbor.Remove(pair.Key);
                });
            });

            next.ForEach(pair => {
                FindPlaneCorners(pair.Key, dir, pair.Value, corners, neighbors, points);
            });

            return points;
        }

        static List<List<int>> GetNeighbors () {
            List<List<int>> neighbors = new List<List<int>>();
            neighbors.Add(new List<int>() {1, 2, 4});
            neighbors.Add(new List<int>() {0, 3, 5});
            neighbors.Add(new List<int>() {0, 3, 6});
            neighbors.Add(new List<int>() {1, 2, 7});
            neighbors.Add(new List<int>() {0, 5, 6});
            neighbors.Add(new List<int>() {1, 4, 7});
            neighbors.Add(new List<int>() {2, 4, 7});
            neighbors.Add(new List<int>() {3, 5, 6});
            return neighbors;
        }

	}

}

