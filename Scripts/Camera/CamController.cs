using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class CamController : MonoBehaviour {
	
	//variables not visible in the inspector
    public static float movespeed;
    public static float zoomSpeed;
    public static float mouseSensitivity;
    public static float clampAngle;

	[SerializeField] private float fallbackMouseSensitivity = 200f;
	[SerializeField] private float fallbackClampAngle = 80f;
	[SerializeField] private bool lockCursorWhileRotating = true;
	
	float rotationY = 0;
	float rotationX = 0;
	
	bool canRotate;
	private Vector3 lastMousePos;
	private bool wasRotating;
 
    void Start(){
		//get start rotation
		Vector3 rot = transform.eulerAngles;
		rotationY = rot.y;
		rotationX = rot.x;

		if(mouseSensitivity <= 0f)
			mouseSensitivity = fallbackMouseSensitivity;
		if(clampAngle <= 0f)
			clampAngle = fallbackClampAngle;
    }
	
	void Update(){
		// defensive: other scripts (Settings) may overwrite statics with 0; restore sane defaults
		if(mouseSensitivity <= 0f) mouseSensitivity = fallbackMouseSensitivity;
		if(clampAngle <= 0f) clampAngle = fallbackClampAngle;

		//if the mobile prefab is added to the scene, use mobile controls. Else use pc controls
		if(GameObject.Find("Mobile") == null && GameObject.Find("Mobile multiplayer") == null){
			PcCamera();
		}
		#if PHOTON_MULTIPLAYER
		else if((GameObject.Find("Mobile") && Mobile.camEnabled) || (GameObject.Find("Mobile multiplayer") && MobileMultiplayer.camEnabled)){
			MobileCamera();
		}
		#else
		else if(GameObject.Find("Mobile") && Mobile.camEnabled){
			MobileCamera();
		}
		#endif
	}
	
	void PcCamera(){
		Vector3 planarForward = transform.forward;
		planarForward.y = 0f;
		if(planarForward.sqrMagnitude < 0.0001f) planarForward = Vector3.forward;
		planarForward.Normalize();

		//if key gets pressed move left/right
		if(Input.GetKey("a") || Input.GetKey(KeyCode.LeftArrow)){
			transform.Translate(Vector3.right * Time.deltaTime * -movespeed);
		}
		if(Input.GetKey("d") || Input.GetKey(KeyCode.RightArrow)){
			transform.Translate(Vector3.right * Time.deltaTime * movespeed);
		}
	
		//if key gets pressed move forward/backward
		if(Input.GetKey("w") || Input.GetKey(KeyCode.UpArrow)){
			transform.Translate(planarForward * Time.deltaTime * movespeed, Space.World);
		}
		if(Input.GetKey("s") || Input.GetKey(KeyCode.DownArrow)){
			transform.Translate(-planarForward * Time.deltaTime * movespeed, Space.World);
		}
	
		//if middle or right mouse button is held AND mouse is moving, rotate camera (drag)
		bool rotatingButton = Input.GetMouseButton(2) || Input.GetMouseButton(1);
		if(rotatingButton){
			if(lockCursorWhileRotating){
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			Vector3 mousePos = Input.mousePosition;
			if(wasRotating){
				Vector3 delta = mousePos - lastMousePos;
				if(delta.sqrMagnitude > 0.0001f){
					RotateCamera(delta.x, -delta.y, true);
				}
			}
			lastMousePos = mousePos;
			wasRotating = true;
		}else if(lockCursorWhileRotating){
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			wasRotating = false;
		}
	
		//move camera when you scroll
		transform.Translate(new Vector3(0, 0, Input.GetAxis("Mouse ScrollWheel")) * Time.deltaTime * zoomSpeed);
	}
	
	
	void MobileCamera(){
		// allow editor/desktop right-drag even if mobile prefab exists (useful when testing on PC)
		if(Input.mousePresent && (Input.GetMouseButton(1) || Input.GetMouseButton(2))){
			if(lockCursorWhileRotating){
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			Vector3 mousePos = Input.mousePosition;
			if(wasRotating){
				Vector3 delta = mousePos - lastMousePos;
				if(delta.sqrMagnitude > 0.0001f){
					RotateCamera(delta.x, -delta.y, true);
				}
			}
			lastMousePos = mousePos;
			wasRotating = true;
			return;
		}
		else if(lockCursorWhileRotating){
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			wasRotating = false;
		}

		//check if exactly one finger is touching the screen
		if(Input.touchCount == 1){
			//rotate camera based on the touch position
			Touch touch = Input.GetTouch(0);
			
			if(touch.phase == TouchPhase.Began){
				if(EventSystem.current.IsPointerOverGameObject() || EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)){
					canRotate = false;
				}
				else{
					canRotate = true;
				}
			}
			
			if(!canRotate)
				return;
				
			float mouseX = touch.deltaPosition.x;
			float mouseY = -touch.deltaPosition.y;
			
			RotateCamera(mouseX, mouseY);
		}
		//check for two touches
		else if(Input.touchCount == 2){
            //store two touches
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            //find the position in the previous frame of each touch
            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            //find the magnitude of the vector (the distance) between the touches in each frame
            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            //find the difference in the distances between each frame
            float z = (prevTouchDeltaMag - touchDeltaMag) * 0.001f * zoomSpeed;
			
			//zoom camera by moving it forward/backward
			transform.Translate(new Vector3(0, 0, -z));
		}
	}
	
	
	void RotateCamera(float mouseX, float mouseY, bool useUnscaledDeltaTime = false){
		float dt = useUnscaledDeltaTime ? Time.unscaledDeltaTime : Time.deltaTime;
		float sens = mouseSensitivity > 0f ? mouseSensitivity : fallbackMouseSensitivity;
		float clamp = clampAngle > 0f ? clampAngle : fallbackClampAngle;

		//check if mobile controls are enabled to adjust sensitivity
		if(GameObject.Find("Mobile") == null && GameObject.Find("Mobile multiplayer") == null){
			rotationY += mouseX * sens * dt;
			rotationX += mouseY * sens * dt;
		}
		else{
			rotationY += mouseX * sens * dt * 0.02f;
			rotationX += mouseY * sens * dt * 0.02f;	
		}
	
		//clamp x rotation to limit it
		rotationX = Mathf.Clamp(rotationX, -clamp, clamp);
	
		//apply rotation
		transform.rotation = Quaternion.Euler(rotationX, rotationY, 0.0f);
	}
}
