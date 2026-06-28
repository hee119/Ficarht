using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif


namespace TinyGiantStudio.Text
{
    /// <summary>
    /// Handles input for raycast selector
    /// </summary>
    [AddComponentMenu("Tiny Giant Studio/Modular 3D Text/Input System/Raycast Input Processor", order: 20052)]
    [HelpURL("https://ferdowsur.gitbook.io/modular-3d-text/input/mouse-touch/raycast-input-processor")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RaycastSelector))]
    public class RaycastInputProcessor : MonoBehaviour
    {
        #region Raycast settings
        [Tooltip("If not assigned, it will automatically get Camera.main on Start")]
        public Camera myCamera;
        #endregion Raycast settings


        public Transform pointerOnUI;
        public Transform currentTarget;

        bool dragging = false;

        RaycastSelector raycastSelector;

        // Debounce: null miss 또는 타겟 전환 시 grace frames 적용
        private int _nullRaycastFrames = 0;
        private const int NullGraceFrames = 4;

        private Transform _pendingTarget = null;   // 전환 후보 타겟
        private int _pendingTargetFrames = 0;
        private const int SwitchGraceFrames = 4;   // 새 타겟으로 전환하려면 연속 N프레임 필요


        #region Unity Things
        void Awake()
        {
            raycastSelector = GetComponent<RaycastSelector>();
#if ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Enable();
#endif
        }

        void Start()
        {
            //If no camera assigned, get Camera.main
            if (!myCamera)
            {
                myCamera = Camera.main;
                if (!myCamera)
                    Debug.Log("No camera selected for 3D UI Raycaster");
            }
        }

        void Update()
        {
            if (!myCamera)
                return;

            //If Already dragging stuff, do dragging stuff
            if (dragging)
            {
                Dragging();
                DetectDragEnd();
            }
            else
            {
                SelectPress();
            }
        }

        /// <summary>
        /// Select or press (hover debounce 적용 — 깜빡임 방지)
        /// </summary>
        void SelectPress()
        {
            pointerOnUI = RaycastCheck();

            if (pointerOnUI != null)
            {
                _nullRaycastFrames = 0;

                if (pointerOnUI == currentTarget)
                {
                    // 동일 타겟 유지 — pending 초기화
                    _pendingTarget = null;
                    _pendingTargetFrames = 0;

                    if (PressedButton())
                    {
                        raycastSelector.PressTarget(pointerOnUI);
                        dragging = true;
                    }
                }
                else
                {
                    // 새 타겟 감지
                    bool pressedNow = PressedButton();

                    if (currentTarget == null)
                    {
                        // 이전 타겟 없음 → 즉시 선택
                        raycastSelector.SelectTarget(pointerOnUI);
                        currentTarget = pointerOnUI;
                        _pendingTarget = null;
                        _pendingTargetFrames = 0;

                        if (pressedNow)
                        {
                            raycastSelector.PressTarget(pointerOnUI);
                            dragging = true;
                        }
                    }
                    else
                    {
                        // 다른 타겟으로 전환 — SwitchGraceFrames 프레임 연속 히트해야 전환
                        if (_pendingTarget == pointerOnUI)
                        {
                            _pendingTargetFrames++;
                        }
                        else
                        {
                            _pendingTarget = pointerOnUI;
                            _pendingTargetFrames = 1;
                        }

                        if (pressedNow || _pendingTargetFrames >= SwitchGraceFrames)
                        {
                            // 클릭이거나 충분히 안정된 경우 → 전환 확정
                            raycastSelector.UnselectTarget(currentTarget);
                            raycastSelector.SelectTarget(pointerOnUI);
                            currentTarget = pointerOnUI;
                            _pendingTarget = null;
                            _pendingTargetFrames = 0;

                            if (pressedNow)
                            {
                                raycastSelector.PressTarget(pointerOnUI);
                                dragging = true;
                            }
                        }
                        // 아직 grace 중 → currentTarget 유지 (깜빡임 방지)
                    }
                }
            }
            else
            {
                // Raycast null — NullGraceFrames 후에 unselect
                _nullRaycastFrames++;
                _pendingTarget = null;
                _pendingTargetFrames = 0;

                if (_nullRaycastFrames >= NullGraceFrames && currentTarget != null)
                {
                    raycastSelector.UnselectTarget(currentTarget);
                    currentTarget = null;
                }
                // grace 기간 중 currentTarget 유지
            }
        }
        #endregion Unity things


        void Dragging()
        {
            Vector3 screenPoint = myCamera.WorldToScreenPoint(currentTarget.position);

#if ENABLE_INPUT_SYSTEM
            //Get the mouse position on screen
            Vector3 cursorScreenPoint = new Vector3(Pointer.current.position.ReadValue().x, Pointer.current.position.ReadValue().y, screenPoint.z);
#else
            //Get the mouse position on screen
            Vector3 cursorScreenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, screenPoint.z);
#endif
            //Convert cursor position to world position
            Vector3 cursorPosition = myCamera.ScreenToWorldPoint(cursorScreenPoint);

            raycastSelector.Dragging(currentTarget, cursorPosition);
        }

        bool PressedButton()
        {
#if ENABLE_INPUT_SYSTEM
            if (MouseClicked() || Tapped())
                return true;
            return false;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        bool MouseClicked()
        {
            if (Mouse.current != null)
                return Mouse.current.leftButton.wasPressedThisFrame;

            return false;
        }

        bool Tapped()
        {
            if (Touch.activeTouches.Count > 0)
                return Touch.activeTouches[0].ended;

            return false;
        }
#endif

        Transform RaycastCheck()
        {
#if ENABLE_INPUT_SYSTEM
            Ray ray = myCamera.ScreenPointToRay(Pointer.current.position.ReadValue());
#else
            Ray ray = myCamera.ScreenPointToRay(Input.mousePosition);
#endif
            return raycastSelector.RaycastCheck(ray, myCamera);
        }

        void DetectDragEnd()
        {
            if (MouseButtonReleased() && dragging)
            {
                dragging = false;
                raycastSelector.DragEnded(currentTarget, RaycastCheck());
            }

            if (!Input.touchSupported)
                return;

            if (Input.touchCount > 0)
            {
#if ENABLE_INPUT_SYSTEM
                if (Input.touches[0].phase == UnityEngine.TouchPhase.Ended)
#else
                if (Input.touches[0].phase == TouchPhase.Ended)
#endif
                {
                    dragging = false;
                    raycastSelector.DragEnded(currentTarget, RaycastCheck());
                }
            }
            else
            {
                dragging = false;
                raycastSelector.DragEnded(currentTarget, RaycastCheck());
            }
        }

        bool MouseButtonReleased()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.leftButton.wasReleasedThisFrame;
            return false;
#else
            return Input.GetMouseButtonUp(0);
#endif
        }
    }
}
