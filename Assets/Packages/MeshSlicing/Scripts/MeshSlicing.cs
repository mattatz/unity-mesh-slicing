using UnityEngine;
using Random = UnityEngine.Random;

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using mattatz.MeshSlicing;

namespace mattatz.MeshSlicing {

    public class SlicingData {
        public Matrix4x4 viewMatrix;
        public Vector3 normal;
        public List<Vector3> points;
        public SlicingData(Matrix4x4 viewMatrix, Vector3 normal, List<Vector3> points) {
            this.viewMatrix = viewMatrix;
            this.normal = normal;
            this.points = points;
        }

        public Mesh Build () {
            if(points.Count < 2) return new Mesh();

            Vector3 center = Vector3.zero;
            points.ForEach(p => {
				center += p;
            });
            center /= points.Count;

            points = MathHelper.ClockWise(center, viewMatrix, points);

            // check forward direction or not
            var v0 = points[0];
            var v1 = points[1];
            var dir = Vector3.Cross(v0 - center, v1 - center);
            var norm = Vector3.Normalize(dir);
            bool forward = Vector3.Dot(norm, normal) > 0f;

            var mesh = new Mesh();
            Vector3[] vertices = new Vector3[points.Count + 1];
            for(int i = 0, n = points.Count; i < n; i++) {
                vertices[i] = points[i];
            }

            int centerIndex = vertices.Length - 1;
            vertices[centerIndex] = center;

            int[] triangles = new int[points.Count * 3];

            if(!forward) {
                for(int i = 0, n = points.Count; i < n; i++) {
                    int offset = i * 3;
                    triangles[offset + 0] = centerIndex;
                    triangles[offset + 1] = i;
                    triangles[offset + 2] = (i + 1) % n;
                }
            } else {
                for(int i = points.Count - 1, n = points.Count; i >= 0; i--) {
                    int offset = i * 3;
                    triangles[offset + 0] = centerIndex;
                    triangles[offset + 1] = i;
                    triangles[offset + 2] = (i - 1) < 0 ? n - 1 : i - 1;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            return mesh;
        }

    }

    public class MeshSlicing {

		public static List<SlicingData> Slice (float slicing, Transform view, Renderer renderer, MeshFilter filter) {
            MeshData data = new MeshData(filter.sharedMesh);
			return Slice (slicing, view, renderer, filter, data);
		}

		public static List<SlicingData> Slice (float slicing, Transform view, Renderer renderer, MeshFilter filter, MeshData data) {
			slicing = Mathf.Clamp(slicing, 0.001f, 0.5f);

            var viewMatrix = MathHelper.MakeViewMatrix(view.right, view.up, view.forward, view.position);

            int closest, farthest;

            // bounding box corners
			Vector3[] corners = MeshHelper.GetBoundingBoxCorners(renderer.bounds);
            MeshHelper.FindMinMaxCorners(corners, viewMatrix, out closest, out farthest);

            var origin = corners[closest];
            var to = corners[farthest];

            Vector3 dir = view.forward;
            float distance = Vector3.Project((to - origin), dir.normalized).magnitude;

        	List<SlicingData> slices = new List<SlicingData>();

            for(float t = 0f, n = 1f + slicing; t < n; t += slicing) {
                Vector3 p = origin + dir * distance * t;

                var plane = new Plane(dir.normalized, p);
                var intersections = GetIntersections(plane, filter, data);
                if(intersections.Count <= 0) continue;

                slices.Add(new SlicingData(viewMatrix, dir, intersections));
            }

			return slices;
        }

        static List<Vector3> GetIntersections (Plane plane, MeshFilter filter, MeshData data) {
            List<Vector3> points = new List<Vector3>();
            Vector3[] vertices = filter.sharedMesh.vertices;
            Ray ray = new Ray();

            Triangle start = new Triangle(-1, -1, -1);
            int a = 0, b = 0;

			/*
            Func<int, int, bool> debug = (int from, int to) => {
                var v0 = filter.transform.TransformPoint(vertices[a]);
                var v1 = filter.transform.TransformPoint(vertices[b]);
                Debug.DrawLine(v0, v1, new Color(1f, 0f, 0f, 1.0f), 50f);
                return true;
            };

            Func<Triangle, bool> debugTriangle = (Triangle t) => {
                var v0 = filter.transform.TransformPoint(vertices[t.a]);
                var v1 = filter.transform.TransformPoint(vertices[t.b]);
                var v2 = filter.transform.TransformPoint(vertices[t.c]);
                Debug.DrawLine(v0, v1, new Color(0f, 1f, 0f, 0.7f), 50f);
                Debug.DrawLine(v1, v2, new Color(0f, 1f, 0f, 0.7f), 50f);
                Debug.DrawLine(v2, v0, new Color(0f, 1f, 0f, 0.7f), 50f);
                return false;
            };
			*/

            Func<Triangle, int, int, bool> intersect = (Triangle tri, int ignore0, int ignore1) => {
                for(int j = 0; j < 3; j++) {
                    int index0 = tri[j];
                    int index1 = tri[(j + 1) % 3];

                    if((index0 == ignore0 || index0 == ignore1) && (index1 == ignore0 || index1 == ignore1)) {
                        continue;
                    }

                    var v0 = filter.transform.TransformPoint(vertices[index0]);
                    var v1 = filter.transform.TransformPoint(vertices[index1]);
                    if(!plane.SameSide(v0, v1)) {
                        var dir = v1 - v0;
                        ray.origin = v0;
                        ray.direction = dir.normalized;
                        float distance;
                        if(plane.Raycast(ray, out distance)) {
                            a = index0;
                            b = index1;
                            points.Add (ray.GetPoint(distance));
                        }
                        return true;
                    }
                }
                return false;
            };

            List<Triangle> triangles = data.triangles;
            for(int i = 0, n = triangles.Count; i < n; i++) {
                var tri = triangles[i];
                if(intersect(tri, -1, -1)) {
                    start = tri;
                    // debug(a, b);
                    break;
                }
            }

            // not found intersection
            if(points.Count <= 0) return points;

            Triangle next = data.GetCommon(start, a, b);

            while(next != start) {

                if(!intersect(next, a, b)) {
                    break;
                }

                next = data.GetCommon(next, a, b);
                if(next == null) {
                    // Debug.Log ("null error");
                    break;
                }
            }
            // Debug.Log (start == next);

            return points;
        }

        List<Vector3> GetIntersections (Plane plane, MeshFilter mf, List<HashSet<int>> edges) {
            List<Vector3> points = new List<Vector3>();

            Vector3[] vertices = mf.sharedMesh.vertices;
            Ray ray = new Ray();

            for(int from = 0, n = edges.Count; from < n; from++) {
                var v0 = mf.transform.TransformPoint(vertices[from]);
                foreach(int to in edges[from]) {
                    var v1 = mf.transform.TransformPoint(vertices[to]);
                    if(!plane.SameSide(v0, v1)) {
                        var dir = v1 - v0;
                        ray.origin = v0;
                        ray.direction = dir.normalized;
                        float distance;
                        if(plane.Raycast(ray, out distance)) {
                            points.Add (ray.GetPoint(distance));
                        }
                    }
                }
            }

            return points;
        }

    }

}

