#if UNITY_EDITOR

//
// Enjoy - Sidiusz
//

using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

#pragma warning disable 649

namespace UnityEditor
{
    class BatchRagdollBuilder : ScriptableWizard
    {
        [SerializeField] List<GameObject> targetObjects = new List<GameObject>();
        [Header("Config File")]
        [Tooltip("Select the configuration file or create it\n'Create > Batch Ragdoll Builder > BR B Config'")]
        [SerializeField] BRBConfig boneConfig;
        [Space]
        [SerializeField] float totalMass = 20f;
        [SerializeField] bool flipForward = false;

        Vector3 right = Vector3.right;
        Vector3 up = Vector3.up;
        Vector3 forward = Vector3.forward;

        Vector3 worldRight = Vector3.right;
        Vector3 worldUp = Vector3.up;
        Vector3 worldForward = Vector3.forward;

        Vector2 windowSize = new Vector2(550f, 400f);

        class BoneInfo
        {
            public float minLimit, maxLimit, swingLimit, radiusScale, density, summedMass;
            public string name;
            public Vector3 axis, normalAxis;
            public BoneInfo parent;
            public Transform anchor;
            public CharacterJoint joint;
            public Type colliderType;
            public ArrayList children = new ArrayList();
        }

        ArrayList bones;
        BoneInfo rootBone;

        string CheckConsistency()
        {
            if (boneConfig == null)
            {
                return "The Bone Config is not assigned. Select or create at least one config with 'Create > Batch Ragdoll Builder > BRB Config'.";
            }
            if (targetObjects.Count == 0)
            {
                return "No object is assigned to the Target Object.";
            }

            PrepareBones();
            HashSet<Transform> uniqueAnchors = new HashSet<Transform>();
            foreach (BoneInfo bone in bones)
            {
                if (bone.anchor)
                {
                    if (uniqueAnchors.Contains(bone.anchor))
                    {
                        return $"{bone.name} has a duplicate bone.";
                    }
                    uniqueAnchors.Add(bone.anchor);
                }
                else
                {
                    return $"{bone.name} has not been assigned yet.\n";
                }
            }

            return "";
        }

        void OnDrawGizmos()
        {
            if (boneConfig != null && FindBoneTransform(targetObjects[0], boneConfig.pelvis) != null)
            {
                Transform pelvis = FindBoneTransform(targetObjects[0], boneConfig.pelvis);
                Gizmos.color = Color.red; Gizmos.DrawRay(pelvis.position, pelvis.TransformDirection(right));
                Gizmos.color = Color.green; Gizmos.DrawRay(pelvis.position, pelvis.TransformDirection(up));
                Gizmos.color = Color.blue; Gizmos.DrawRay(pelvis.position, pelvis.TransformDirection(forward));
            }
        }

        [MenuItem("Tools/Batch Ragdoll Builder")]
        static void CreateWizard()
        {
            BatchRagdollBuilder wizard = ScriptableWizard.DisplayWizard<BatchRagdollBuilder>("Create Ragdoll");
            wizard.minSize = wizard.windowSize;
            wizard.OnWizardUpdate();
        }

        void DecomposeVector(out Vector3 normalCompo, out Vector3 tangentCompo, Vector3 outwardDir, Vector3 outwardNormal)
        {
            outwardNormal = outwardNormal.normalized;
            normalCompo = outwardNormal * Vector3.Dot(outwardDir, outwardNormal);
            tangentCompo = outwardDir - normalCompo;
        }

        void CalculateAxes()
        {
            if (boneConfig != null && targetObjects.Count > 0)
            {
                Transform head = FindBoneTransform(targetObjects[0], boneConfig.head);
                Transform pelvis = FindBoneTransform(targetObjects[0], boneConfig.pelvis);
                if (head != null && pelvis != null)
                {
                    up = CalculateDirectionAxis(pelvis.InverseTransformPoint(head.position));
                }

                Transform rightElbow = FindBoneTransform(targetObjects[0], boneConfig.rightElbow);
                if (rightElbow != null && pelvis != null)
                {
                    Vector3 removed, temp;
                    DecomposeVector(out temp, out removed, pelvis.InverseTransformPoint(rightElbow.position), up);
                    right = CalculateDirectionAxis(removed);
                }

                if (right != Vector3.zero && up != Vector3.zero)
                {
                    forward = Vector3.Cross(right, up);
                    if (flipForward)
                        forward = -forward;
                }
                else
                {
                    helpString = ("Unable to calculate forward vector: right or up is Vector3.zero.");
                }
            }
            else
            {
                if (boneConfig == null)
                {
                    helpString = ("Bone Config is null, axes cannot be calculated.");
                }

                if (targetObjects.Count == 0)
                {
                    helpString = ("Target Objects list is empty, axes cannot be calculated.");
                }
            }
        }

        void OnWizardUpdate()
        {
            errorString = CheckConsistency();
            CalculateAxes();

            helpString = "Make sure your character is in T-Stand.\nMake sure the blue axis faces in the same direction the character is looking.\nUse flipForward to flip the direction";

            isValid = errorString.Length == 0;
        }

        void PrepareBones()
        {
            bones = new ArrayList();

            if (boneConfig != null && targetObjects.Count > 0)
            {
                Transform pelvis = FindBoneTransform(targetObjects[0], boneConfig.pelvis);
                if (pelvis != null)
                {
                    worldRight = pelvis.TransformDirection(right);
                    worldUp = pelvis.TransformDirection(up);
                    worldForward = pelvis.TransformDirection(forward);

                    rootBone = new BoneInfo();
                    rootBone.name = "Pelvis";
                    rootBone.anchor = pelvis;
                    rootBone.parent = null;
                    rootBone.density = 2.5F;
                    bones.Add(rootBone);

                    AddMirroredJoint("Hips", boneConfig.leftHips, boneConfig.rightHips, "Pelvis", worldRight, worldForward, -20, 70, 30, typeof(CapsuleCollider), 0.3F, 1.5F);
                    AddMirroredJoint("Knee", boneConfig.leftKnee, boneConfig.rightKnee, "Hips", worldRight, worldForward, -80, 0, 0, typeof(CapsuleCollider), 0.25F, 1.5F);

                    AddJoint("Middle Spine", boneConfig.middleSpine, "Pelvis", worldRight, worldForward, -20, 20, 10, null, 1, 2.5F);

                    AddMirroredJoint("Arm", boneConfig.leftArm, boneConfig.rightArm, "Middle Spine", worldUp, worldForward, -70, 10, 50, typeof(CapsuleCollider), 0.25F, 1.0F);
                    AddMirroredJoint("Elbow", boneConfig.leftElbow, boneConfig.rightElbow, "Arm", worldForward, worldUp, -90, 0, 0, typeof(CapsuleCollider), 0.20F, 1.0F);

                    AddJoint("Head", boneConfig.head, "Middle Spine", worldRight, worldForward, -40, 25, 25, null, 1, 1.0F);
                }
                else
                {
                    helpString = ("Pelvis bone not found in the target object with boneConfig. Check if the pelvis bone name matches the target object's skeleton structure.");
                }
            }
            else
            {
                if (boneConfig == null)
                {
                    helpString = ("Bone configuration (boneConfig) is null. Assign a valid bone configuration in the inspector.");
                }
                if (targetObjects.Count == 0)
                {
                    helpString = ("No target objects assigned. Add at least one target object to create ragdolls.");
                }
            }
        }

        void OnWizardCreate()
        {
            isValid = boneConfig != null && bones.Count > 0;
            if (!isValid)
            {
                errorString = "Bone configuration is missing or incomplete.";
                return;
            }

            foreach (GameObject target in targetObjects)
            {
                Cleanup(target);
                BuildCapsules(target);
                AddBreastColliders(target);
                AddHeadCollider(target);

                BuildBodies(target);
                BuildJoints(target);
                CalculateMass(target);
            }
        }

        BoneInfo FindBone(string name)
        {
            foreach (BoneInfo bone in bones)
            {
                if (bone.name == name)
                    return bone;
            }
            return null;
        }

        void AddMirroredJoint(string name, string leftAnchor, string rightAnchor, string parent, Vector3 worldTwistAxis, Vector3 worldSwingAxis, float minLimit, float maxLimit, float swingLimit, Type colliderType, float radiusScale, float density)
        {
            AddJoint("Left " + name, leftAnchor, parent, worldTwistAxis, worldSwingAxis, minLimit, maxLimit, swingLimit, colliderType, radiusScale, density);
            AddJoint("Right " + name, rightAnchor, parent, worldTwistAxis, worldSwingAxis, minLimit, maxLimit, swingLimit, colliderType, radiusScale, density);
        }

        void AddJoint(string name, string anchorName, string parent, Vector3 worldTwistAxis, Vector3 worldSwingAxis, float minLimit, float maxLimit, float swingLimit, Type colliderType, float radiusScale, float density)
        {
            BoneInfo bone = new BoneInfo();
            bone.name = name;
            bone.anchor = FindBoneTransform(targetObjects[0], anchorName);
            bone.axis = worldTwistAxis;
            bone.normalAxis = worldSwingAxis;
            bone.minLimit = minLimit;
            bone.maxLimit = maxLimit;
            bone.swingLimit = swingLimit;
            bone.density = density;
            bone.colliderType = colliderType;
            bone.radiusScale = radiusScale;

            if (FindBone(parent) != null)
                bone.parent = FindBone(parent);
            else if (name.StartsWith("Left"))
                bone.parent = FindBone("Left " + parent);
            else if (name.StartsWith("Right"))
                bone.parent = FindBone("Right " + parent);


            bone.parent.children.Add(bone);
            bones.Add(bone);
        }

        void BuildCapsules(GameObject target)
        {
            foreach (BoneInfo bone in bones)
            {
                if (bone.colliderType != typeof(CapsuleCollider))
                    continue;

                Transform boneTransform = FindBoneTransform(target, bone.anchor.name);
                if (!boneTransform) continue;

                int direction;
                float distance;
                if (bone.children.Count == 1)
                {
                    BoneInfo childBone = (BoneInfo)bone.children[0];
                    Transform childTransform = FindBoneTransform(target, childBone.anchor.name);
                    if (!childTransform) continue;
                    Vector3 endPoint = childTransform.position;
                    CalculateDirection(boneTransform.InverseTransformPoint(endPoint), out direction, out distance);
                }
                else
                {
                    Vector3 endPoint = (boneTransform.position - FindBoneTransform(target, bone.parent.anchor.name).position) + boneTransform.position;
                    CalculateDirection(boneTransform.InverseTransformPoint(endPoint), out direction, out distance);

                    if (boneTransform.GetComponentsInChildren(typeof(Transform)).Length > 1)
                    {
                        Bounds bounds = new Bounds();
                        foreach (Transform child in boneTransform.GetComponentsInChildren(typeof(Transform)))
                        {
                            bounds.Encapsulate(boneTransform.InverseTransformPoint(child.position));
                        }

                        if (distance > 0)
                            distance = bounds.max[direction];
                        else
                            distance = bounds.min[direction];
                    }
                }

                CapsuleCollider collider = Undo.AddComponent<CapsuleCollider>(boneTransform.gameObject);
                collider.direction = direction;

                Vector3 center = Vector3.zero;
                center[direction] = distance * 0.5F;
                collider.center = center;
                collider.height = Mathf.Abs(distance);
                collider.radius = Mathf.Abs(distance * bone.radiusScale);
            }
        }

        void Cleanup(GameObject target)
        {
            foreach (BoneInfo bone in bones)
            {
                Transform boneTransform = FindBoneTransform(target, bone.anchor.name);
                if (!boneTransform) continue;

                Component[] joints = boneTransform.GetComponentsInChildren(typeof(Joint));
                foreach (Joint joint in joints)
                    Undo.DestroyObjectImmediate(joint);

                Component[] bodies = boneTransform.GetComponentsInChildren(typeof(Rigidbody));
                foreach (Rigidbody body in bodies)
                    Undo.DestroyObjectImmediate(body);

                Component[] colliders = boneTransform.GetComponentsInChildren(typeof(Collider));
                foreach (Collider collider in colliders)
                    Undo.DestroyObjectImmediate(collider);
            }
        }

        void BuildBodies(GameObject target)
        {
            foreach (BoneInfo bone in bones)
            {
                Transform boneTransform = FindBoneTransform(target, bone.anchor.name);
                if (!boneTransform) continue;
                Undo.AddComponent<Rigidbody>(boneTransform.gameObject);
                boneTransform.GetComponent<Rigidbody>().mass = bone.density;
            }
        }

        void BuildJoints(GameObject target)
        {
            foreach (BoneInfo bone in bones)
            {
                if (bone.parent == null)
                    continue;

                Transform boneTransform = FindBoneTransform(target, bone.anchor.name);
                if (!boneTransform) continue;

                CharacterJoint joint = Undo.AddComponent<CharacterJoint>(boneTransform.gameObject);
                bone.joint = joint;

                joint.axis = CalculateDirectionAxis(boneTransform.InverseTransformDirection(bone.axis));
                joint.swingAxis = CalculateDirectionAxis(boneTransform.InverseTransformDirection(bone.normalAxis));
                joint.anchor = Vector3.zero;
                joint.connectedBody = FindBoneTransform(target, bone.parent.anchor.name).GetComponent<Rigidbody>();
                joint.enablePreprocessing = false;

                SoftJointLimit limit = new SoftJointLimit();
                limit.contactDistance = 0;

                limit.limit = bone.minLimit;
                joint.lowTwistLimit = limit;

                limit.limit = bone.maxLimit;
                joint.highTwistLimit = limit;

                limit.limit = bone.swingLimit;
                joint.swing1Limit = limit;

                limit.limit = 0;
                joint.swing2Limit = limit;
            }
        }

        void CalculateMassRecurse(BoneInfo bone, GameObject target)
        {
            Transform boneTransform = bone.anchor;
            if (boneTransform != null)
            {
                float mass = boneTransform.GetComponent<Rigidbody>().mass;
                foreach (BoneInfo child in bone.children)
                {
                    CalculateMassRecurse(child, target);
                    mass += child.summedMass;
                }
                bone.summedMass = mass;
            }
            else
            {
                errorString += $"{bone.name} has not been assigned yet.\n";
            }
        }

        void CalculateMass(GameObject target)
        {
            CalculateMassRecurse(rootBone, target);

            float massScale = totalMass / rootBone.summedMass;
            foreach (BoneInfo bone in bones)
            {
                Transform boneTransform = FindBoneTransform(target, bone.anchor.name);
                if (boneTransform)
                    boneTransform.GetComponent<Rigidbody>().mass *= massScale;
            }

            CalculateMassRecurse(rootBone, target);
        }

        void CalculateDirection(Vector3 point, out int direction, out float distance)
        {
            direction = 0;
            if (Mathf.Abs(point[1]) > Mathf.Abs(point[0]))
                direction = 1;
            if (Mathf.Abs(point[2]) > Mathf.Abs(point[direction]))
                direction = 2;

            distance = point[direction];
        }

        Vector3 CalculateDirectionAxis(Vector3 point)
        {
            int direction = 0;
            float distance;
            CalculateDirection(point, out direction, out distance);
            Vector3 axis = Vector3.zero;
            if (distance > 0)
                axis[direction] = 1.0F;
            else
                axis[direction] = -1.0F;
            return axis;
        }

        int SmallestComponent(Vector3 point)
        {
            int direction = 0;
            if (Mathf.Abs(point[1]) < Mathf.Abs(point[0]))
                direction = 1;
            if (Mathf.Abs(point[2]) < Mathf.Abs(point[direction]))
                direction = 2;
            return direction;
        }

        int LargestComponent(Vector3 point)
        {
            int direction = 0;
            if (Mathf.Abs(point[1]) > Mathf.Abs(point[0]))
                direction = 1;
            if (Mathf.Abs(point[2]) > Mathf.Abs(point[direction]))
                direction = 2;
            return direction;
        }

        int SecondLargestComponent(Vector3 point)
        {
            int smallest = SmallestComponent(point);
            int largest = LargestComponent(point);
            if (smallest < largest)
            {
                int temp = largest;
                largest = smallest;
                smallest = temp;
            }

            if (smallest == 0 && largest == 1)
                return 2;
            else if (smallest == 0 && largest == 2)
                return 1;
            else
                return 0;
        }

        Bounds Clip(Bounds bounds, Transform relativeTo, Transform clipTransform, bool below)
        {
            int axis = LargestComponent(bounds.size);

            if (Vector3.Dot(worldUp, relativeTo.TransformPoint(bounds.max)) > Vector3.Dot(worldUp, relativeTo.TransformPoint(bounds.min)) == below)
            {
                Vector3 min = bounds.min;
                min[axis] = relativeTo.InverseTransformPoint(clipTransform.position)[axis];
                bounds.min = min;
            }
            else
            {
                Vector3 max = bounds.max;
                max[axis] = relativeTo.InverseTransformPoint(clipTransform.position)[axis];
                bounds.max = max;
            }
            return bounds;
        }

        Bounds GetBreastBounds(Transform relativeTo, GameObject target)
        {
            Bounds bounds = new Bounds();
            if (boneConfig != null)
            {
                bounds.Encapsulate(relativeTo.InverseTransformPoint(FindBoneTransform(target, boneConfig.leftHips).position));
                bounds.Encapsulate(relativeTo.InverseTransformPoint(FindBoneTransform(target, boneConfig.rightHips).position));
                bounds.Encapsulate(relativeTo.InverseTransformPoint(FindBoneTransform(target, boneConfig.leftArm).position));
                bounds.Encapsulate(relativeTo.InverseTransformPoint(FindBoneTransform(target, boneConfig.rightArm).position));
            }
            Vector3 size = bounds.size;
            size[SmallestComponent(bounds.size)] = size[LargestComponent(bounds.size)] / 2.0F;
            bounds.size = size;
            return bounds;
        }

        void AddBreastColliders(GameObject target)
        {
            if (boneConfig != null)
            {
                Transform pelvisTransform = FindBoneTransform(target, boneConfig.pelvis);
                Transform middleSpineTransform = FindBoneTransform(target, boneConfig.middleSpine);

                if (middleSpineTransform != null && pelvisTransform != null)
                {
                    Bounds bounds;
                    BoxCollider box;

                    bounds = Clip(GetBreastBounds(pelvisTransform, target), pelvisTransform, middleSpineTransform, false);
                    box = Undo.AddComponent<BoxCollider>(pelvisTransform.gameObject);
                    box.center = bounds.center;
                    box.size = bounds.size;

                    bounds = Clip(GetBreastBounds(middleSpineTransform, target), middleSpineTransform, middleSpineTransform, true);
                    box = Undo.AddComponent<BoxCollider>(middleSpineTransform.gameObject);
                    box.center = bounds.center;
                    box.size = bounds.size;
                }
                else if (pelvisTransform != null)
                {
                    Bounds bounds = new Bounds();
                    bounds.Encapsulate(pelvisTransform.InverseTransformPoint(FindBoneTransform(target, boneConfig.leftHips).position));
                    bounds.Encapsulate(pelvisTransform.InverseTransformPoint(FindBoneTransform(target, boneConfig.rightHips).position));
                    bounds.Encapsulate(pelvisTransform.InverseTransformPoint(FindBoneTransform(target, boneConfig.leftArm).position));
                    bounds.Encapsulate(pelvisTransform.InverseTransformPoint(FindBoneTransform(target, boneConfig.rightArm).position));

                    Vector3 size = bounds.size;
                    size[SmallestComponent(bounds.size)] = size[LargestComponent(bounds.size)] / 2.0F;

                    BoxCollider box = Undo.AddComponent<BoxCollider>(pelvisTransform.gameObject);
                    box.center = bounds.center;
                    box.size = size;
                }
            }
        }

        void AddHeadCollider(GameObject target)
        {
            if (boneConfig != null)
            {
                Transform headTransform = FindBoneTransform(target, boneConfig.head);
                if (!headTransform) return;

                if (headTransform.GetComponent<Collider>())
                    Destroy(headTransform.GetComponent<Collider>());

                Transform leftArmTransform = FindBoneTransform(target, boneConfig.leftArm);
                Transform rightArmTransform = FindBoneTransform(target, boneConfig.rightArm);
                if (!leftArmTransform || !rightArmTransform) return;

                float radius = Vector3.Distance(leftArmTransform.transform.position, rightArmTransform.transform.position);
                radius /= 4;

                SphereCollider sphere = Undo.AddComponent<SphereCollider>(headTransform.gameObject);
                sphere.radius = radius;
                Vector3 center = Vector3.zero;

                int direction;
                float distance;
                CalculateDirection(headTransform.InverseTransformPoint(FindBoneTransform(target, boneConfig.pelvis).position), out direction, out distance);
                if (distance > 0)
                    center[direction] = -radius;
                else
                    center[direction] = radius;
                sphere.center = center;
            }
        }

        Transform FindBoneTransform(GameObject target, string boneName)
        {
            Transform[] allTransforms = target.GetComponentsInChildren<Transform>();
            foreach (Transform transform in allTransforms)
            {
                if (transform.name == boneName)
                    return transform;
            }
            return null;
        }
    }
}
#endif