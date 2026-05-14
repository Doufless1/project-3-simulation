// ============================================================================
// First-Person Fly Camera — Interactive camera for the 3D lab view.
//
// Controls:
//   - Right-click drag (or Alt + Left-click): Look around (First Person) - Smoothed
//   - Arrow Keys: Look around (Alternative for Trackpads)
//   - WASD / Q E: Move through space - Smoothed
//   - Shift: Sprint
// ============================================================================

using UnityEngine;

namespace HVOFSim.Presentation.Camera
{
    /// <summary>
    /// Smoothed first-person fly camera controller for the 3D laboratory view.
    /// Provides intuitive game-like navigation with professional weight/inertia.
    /// </summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        [Header("Fly Settings")]
        public float LookSensitivity = 3f;
        public float MoveSpeed = 5f;
        public float SprintMultiplier = 3f;
        
        [Header("Smoothing")]
        public float MovementSmoothTime = 0.15f;
        public float RotationSmoothTime = 0.1f;

        // Target values
        private float _targetYaw = 0f;
        private float _targetPitch = 0f;
        private Vector3 _targetPosition;

        // Current smoothed values
        private float _currentYaw = 0f;
        private float _currentPitch = 0f;
        private Vector3 _currentPosition;

        // Velocity references for SmoothDamp
        private float _yawVelocity;
        private float _pitchVelocity;
        private Vector3 _positionVelocity;

        private void Start()
        {
            // Initial First-Person start position, standing in the room looking at the machine
            _currentPosition = new Vector3(0f, 1.6f, -3.5f);
            _targetPosition = _currentPosition;
            
            transform.position = _currentPosition;
            transform.LookAt(new Vector3(0f, 1.0f, 0f));

            var euler = transform.eulerAngles;
            _currentYaw = euler.y;
            _currentPitch = euler.x;
            _targetYaw = _currentYaw;
            _targetPitch = _currentPitch;
        }

        private void LateUpdate()
        {
            HandleLookInput();
            HandleMovementInput();
            
            ApplySmoothing();
        }

        private void HandleLookInput()
        {
            // Look (Right mouse button to look around, leaving Left Click free for interacting with the 3D UI)
            // Or Left Alt + Left Click for laptop trackpad users
            if (Input.GetMouseButton(1) || (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButton(0)))
            {
                _targetYaw += Input.GetAxis("Mouse X") * LookSensitivity;
                _targetPitch -= Input.GetAxis("Mouse Y") * LookSensitivity;
            }

            // Keyboard look (for trackpad users without a mouse)
            float kbdSpeed = LookSensitivity * 60f * Time.deltaTime; // Scale for framerate
            if (Input.GetKey(KeyCode.LeftArrow)) _targetYaw -= kbdSpeed;
            if (Input.GetKey(KeyCode.RightArrow)) _targetYaw += kbdSpeed;
            if (Input.GetKey(KeyCode.UpArrow)) _targetPitch -= kbdSpeed;
            if (Input.GetKey(KeyCode.DownArrow)) _targetPitch += kbdSpeed;

            _targetPitch = Mathf.Clamp(_targetPitch, -89f, 89f);
        }

        private void HandleMovementInput()
        {
            float currentSpeed = MoveSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftShift)) currentSpeed *= SprintMultiplier;

            Vector3 moveDir = Vector3.zero;
            
            // We move relative to our current smoothed rotation
            if (Input.GetKey(KeyCode.W)) moveDir += transform.forward;
            if (Input.GetKey(KeyCode.S)) moveDir -= transform.forward;
            if (Input.GetKey(KeyCode.D)) moveDir += transform.right;
            if (Input.GetKey(KeyCode.A)) moveDir -= transform.right;
            if (Input.GetKey(KeyCode.E)) moveDir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) moveDir -= Vector3.up;

            if (moveDir != Vector3.zero)
            {
                _targetPosition += moveDir.normalized * currentSpeed;
            }
        }

        private void ApplySmoothing()
        {
            // Smooth Rotation
            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, RotationSmoothTime);
            _currentPitch = Mathf.SmoothDampAngle(_currentPitch, _targetPitch, ref _pitchVelocity, RotationSmoothTime);
            transform.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);

            // Smooth Position
            _currentPosition = Vector3.SmoothDamp(_currentPosition, _targetPosition, ref _positionVelocity, MovementSmoothTime);
            transform.position = _currentPosition;
        }
    }
}
