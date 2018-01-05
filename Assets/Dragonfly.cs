using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnitySteer.Behaviors;

public class Dragonfly : MonoBehaviour {

    public Transform model;
    public bool debugMode = false;

    public float lowestHeight = 1f;
    public float wanderRadius = 1.5f;
    public float minPauseTime = 3f;
    public float maxPauseTime = 5f;

    public LayerMask DetectLayers
    {
        get { return m_detectLayers; }
        set { m_detectLayers = value; }
    }
    [SerializeField] private LayerMask m_detectLayers;

    public enum FlyScenario
    {
        WONDER,
        LAND,
        PLAYWATER
    }
    public FlyScenario flyScenario = FlyScenario.WONDER;

    private AutonomousVehicle _autoVehicle;
    private SteerForPoint _toPoint;
    private SteerForWander _toWander;
    private SteerForTether _toTether;

    public bool IsBorn
    {
        get { return m_isBorn; }
        set { m_isBorn = value; }
    }
    private bool m_isBorn = false;
    private bool isEntering = false;
    private bool isLeaving = false;

    public System.Action OnEnterFinish;
    public System.Action OnLandFinish;
    public System.Action OnWanderFinish;

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
            //FindNewTarget();
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
        if (!m_isBorn)
            return;

        // models follow agent except rot x & z
        model.transform.position = transform.position;
        Vector3 newRot = transform.eulerAngles;
        newRot.x = newRot.z = 0;
        model.transform.eulerAngles = newRot;
    }

    private void RestBeforeNewTarget()
    {
        StartCoroutine(Rest());
    }

    private IEnumerator Rest()
    {
        // TODO: disable SteerForWander maybe?
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

    public void DoEnter()
    {
        // fly in: wander down

        // OnWanderFinish
    }
}
