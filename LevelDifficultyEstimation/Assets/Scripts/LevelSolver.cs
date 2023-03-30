using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

public class LevelSolver : Agent
{
    public bool isFailed;

    public float moveSpeed = 5f;
    public float turnSpeed = 150f;
    public float jumpForce = 5f;
    private Rigidbody rb;

    private bool jumpAble = false;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    void ReturnReward()
    {
        float normalizedDistance = Vector3.Distance(transform.position, RLManager.generatorCopy.latestObject.transform.position) / 
            Vector3.Distance(RLManager.generatorCopy.latestObject.transform.position, RLManager.generatorCopy.latestObject2.transform.position);
        float intReward = -1 * Mathf.Log(normalizedDistance);

        SetReward(intReward);
    }
    public override void OnEpisodeBegin()
    {
        transform.position = RLManager.generatorCopy.startPosition + new Vector3(0, 1, 0);
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformPoint(RLManager.generatorCopy.latestObject.transform.position));
        /*        sensor.AddObservation(transform.InverseTransformDirection(targetObject.transform.eulerAngles));*/
        sensor.AddObservation(Vector3.Distance(transform.position, RLManager.generatorCopy.latestObject.transform.position));
        sensor.AddObservation(transform.InverseTransformPoint(RLManager.generatorCopy.latestObject2.transform.position));
        /*        sensor.AddObservation(transform.InverseTransformDirection(latestObject.transform.eulerAngles));*/
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        var continuousActions = actionBuffers.ContinuousActions;
        var discreteActions = actionBuffers.DiscreteActions;

        float horizontal = continuousActions[0];
        float vertical = continuousActions[1];

        Vector3 movement = new Vector3(horizontal, 0, vertical) * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);

        // 캐릭터 회전
        float turn = continuousActions[2];
        Quaternion turnRotation = Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);
        rb.MoveRotation(rb.rotation * turnRotation);

        // 점프
        float jump = discreteActions[0];
        if (jump == 1 && jumpAble && rb.velocity.y == 0)
        {
            jumpAble = false;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        if (RLManager.mode == 0)
        {
            ReturnReward();
        }

        float normalizedDistance = Vector3.Distance(transform.position, RLManager.generatorCopy.endPosition) / Vector3.Distance(RLManager.generatorCopy.startPosition, RLManager.generatorCopy.endPosition);
        if (transform.position.y < -5 || normalizedDistance < 0.01f)
        {
            if (RLManager.mode == 0)
            {
                RLManager.currentSolverStep += 1;
                if (transform.position.y < -5)
                {
                    isFailed = true;
                    SetReward(-10);
                }
            }
            else if (RLManager.mode == 1)
            {
                RLManager.currentGeneratorStep += 1;
            }
            EndEpisode();
            if (isFailed && RLManager.mode == 1)
            {
                RLManager.generatorCopy.SetReward(-1);
                isFailed = false;
            }
            RLManager.generatorCopy.EndEpisode();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("floor") && transform.position.y > collision.gameObject.transform.position.y)
        {
            collision.gameObject.tag = "touchedFloor";
            LevelGenerator.isGenerateAble = true;
            if (RLManager.mode == 0)
            {
                SetReward(100);
            }else if(RLManager.mode == 1)
            {
                RLManager.generatorCopy.SetReward(10);
            }
        }
        if (collision.gameObject.CompareTag("touchedFloor") && transform.position.y > collision.gameObject.transform.position.y)
        {
            jumpAble = true;
        }
    }
}
