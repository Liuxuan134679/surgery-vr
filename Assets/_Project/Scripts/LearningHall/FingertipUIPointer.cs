using Autohand;
using UnityEngine;

namespace ShuQi.VR
{
    [DefaultExecutionOrder(11000)]
    public sealed class FingertipUIPointer : HandCanvasPointer
    {
        public Finger finger;

        void FollowHand()
        {
            if(!finger || !finger.hand || finger.isMissingReferences) return;
            // Use the saved open pose as a palm-relative anchor without changing the animated fingers.
            var pose=finger.poseData[(int)FingerPoseEnum.Open].poseRelativeMatrix;
            var distal=finger.hand.transform.localToWorldMatrix * pose[(int)FingerJointEnum.knuckle]
                * pose[(int)FingerJointEnum.middle] * pose[(int)FingerJointEnum.distal];
            Vector3 tip=distal.MultiplyPoint3x4(pose[(int)FingerJointEnum.tip].GetColumn(3));
            transform.SetPositionAndRotation(tip,
                Quaternion.LookRotation(tip-(Vector3)distal.GetColumn(3), finger.hand.transform.up));
        }

        protected override void Preprocess()
        {
            if(!isActiveAndEnabled) return;
            FollowHand();
            UICamera.farClipPlane=raycastLength;
            UICamera.transform.SetPositionAndRotation(transform.position,transform.rotation);
        }

        protected override void LateUpdate()
        {
            FollowHand();
            base.LateUpdate();
        }
    }
}
