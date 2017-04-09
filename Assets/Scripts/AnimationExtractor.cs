/*  I simply created my own animation format ( a Unity scriptableObject as a container for the data),
 *  that extracted the matrices of each bone for each frame of animation.
 *  This data is then supplied as a matrix array via a compute buffer so that gpu skinning can be used for each instance
 *  and each instance can have its own frame index into the animation data.
 *  Its pretty simple to extract the animation data from legacy animations via animationclip.sampleAnimation(),
 *  mecanim is a bit harder/awkward.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AnimationExtractor : MonoBehaviour {

    private Bone[] bonesArray;
    private Mesh mesh;
    private int rootBoneIndex;
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private BoneAnimation[] boneAnimationArray;
    private RuntimeAnimatorController animatorController;

    void Start() {

        animatorController = GetComponent<Animator>().runtimeAnimatorController;

        //Initialize the extractor, creating the skeleton structure
        Init();

        //Print the skeleton hierarchy
        //PrintBones();
    }

    private void Init() {
        //Define bones structure and everything needed
        DefineStructure();

        //Construct bones hierarchy
        ConstructHierarchy(bonesArray[rootBoneIndex]);

        //Extract animation data
        ExtractAnimationData();
    }

    /// <summary>
    /// Defines the skeleton structure creating for each bone in the model a Bone and filling the structure
    /// </summary>
    private void DefineStructure() {
        //Get the mesh
        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        mesh = skinnedMeshRenderer.sharedMesh;

        //Define and fill the Bone structure
        int bonesNumber = skinnedMeshRenderer.bones.Length;
        bonesArray = new Bone[bonesNumber];

        for (int i = 0; i < bonesNumber; i++) {
            Bone bone = new Bone();
            bonesArray[i] = bone;
            bone.transform = skinnedMeshRenderer.bones[i];
            //save the root bone's index
            if (bone.transform == skinnedMeshRenderer.rootBone)
                rootBoneIndex = i;
            bone.name = bone.transform.gameObject.name;
            bone.bindpose = mesh.bindposes[i];
        }
    }

    /// <summary>
    /// Creates the tree shaped skeleton's structure
    /// </summary>
    /// <param name="bone">Skeleton root bone (root of the tree)</param>
    private void ConstructHierarchy(Bone bone) {
        List<Bone> children = new List<Bone>();
        for (int j = 0; j < bone.transform.childCount; j++) {
            Transform childTransform = bone.transform.GetChild(j);
            Bone childBone = GetBoneByTransform(childTransform);
            if (childBone != null) {
                childBone.parent = bone;
                children.Add(childBone);
            }
        }
        foreach (Bone child in children) {
            ConstructHierarchy(child);
        }
        bone.children = children.ToArray();
    }

    /// <summary>
    /// 
    /// </summary>
    private void ExtractAnimationData() {
        //create folder
        if (!System.IO.Directory.Exists("Assets/Baked Animations/" + gameObject.name))
            AssetDatabase.CreateFolder("Assets/Baked Animations", gameObject.name);

        //for each animation
        foreach (AnimationClip clip in animatorController.animationClips) {
            BoneAnimation boneAnimation = ScriptableObject.CreateInstance<BoneAnimation>();

            //TODO not hardcode the fps
            boneAnimation.fps = 60;
            boneAnimation.animName = clip.name;
            boneAnimation.frames = new BoneAnimationFrame[(int)(clip.length * boneAnimation.fps)];
            boneAnimation.length = clip.length;

            //for each frame
            for (int frameIndex = 0; frameIndex < boneAnimation.frames.Length; frameIndex++) {
                BoneAnimationFrame frame = new BoneAnimationFrame();
                boneAnimation.frames[frameIndex] = frame;
                float second = (float)(frameIndex) / (float)boneAnimation.fps;

                List<Bone> bonesList = new List<Bone>();
                List<Matrix4x4> matrices = new List<Matrix4x4>();
                List<string> bonesHierarchyNames = null;
                if (boneAnimation.bonesHierarchyNames == null)
                    bonesHierarchyNames = new List<string>();
                EditorCurveBinding[] curvesBindingArray = AnimationUtility.GetCurveBindings(clip);

                //for each curve
                foreach (EditorCurveBinding curveBinding in curvesBindingArray) {
                    Bone bone = GetBoneByHierarchyName(bonesArray[rootBoneIndex], bonesArray[rootBoneIndex].name, curveBinding.path);

                    if (bonesList.Contains(bone) || bone == null)
                        continue;
                    bonesList.Add(bone);


                    //get and save the hierarchy name of each bone added to bonesList
                    if (bonesHierarchyNames != null)
                        bonesHierarchyNames.Add(GetBoneHierarchyName(bone));

                    //define AnimationCurve using AnimationUtility.GetEditorCurve(...) and save their float data with curve.Evaluate(second)
                    //TODO remove deprecate
                    AnimationCurve curveRX = AnimationUtility.GetEditorCurve(clip, curveBinding.path, curveBinding.type, "m_LocalRotation.x");
                    AnimationCurve curveRY = AnimationUtility.GetEditorCurve(clip, curveBinding.path, curveBinding.type, "m_LocalRotation.y");
                    AnimationCurve curveRZ = AnimationUtility.GetEditorCurve(clip, curveBinding.path, curveBinding.type, "m_LocalRotation.z");
                    AnimationCurve curveRW = AnimationUtility.GetEditorCurve(clip, curveBinding.path, curveBinding.type, "m_LocalRotation.w");

                    AnimationCurve curvePX = AnimationUtility.GetEditorCurve(clip, curveBinding.path, curveBinding.type, "m_LocalPosition.x");
                    AnimationCurve curvePY = AnimationUtility.GetEditorCurve(clip, curveBinding.path, curveBinding.type, "m_LocalPosition.y");
                    AnimationCurve curvePZ = AnimationUtility.GetEditorCurve(clip, curveBinding.path, curveBinding.type, "m_LocalPosition.z");

                    float evaluatedRX = curveRX.Evaluate(second);
                    float evaluatedRY = curveRY.Evaluate(second);
                    float evaluatedRZ = curveRZ.Evaluate(second);
                    float evaluatedRW = curveRW.Evaluate(second);

                    float evaluatedPX = curvePX.Evaluate(second);
                    float evaluatedPY = curvePY.Evaluate(second);
                    float evaluatedPZ = curvePZ.Evaluate(second);


                    //create Vector3 translation and rotation using the curves defined above and build a Matrix4x4 transformation matrices
                    Vector3 translation = new Vector3(evaluatedPX, evaluatedPY, evaluatedPZ);
                    Quaternion rotation = new Quaternion(evaluatedRX, evaluatedRY, evaluatedRZ, evaluatedRW);

                    NormalizeQuaternion(ref rotation);
                    matrices.Add(Matrix4x4.TRS(translation, rotation, Vector3.one));
                }

                //populate BoneAnimationFrame frame bones and matrices fields with bonesList.ToArray() and matrices.ToArray()
                frame.matrices = matrices.ToArray();
                frame.bones = bonesList.ToArray();
                if (boneAnimation.bonesHierarchyNames == null)
                    boneAnimation.bonesHierarchyNames = bonesHierarchyNames.ToArray();
            }

            //save boneAnimation as asset
            AssetDatabase.CreateAsset(boneAnimation, "Assets/Baked Animations/" + gameObject.name + "/" +clip.name + ".asset");
        }
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Gets the correct skeleton's bone using his Unity hierarchy name
    /// </summary>
    /// <param name="hierarchyName"></param>
    /// <returns></returns>
    private Bone GetBoneByHierarchyName(Bone bone, string name, string hierarchyName) {
        if (hierarchyName.Equals("Armature"))
            return null;

        string[] boneName = hierarchyName.Split('/');

        if (boneName[boneName.Length-1].Equals(name))
            return bone;
        
        foreach (Bone child in bone.children) {
            Bone result = GetBoneByHierarchyName(child, child.name, hierarchyName);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Gets the Bone element relative to the transform taken as input
    /// </summary>
    /// <param name="transform">Transform used to retrieve the Bone element</param>
    /// <returns>Bone got using the transform</returns>
    private Bone GetBoneByTransform(Transform transform) {
        foreach (Bone bone in bonesArray) {
            if (bone.transform == transform) {
                return bone;
            }
        }
        return null;
    }


    private string GetBoneHierarchyName(Bone bone) {
        string str = string.Empty;

        Bone current = bone;
        while (current != null) {
            if (str == string.Empty)
                str = current.name;
            else
                str = current.name + "/" + str;

            current = current.parent;
        }

        return str;
    }

    private void NormalizeQuaternion(ref Quaternion quaternion) {
        float sum = 0;
        for (int i = 0; i < 4; ++i)
            sum += quaternion[i] * quaternion[i];
        float magnitudeInverse = 1 / Mathf.Sqrt(sum);
        for (int i = 0; i < 4; ++i)
            quaternion[i] *= magnitudeInverse;
    }

    /// <summary>
    /// Prints the bone hierarchy
    /// </summary>
    private void PrintBones() {
        string text = string.Empty;
        System.Action<Bone, string> PrintBone = null;
        PrintBone = (bone, prefix) => {
            text += prefix + bone.transform.gameObject.name + "\n";
            prefix += "    ";
            foreach (Bone childBone in bone.children) {
                PrintBone(childBone, prefix);
            }
        };
        PrintBone(bonesArray[rootBoneIndex], string.Empty);
        Debug.LogWarning(text);
    }
}