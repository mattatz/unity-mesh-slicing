using UnityEngine;
using Random = UnityEngine.Random;

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using mattatz.MeshSlicing;

public class Demo : MonoBehaviour {

	public enum SlicingMode {
		Mesh, BoundingBox
	};

	[SerializeField] SlicingMode mode = SlicingMode.Mesh;
	[SerializeField] GameObject target;
	[SerializeField] GameObject prefab;
	[SerializeField, Range(0.02f, 0.5f)] float slicing = 0.05f;

	MeshData data;
	List<SlicingData> slices;
	List<GameObject> visualizers = new List<GameObject>();

	void Start () {
		data = new MeshData(target.GetComponent<MeshFilter>().sharedMesh);

		Round();
	}
	
	void Update () {
		Round();
	}

	void Round () {
		var dir = target.transform.position - transform.position;
		var right = Vector3.Cross(dir.normalized, transform.up);
		transform.position += right * Time.deltaTime * 2f;
		Slice ();
	}

	void Slice () {
		transform.LookAt(target.transform.position);

		if(mode == SlicingMode.Mesh) {
			slices = MeshSlicing.Slice(slicing, transform, target.GetComponent<Renderer>(), target.GetComponent<MeshFilter>(), data);
		} else {
			slices = BoundingBoxSlicing.Slice(slicing, transform, target.GetComponent<Renderer>().bounds);
		}

		visualizers.ForEach(go => {
			Destroy(go);
		});
		visualizers.Clear();
		visualizers = slices.Select(slice => {
			var go = Instantiate(prefab);
			go.hideFlags = HideFlags.HideAndDontSave;
			var mesh = slice.Build();
			mesh.RecalculateBounds();
			mesh.RecalculateNormals();
			go.GetComponent<MeshFilter>().sharedMesh = mesh;
			return go;
		}).ToList();
	}

	void OnDrawGizmos () {
		if(slices == null) return;

		Gizmos.color = Color.white;
		Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1f);

		Gizmos.color = Color.green;

		for(int i = 0, n = slices.Count; i < n; i++) {
			var points = slices[i].points;
			for(int j = 0, m = points.Count; j < m; j++) {
				var p0 = points[j];
				var p1 = points[(j + 1) % m];
				Gizmos.DrawLine(p0, p1);
			}
		}
	}

}
