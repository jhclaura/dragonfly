using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnitySteer.Behaviors;

public class Dragonfly : MonoBehaviour {

    public Transform model;
    public bool debugMode = false;

    public float lerpRate = 1f;
    public float turnTime = 0.2f;

    public LayerMask DetectLayers
    {
        get { return m_detectLayers; }
        set { m_detectLayers = value; }
    }
    [SerializeField] private LayerMask m_detectLayers;

    public enum FlyScenario
    {
        WANDER,
        LAND
    }
    public FlyScenario flyScenario = FlyScenario.WANDER;
    private enum FlyStatus
    {
        ENTER,
        LAND,
        LANDED,
        WANDER,
        SCARED,
        LEAVE
    }
    private FlyStatus flyStatus;

    private Vector3 force;
    private float deltaTime;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private float lowestHeight = 0.3f;

    [Header("Descend")]
    public float descendSpeed = 10f;
    public float descendWanderSide = 0.5f;
    private Vector3 descendTarget;

    [Header("Wander")]
    public float wanderRadius = 1.5f;
    public float minPauseTime = 3f;
    public float maxPauseTime = 5f;

    [Header("Land")]
    public int landMaxStops = 3;
    private Vector3 m_landTarget;
    public Vector3 LandTarget
    {
        get { return m_landTarget; }
        set { 
            m_landTarget = value;
            hasLandTarget = true;
        }
    }
    private bool hasLandTarget = false;
    private bool landed = false;
    private Vector3 landNormal;
    private int landCount = 0;

    [Header("Leave")]
    public float leaveSpeed = 20f;
    public float leaveWanderSide = 0.2f;
    public int scareMaxCount = 3;
    private Vector3 leaveTarget;
    private int scareCount = 0;

    private AutonomousVehicle _autoVehicle;
    private SteerForPoint _toPoint;
    private SteerForWander _toWander;
    private SteerForTether _toTether;

    public bool CanMove
    {
        get { return m_canMove; }
        set { m_canMove = value; }
    }
    private bool m_canMove = false;
    private bool isEntering = false;
    private bool isLeaving = false;

    public System.Action OnEnterFinish;
    public System.Action OnLandFinish;
    public System.Action OnWanderFinish;
    public System.Action OnLeaveFinish;



    /////////////////
    /// FUNCTIONS ///
    /////////////////
    private void OnEnable()
    {
        if(_autoVehicle==null)
        {
            _autoVehicle = GetComponent<AutonomousVehicle>();
            _toPoint = GetComponent<SteerForPoint>();
            _toWander = GetComponent<SteerForWander>();
            _toTether = GetComponent<SteerForTether>();
        }

        if (_toPoint != null)
        {
            _toPoint.OnArrival += (_) => RestBeforeNewTarget();
            _toPoint.enabled = false;
        }

        if (_toWander != null)
        {
            _toWander.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (_toPoint != null)
            _toPoint.OnArrival -= (_) => RestBeforeNewTarget();
    }

    private void Start()
    {
        _autoVehicle.CanMove = false;
    }

    private void Update()
    {
        // For triggerin event in Editor to test
#if UNITY_EDITOR
        if (Input.GetKeyDown("1"))
        {
            CanMove = true;
            leaveTarget = transform.position;

            descendTarget = Random.insideUnitSphere * 4f;
            if (descendTarget.y < 0)
                descendTarget.y *= -1;
            descendTarget.y = Mathf.Clamp(descendTarget.y, 0.1f, 2f);

            // calculate target height, e.g. +1 above surface
            //RaycastHit hit;
            //if (Physics.Raycast(transform.position, Vector3.down, out hit, 20f)) {
            //    descendTarget = hit.point;
            //}

            Debug.Log(descendTarget);
            flyStatus = FlyStatus.ENTER;
        }
        else if (Input.GetKeyDown("2"))
        {
            if (flyStatus == FlyStatus.WANDER)
            {
                if (OnWanderFinish != null)
                    OnWanderFinish();
            }
            flyStatus = FlyStatus.LAND;
            FindLandTarget();
        }
        else if (Input.GetKeyDown("3"))
        {
            if (flyStatus == FlyStatus.WANDER)
            {
                if (OnWanderFinish != null)
                    OnWanderFinish();
            }
            flyStatus = FlyStatus.LEAVE;
            Debug.Log("Start leaving");
        }
#endif

        if (!CanMove)
            return;

        switch (flyStatus)
        {
            case FlyStatus.ENTER:
                Entering();
                break;

            case FlyStatus.LANDED:
                LandedAndChecking();
                break;

            case FlyStatus.LEAVE:
                Leaving();
                break;
        }

        deltaTime = Time.deltaTime;

        // Update agent
        if(targetPosition - transform.position != Vector3.zero)
            targetRotation = Quaternion.LookRotation(targetPosition - transform.position);
        
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, lerpRate * deltaTime / turnTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpRate * deltaTime);


        // Update models follow agent, except rot x & z
        model.transform.position = transform.position;
        Vector3 newRot = transform.eulerAngles;
        if(!landed)
        {
            newRot.x = newRot.z = 0;
        }
        model.transform.eulerAngles = newRot;
    }

    /////////////////////////////////////////////////////////////////////////////
    /// Collider
    /////////////////////////////////////////////////////////////////////////////
    private void OnTriggerEnter(Collider other)
    {
        scareCount++;
        switch (flyStatus)
        {
            case FlyStatus.ENTER:
                StartCoroutine(FlyAway(other.transform.position - transform.position, 3f, true, FlyStatus.ENTER));
                break;

            case FlyStatus.LEAVE:
                StartCoroutine(FlyAway(other.transform.position - transform.position, 1f, true, FlyStatus.LEAVE));
                break;
        }
    }

    /////////////////////////////////////////////////////////////////////////////
    /// Behaviors
    /////////////////////////////////////////////////////////////////////////////
    public void Entering()
    {
        // fly in
        targetPosition = Vector3.MoveTowards(transform.position, descendTarget, descendSpeed * deltaTime);
        //small random x & z
        targetPosition.x += Mathf.Sin(Time.time) * ((Mathf.Cos(Time.time) + 1f) / 2f * descendWanderSide);

        // OnFlyInFinish
        if((descendTarget - transform.position).sqrMagnitude < 1f)
        {
            Debug.Log("Entered!");

            if (flyScenario == FlyScenario.WANDER)
            {
                flyStatus = FlyStatus.WANDER;
                StartCoroutine(Wandering());
            }
            else {
                flyStatus = FlyStatus.LAND;
                FindLandTarget();
            }

            if (OnEnterFinish != null)
                OnEnterFinish();
        }
    }

    public IEnumerator Wandering()
    {
        Debug.Log("Start wandering");
        while(flyStatus == FlyStatus.WANDER)
        {
            yield return new WaitForSeconds(Random.Range(minPauseTime, maxPauseTime));

            // wander
            Vector3 newTarget = Random.insideUnitSphere * wanderRadius;
            newTarget.y = Mathf.Clamp(newTarget.y, -0.5f, 0.5f);
            targetPosition += newTarget;
        }
    }

    public void FindLandTarget()
    {
        if(hasLandTarget)
        {
            StartCoroutine(Landing());
        }
        else {
            StartCoroutine(FindTarget());
        }            
    }

    IEnumerator FindTarget()
    {
        // SphereCast to find landTarget
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, 2f, transform.forward, out hit, 10f, DetectLayers))
        {
            LandTarget = hit.point;
            Debug.Log("Found land target!");
        }
        else // if not, change rotation a little bit and FindLandTarget again
        {
            float newHeading = Random.Range(-45f, 45f);
            targetRotation.eulerAngles += new Vector3(newHeading/3f, newHeading, 0);
        }
        yield return null;
        yield return null;
        yield return null;
        FindLandTarget();
    }

    public IEnumerator Landing()
    {
        // break if no landTarget
        if (!hasLandTarget)
            yield break;

        while ((LandTarget - transform.position).sqrMagnitude > 0.05f * 0.05f)
        {
            targetPosition = Vector3.MoveTowards(transform.position, LandTarget, leaveSpeed * deltaTime);
            yield return null;
        }

        Debug.Log("Maybe landed~");

        // change orientation to things normal
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, 2f, DetectLayers))
        {
            landNormal = hit.normal;
            landed = true;
            targetRotation = Quaternion.Euler(landNormal);  // doesn't seem to be right...

            if (OnLandFinish != null)
                OnLandFinish();
            
            Debug.Log("Landed!");
            flyStatus = FlyStatus.LANDED;
            landCount++;
            StartCoroutine(LandedAndChilling());
        }
        else
        {
            Debug.Log("Nah, didn't land~");
            landed = false;
            LandTarget = Vector3.zero;
            hasLandTarget = false;
            FindLandTarget();
        }
    }

    public IEnumerator LandedAndChilling()
    {
        Debug.Log("Landed And Chilling");

        yield return new WaitForSeconds(maxPauseTime);

        landed = false;
        hasLandTarget = false;

        if(landCount>landMaxStops)
        {
            flyStatus = FlyStatus.LEAVE;
            Debug.Log("Start leaving");
        }
        else 
        {            
            StartCoroutine(FlyAway(LandTarget - transform.position, 5f, false, FlyStatus.LANDED));
        }
        LandTarget = Vector3.zero;
    }

    public void LandedAndChecking()
    {
        // check if landTarget moves
        if ((LandTarget - transform.position).sqrMagnitude > 0.05f * 0.05f)
        {
            // TODO: cancel LandedAndChilling()!!

            Debug.Log("Target moves! Ahhh");
            landed = false;
            hasLandTarget = false;

            scareCount++;
            StartCoroutine(FlyAway(LandTarget - transform.position, 5f, true, FlyStatus.LANDED));
            LandTarget = Vector3.zero;
        }
    }

    private IEnumerator FlyAway(Vector3 direction, float waitTime, bool scared, FlyStatus preStatus)
    {   
        // leave
        if(scareCount>scareMaxCount) {            
            flyStatus = FlyStatus.LEAVE;
            yield break;
        }

        flyStatus = FlyStatus.SCARED;
        Vector3.Normalize(direction);
        // FLY
        if (scared)
            targetPosition = transform.position + direction * 2f;
        else
            targetPosition = transform.position + direction;
        
        yield return null;

        yield return new WaitForSeconds(waitTime);

        // do what it was doing
        if(flyStatus == FlyStatus.LANDED)
        {
            flyStatus = FlyStatus.LAND;
            FindLandTarget();
        }
        else
        {
            flyStatus = preStatus;
        }

    }

    public void Leaving()
    {
        // fly out
        targetPosition = Vector3.MoveTowards(transform.position, leaveTarget, leaveSpeed * deltaTime);
        //small random x & z
        targetPosition.x += Mathf.Sin(Time.time) * (Mathf.Cos(Time.time) + 1f) / 2f * descendWanderSide;

        // OnLeaveFinish
        if ((leaveTarget - transform.position).sqrMagnitude < 1f)
        {
            Debug.Log("Left!");
            CanMove = false;

            if ( OnLeaveFinish != null)
                OnLeaveFinish();
        }
    }


    private void RestBeforeNewTarget()
    {
        StartCoroutine(Rest());
    }

    private IEnumerator Rest()
    {
        yield return new WaitForSeconds(Random.Range(minPauseTime, maxPauseTime));
        FindNewTarget();
    }

    private void FindNewTarget()
    {
        Vector3 newPos;
        GetRandomPoint(transform.position, wanderRadius, out newPos);
        _toPoint.TargetPoint = newPos;
        _toPoint.enabled = true;
    }

    private bool GetRandomPoint(Vector3 center, float range, out Vector3 result)
    {
        result = center;
        for(int i=0; i<5; i++)
        {
            Vector3 newDir = Random.insideUnitSphere;
            newDir.y *= 0.3f;
            float newRange = range * Random.Range(0.5f, 1f);

            Vector3 possibleResult = center + newDir * newRange;
            //if(!Physics.Raycast(center, newDir, newRange, DetectLayers))
            if (possibleResult.sqrMagnitude < _toTether.MaximumDistance * _toTether.MaximumDistance
                && possibleResult.y > lowestHeight)
            {
                if (debugMode)
                    Debug.DrawRay(center, newDir * newRange, Color.green, 1f);

                result = center + newDir * newRange;
                return true;
            }
            else
            {
                if (debugMode)
                    Debug.DrawRay(center, newDir * newRange, Color.blue, 1f);
            }
        }
        result.x *= 0.5f;
        result.z *= 0.5f;
        return false;
    }


}
