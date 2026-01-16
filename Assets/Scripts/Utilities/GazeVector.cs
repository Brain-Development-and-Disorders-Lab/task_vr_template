using UnityEngine;

namespace Utilities
{
    /// <summary>
    /// Struct to generalize a pair of vector components representing the left and right eye gaze estimates.
    /// </summary>
    public struct GazeVector
    {
        private Vector3 _left;
        private Vector3 _right;

        public GazeVector(Vector3 left, Vector3 right)
        {
            _left = left;
            _right = right;
        }

        public void SetAdjustments(Vector3 left, Vector3 right)
        {
            _left = left;
            _right = right;
        }

        public readonly Vector3 GetLeft() => _left;

        public readonly Vector3 GetRight() => _right;
    }
}
