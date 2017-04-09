using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bone {
    public Transform transform = null;
    public Matrix4x4 bindpose;
    public Bone parent = null;
    public Bone[] children = null;
    public Matrix4x4 animationMatrix;
    public Matrix4x4 hierarchyMatrix;
    public string name = null;
}
