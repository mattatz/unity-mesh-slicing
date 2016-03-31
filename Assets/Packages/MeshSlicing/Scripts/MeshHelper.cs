using UnityEngine;

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace mattatz.MeshSlicing {

	public class MeshHelper {

		public static Vector3[] GetBoundingBoxCorners (Bounds bounds) {
            return new Vector3[] {
                // bottom
                bounds.min, 
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z), 
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z), 
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z), 

                // top
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z), 
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z), 
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z), 
                bounds.max
            };
		}

        public static void FindMinMaxCorners (Vector3[] corners, Matrix4x4 viewMatrix, out int closest, out int farthest) {
            int ci = 0, fi = 0;
            float cd, fd;
            cd = fd = (viewMatrix * corners[0]).z;

            for(int i = 1, n = corners.Length; i < n; i++) {
                float d = (viewMatrix * corners[i]).z;
                if(d < cd) {
                    ci = i;
                    cd = d;
                }
                if(d > fd) {
                    fi = i;
                    fd = d;
                }
            }

            closest = ci;
            farthest = fi;
        }

		// http://answers.unity3d.com/questions/228841/dynamically-combine-verticies-that-share-the-same.html
        public static Mesh AutoWeld (Mesh mesh, float threshold, float bucketStep) {
            Vector3[] oldVertices = mesh.vertices;
            Vector3[] newVertices = new Vector3[oldVertices.Length];
            int[] old2new = new int[oldVertices.Length];
            int newSize = 0;

            // Find AABB
            Vector3 min = new Vector3 (float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3 (float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < oldVertices.Length; i++) {
                if (oldVertices[i].x < min.x) min.x = oldVertices[i].x;
                if (oldVertices[i].y < min.y) min.y = oldVertices[i].y;
                if (oldVertices[i].z < min.z) min.z = oldVertices[i].z;
                if (oldVertices[i].x > max.x) max.x = oldVertices[i].x;
                if (oldVertices[i].y > max.y) max.y = oldVertices[i].y;
                if (oldVertices[i].z > max.z) max.z = oldVertices[i].z;
            }

            // Make cubic buckets, each with dimensions "bucketStep"
            int bucketSizeX = Mathf.FloorToInt ((max.x - min.x) / bucketStep) + 1;
            int bucketSizeY = Mathf.FloorToInt ((max.y - min.y) / bucketStep) + 1;
            int bucketSizeZ = Mathf.FloorToInt ((max.z - min.z) / bucketStep) + 1;
            List<int>[,,] buckets = new List<int>[bucketSizeX, bucketSizeY, bucketSizeZ];

            // Make new vertices
            for (int i = 0; i < oldVertices.Length; i++) {
                // Determine which bucket it belongs to
                int x = Mathf.FloorToInt ((oldVertices[i].x - min.x) / bucketStep);
                int y = Mathf.FloorToInt ((oldVertices[i].y - min.y) / bucketStep);
                int z = Mathf.FloorToInt ((oldVertices[i].z - min.z) / bucketStep);

                // Check to see if it's already been added
                if (buckets[x, y, z] == null) buckets[x, y, z] = new List<int> (); // Make buckets lazily

                for (int j = 0; j < buckets[x, y, z].Count; j++) {
                    Vector3 to = newVertices[buckets[x, y, z][j]] - oldVertices[i];
                    if (Vector3.SqrMagnitude(to) < threshold) {
                        old2new[i] = buckets[x, y, z][j];
                        goto skip; // Skip to next old vertex if this one is already there
                    }
                }

                // Add new vertex
                newVertices[newSize] = oldVertices[i];
                buckets[x, y, z].Add (newSize);
                old2new[i] = newSize;
                newSize++;

skip:;
            }

            // Make new triangles
            int[] oldTris = mesh.triangles;
            int[] newTris = new int[oldTris.Length];
            for (int i = 0; i < oldTris.Length; i++) {
                newTris[i] = old2new[oldTris[i]];
            }

            Vector3[] finalVertices = new Vector3[newSize];
            for (int i = 0; i < newSize; i++) {
                finalVertices[i] = newVertices[i];
            }

            mesh.Clear();
            mesh.vertices = finalVertices;
            mesh.triangles = newTris;
            mesh.RecalculateNormals ();
            mesh.Optimize ();

			return mesh;
        }

    }

	public class Triangle {
		public int a, b, c; // vertex indices

		public Triangle (int a, int b, int c) {
			this.a = a;
			this.b = b;
			this.c = c;
		}

		public int this[int i] {
			get { 
				int mod = i % 3;
				if(mod == 0) return a;
				else if(mod == 1) return b;
				else if(mod == 2) return c;
				else return a;
			}
		}

		public bool HasEdge(int i0, int i1) {
			bool ea = (a == i0 || a == i1);
			bool eb = (b == i0 || b == i1);
			bool ec = (c == i0 || c == i1);
			return 
				(ea && eb) ||
				(eb && ec) ||
				(ec && ea);
		}

		public override String ToString () {
			return String.Format("({0}) ({1}) ({2})", this.a, this.b, this.c);
		}
	}

	public class MeshData {

		public List<Triangle> triangles;
		public List<HashSet<Triangle>> commons; // index = vertex index

		public MeshData(Mesh mesh) {
			// CAUTION:
			// 	need to combine duplicate vertices
			mesh = MeshHelper.AutoWeld(mesh, float.Epsilon, mesh.bounds.size.x);
			Setup (mesh);
		}

		void Setup (Mesh mesh) {
			triangles = new List<Triangle>();
			commons = new List<HashSet<Triangle>>();

			for(int i = 0, n = mesh.vertexCount; i < n; i++) {
				commons.Add(new HashSet<Triangle>());
			}

			for(int i = 0, n = mesh.triangles.Length; i < n; i += 3) {
				int a = mesh.triangles[i];
				int b = mesh.triangles[i + 1];
				int c = mesh.triangles[i + 2];
				var tri = new Triangle(a, b, c);
				triangles.Add(tri);
				commons[a].Add(tri);
				commons[b].Add(tri);
				commons[c].Add(tri);
			}
		}

		public Triangle GetTriangle(int a, int b, int c) {
			HashSet<Triangle> s = new HashSet<Triangle>(commons[a]);
			s.IntersectWith(commons[b]);
			s.IntersectWith(commons[c]);
			return s.First();
		}

		/*
		 * return Triangle that commons a-b edge from tri
		 */
		public Triangle GetCommon (Triangle tri, int a, int b) {

			var neighbors = GetNeighbors(tri);
			for(int i = 0, n = neighbors.Count; i < n; i++) {
				var neighbor = neighbors.ElementAt(i);
				if(neighbor.HasEdge(a, b)) {
					return neighbor;
				}
			}

			return null;
		}

		public HashSet<Triangle> GetNeighbors (Triangle tri) {
			HashSet<Triangle> ab = new HashSet<Triangle>(commons[tri.a]);
			HashSet<Triangle> bc = new HashSet<Triangle>(commons[tri.b]);
			HashSet<Triangle> ca = new HashSet<Triangle>(commons[tri.c]);
			ab.IntersectWith(commons[tri.b]);
			bc.IntersectWith(commons[tri.c]);
			ca.IntersectWith(commons[tri.a]);
			ab.UnionWith(bc);
			ab.UnionWith(ca);
			ab.Remove(tri);
			return ab;
		}

	}


} 
