using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace AnimationRiggedExtensions.AnimationJob
{
    /// <summary>
    /// The FixedTwistCorrection job.
    /// </summary>
    [Unity.Burst.BurstCompile]
    public struct FixedTwistCorrectionJob : IWeightedAnimationJob
    {
        /// <summary>The Transform handle for the source object Transform.</summary>
        public ReadOnlyTransformHandle source;
        /// <summary>Cached inverse local rotation for source Transform.</summary>
        public Quaternion sourceInverseBindRotation;
        /// <summary>The local twist axis</summary>
        public Vector3 axisMask;
        public float zeroAngleShift;

        /// <summary>List of Transform handles for the twist nodes.</summary>
        public NativeArray<ReadWriteTransformHandle> twistTransforms;
        /// <summary>List of weights for the twist nodes.</summary>
        public NativeArray<PropertyStreamHandle> twistWeights;
        /// <summary>List of cached local rotation for twist nodes.</summary>
        public NativeArray<Quaternion> twistBindRotations;

        /// <summary>Buffer used to store weights during job execution.</summary>
        public NativeArray<float> weightBuffer;

        /// <inheritdoc />
        public FloatProperty jobWeight { get; set; }

        /// <summary>
        /// Defines what to do when processing the root motion.
        /// </summary>
        /// <param name="stream">The animation stream to work on.</param>
        public void ProcessRootMotion(AnimationStream stream) { }

        /// <summary>
        /// Defines what to do when processing the animation.
        /// </summary>
        /// <param name="stream">The animation stream to work on.</param>
        public void ProcessAnimation(AnimationStream stream)
        {
            float w = jobWeight.Get(stream);
            if (w > 0f)
            {
                if (twistTransforms.Length == 0)
                    return;

                AnimationStreamHandleUtility.ReadFloats(stream, twistWeights, weightBuffer);

                var twistShift = Quaternion.AngleAxis(zeroAngleShift, Vector3.up);
                var invTwistShift = Quaternion.Inverse(twistShift);

                Quaternion twistRot = TwistRotation(axisMask, sourceInverseBindRotation * twistShift * source.GetLocalRotation(stream));
                //Quaternion twistRot = sourceInverseBindRotation * twistShift * source.GetLocalRotation(stream);
                Quaternion invTwistRot = Quaternion.Inverse(twistRot);
                for (int i = 0; i < twistTransforms.Length; ++i)
                {
                    ReadWriteTransformHandle twistTransform = twistTransforms[i];

                    float twistWeight = Mathf.Clamp(weightBuffer[i], -1f, 1f);

                    var targetTwistRot = Mathf.Sign(twistWeight) < 0f ? invTwistRot : twistRot;
                    targetTwistRot = twistShift * targetTwistRot;
/*                    
                    var twistBindEuler = twistBindRotations[i].eulerAngles;
                    var targetTwistEuler = targetTwistRot.eulerAngles;

                    Debug.Log($"bone Y: {twistBindEuler.y:0.0}, target Y: {targetTwistEuler.y:0.0}");

                    if (targetTwistEuler.y > 180f) {
                        targetTwistEuler.y -= 360f;
                    }

                    var mulWeight = Mathf.Abs(twistWeight) * w;
                    var lerpedEuler = new Vector3(
                        twistBindEuler.x,
                        Mathf.Lerp(twistBindEuler.y, targetTwistEuler.y, mulWeight),
                        twistBindEuler.z
                    );
                    twistTransform.SetLocalRotation(stream, Quaternion.Euler(lerpedEuler));
*/
                    var mulWeight = Mathf.Abs(twistWeight) * w;
                    var rot = Quaternion.Slerp(twistBindRotations[i], targetTwistRot, mulWeight);

                    rot *= invTwistShift;
                    twistTransform.SetLocalRotation(stream, rot);


                    //Quaternion rot = Quaternion.Lerp(Quaternion.identity, Mathf.Sign(twistWeight) < 0f ? invTwistRot : twistRot, Mathf.Abs(twistWeight));
                    //twistTransform.SetLocalRotation(stream, Quaternion.Lerp(twistBindRotations[i], rot, w));

                    //Quaternion rot = Quaternion.Lerp(twistBindRotations[i], Mathf.Sign(twistWeight) < 0f ? invTwistRot : twistRot, Mathf.Abs(twistWeight) * w);
                    //twistTransform.SetLocalRotation(stream, rot);

                    // Required to update handles with binding info.
                    twistTransforms[i] = twistTransform;
                }
            }
            else
            {
                for (int i = 0; i < twistTransforms.Length; ++i)
                    AnimationRuntimeUtils.PassThrough(stream, twistTransforms[i]);
            }
        }

        static Quaternion TwistRotation(Vector3 axis, Quaternion rot)
        {
            return new Quaternion(axis.x * rot.x, axis.y * rot.y, axis.z * rot.z, rot.w);
        }
    }

    /// <summary>
    /// This interface defines the data mapping for TwistCorrection.
    /// </summary>
    public interface IFixedTwistCorrectionData
    {
        /// <summary>The source Transform that influences the twist nodes.</summary>
        Transform source { get; }
        /// <summary>
        /// The list of Transforms on which to apply twist corrections.
        /// Each twist node has a weight that ranges from -1 to 1 to control
        /// how closely a twist node follows source rotation (from 0 to 1),
        /// or opposite rotation (from -1 to 0).
        /// </summary>
        WeightedTransformArray twistNodes { get; }
        /// <summary>The local twist axis of the source Transform on which to evaluate twist rotation.</summary>
        Vector3 twistAxis { get; }
        /// <summary>The path to the twist nodes property in the constraint component.</summary>
        string twistNodesProperty { get; }

        float zeroAngleShift { get; }
    }

    /// <summary>
    /// The TwistCorrection job binder.
    /// </summary>
    /// <typeparam name="T">The constraint data type</typeparam>
    public class FixedTwistCorrectionJobBinder<T> : AnimationJobBinder<FixedTwistCorrectionJob, T>
        where T : struct, IAnimationJobData, IFixedTwistCorrectionData
    {
        /// <inheritdoc />
        public override FixedTwistCorrectionJob Create(Animator animator, ref T data, Component component)
        {
            var job = new FixedTwistCorrectionJob();

            job.source = ReadOnlyTransformHandle.Bind(animator, data.source);
            job.sourceInverseBindRotation = Quaternion.Inverse(data.source.localRotation);
            job.axisMask = data.twistAxis;
            job.zeroAngleShift = data.zeroAngleShift;

            WeightedTransformArray twistNodes = data.twistNodes;

            WeightedTransformArrayBinder.BindReadWriteTransforms(animator, component, twistNodes, out job.twistTransforms);
            WeightedTransformArrayBinder.BindWeights(animator, component, twistNodes, data.twistNodesProperty, out job.twistWeights);

            job.weightBuffer = new NativeArray<float>(twistNodes.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            job.twistBindRotations = new NativeArray<Quaternion>(twistNodes.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < twistNodes.Count; ++i)
            {
                var sourceTransform = twistNodes[i].transform;
                job.twistBindRotations[i] = sourceTransform.localRotation;
            }

            return job;
        }

        /// <inheritdoc />
        public override void Destroy(FixedTwistCorrectionJob job)
        {
            job.twistTransforms.Dispose();
            job.twistWeights.Dispose();
            job.twistBindRotations.Dispose();
            job.weightBuffer.Dispose();
        }
    }
}
