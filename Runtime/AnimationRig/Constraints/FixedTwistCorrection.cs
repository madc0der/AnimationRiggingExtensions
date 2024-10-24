using AnimationRiggedExtensions.AnimationJob;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace AnimationRiggedExtensions.AnimationRig.Constraints
{
    /// <summary>
    /// The TwistCorrection constraint data.
    /// </summary>
    [System.Serializable]
    public struct FixedTwistCorrectionData : IAnimationJobData, IFixedTwistCorrectionData
    {
        /// <summary>
        /// Axis type for TwistCorrection.
        /// </summary>
        public enum Axis
        {
            /// <summary>X Axis.</summary>
            X,
            /// <summary>Y Axis.</summary>
            Y,
            /// <summary>Z Axis.</summary>
            Z
        }

        [SyncSceneToStream, SerializeField] Transform m_Source;

        [NotKeyable, SerializeField] Axis m_TwistAxis;
        [SyncSceneToStream, SerializeField, WeightRange(-1f, 1f)] WeightedTransformArray m_TwistNodes;

        [SyncSceneToStream, SerializeField] float m_ZeroAngleShift;

        /// <summary>The source Transform that influences the twist nodes.</summary>
        public Transform sourceObject { get => m_Source; set => m_Source = value; }

        /// <inheritdoc />
        public WeightedTransformArray twistNodes
        {
            get => m_TwistNodes;
            set => m_TwistNodes = value;
        }

        /// <inheritdoc />
        public Axis twistAxis { get => m_TwistAxis; set => m_TwistAxis = value; }

        public float zeroAngleShift { get => m_ZeroAngleShift; set => m_ZeroAngleShift = value; }

        /// <inheritdoc />
        Transform IFixedTwistCorrectionData.source => m_Source;
        /// <inheritdoc />
        Vector3 IFixedTwistCorrectionData.twistAxis => Convert(m_TwistAxis);

        /// <inheritdoc />
        string IFixedTwistCorrectionData.twistNodesProperty => ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(m_TwistNodes));

        float IFixedTwistCorrectionData.zeroAngleShift => m_ZeroAngleShift;

        static Vector3 Convert(Axis axis)
        {
            if (axis == Axis.X)
                return Vector3.right;

            if (axis == Axis.Y)
                return Vector3.up;

            return Vector3.forward;
        }

        /// <inheritdoc />
        bool IAnimationJobData.IsValid()
        {
            if (m_Source == null)
                return false;

            for (int i = 0; i < m_TwistNodes.Count; ++i)
                if (m_TwistNodes[i].transform == null)
                    return false;

            return true;
        }

        /// <inheritdoc />
        void IAnimationJobData.SetDefaultValues()
        {
            m_Source = null;
            m_TwistAxis = Axis.Z;
            m_TwistNodes.Clear();
        }
    }

    /// <summary>
    /// TwistCorrection constraint.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Animation Rigging/Fixed Twist Correction")]
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwistCorrection.html")]
    public class FixedTwistCorrection : RigConstraint<
        FixedTwistCorrectionJob,
        FixedTwistCorrectionData,
        FixedTwistCorrectionJobBinder<FixedTwistCorrectionData>
        >
    {
        /// <inheritdoc />
        protected override void OnValidate()
        {
            base.OnValidate();
            var weights = m_Data.twistNodes;
            WeightedTransformArray.OnValidate(ref weights, -1f, 1f);
            m_Data.twistNodes = weights;
        }
    }
}