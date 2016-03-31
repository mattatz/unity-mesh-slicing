using UnityEngine;

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace mattatz.MeshSlicing {

	public class MathHelper {

		public static Matrix4x4 MakeViewMatrix(Vector3 right, Vector3 up, Vector3 forward, Vector3 position) {
			var mat = new Matrix4x4();

			mat[0, 0] = right.x;
			mat[1, 0] = up.x;
			mat[2, 0] = forward.x;
			mat[3, 0] = 0f;

			mat[0, 1] = right.y;
			mat[1, 1] = up.y;
			mat[2, 1] = forward.y;
			mat[3, 1] = 0f;

			mat[0, 2] = right.z;
			mat[1, 2] = up.z;
			mat[2, 2] = forward.z;
			mat[3, 2] = 0f;

			mat[0, 3] = -Vector3.Dot(right, position);
			mat[1, 3] = -Vector3.Dot(up, position);
			mat[2, 3] = -Vector3.Dot(forward, position);
			mat[3, 3] = 1f;

			return mat;
		}

		// angle p2 from p1
		public static float GetAim(Vector2 p1, Vector2 p2) {
			float dx = p2.x - p1.x;
			float dy = p2.y - p1.y;
			return Mathf.Atan2(dy, dx);
		}

		/*
		 * Sort List<Vector3> by angle from center
		 */
		public static List<Vector3> ClockWise (Vector3 center, Matrix4x4 viewMatrix, List<Vector3> points) {
			Vector3 p = (viewMatrix * center);
			Vector2 p2d = new Vector2(p.x, p.y);
			return points.OrderBy(point => { 
				var tmp = (viewMatrix * point);
				return GetAim(p2d, new Vector2(tmp.x, tmp.y));
			}).ToList();
		}

	}

}
